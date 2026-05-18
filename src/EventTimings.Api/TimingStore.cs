using EventTimings.Contracts;

namespace EventTimings.Api;

internal sealed class TimingStore
{
    private const string EventCode = "marden-2026";

    private readonly object syncRoot = new();
    private readonly Dictionary<string, string> officialPins = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Mark Phillipson"] = "2468",
        ["Lorna Stafford"] = "1357"
    };

    private readonly List<RiderSummary> riders =
    [
        new("rider-001", "101", "Ava Mitchell", "Women Open"),
        new("rider-002", "204", "Ben Carter", "Men 40+"),
        new("rider-003", "317", "Chloe King", "Junior"),
        new("rider-004", "422", "Daniel Evans", "Men Open")
    ];

    private readonly List<TimingSessionDto> timingSessions = [];
    private string lastUpdatedBy = "System";
    private DateTimeOffset lastUpdatedAt = DateTimeOffset.UtcNow;

    public EventSnapshot GetSnapshot()
    {
        lock (syncRoot)
        {
            return CreateSnapshot();
        }
    }

    public TimingCommandResult StartTiming(TimingCommandRequest request)
    {
        lock (syncRoot)
        {
            var validation = ValidateRequest(request);
            if (validation is not null)
            {
                return new TimingCommandResult(false, validation, CreateSnapshot());
            }

            var rider = riders.First(riderItem => riderItem.RiderId == request.RiderId);
            var existingSession = timingSessions.LastOrDefault(session => session.RiderId == rider.RiderId && session.StoppedAt is null);
            if (existingSession is not null)
            {
                return new TimingCommandResult(false, $"{rider.FullName} is already running.", CreateSnapshot());
            }

            var session = new TimingSessionDto(
                Guid.NewGuid().ToString("N"),
                rider.RiderId,
                rider.FullName,
                request.OfficialName,
                DateTimeOffset.UtcNow,
                null);

            timingSessions.Add(session);
            UpdateAuditTrail(request.OfficialName);

            return new TimingCommandResult(true, $"Started timing for {rider.FullName}.", CreateSnapshot());
        }
    }

    public TimingCommandResult StopTiming(TimingCommandRequest request)
    {
        lock (syncRoot)
        {
            var validation = ValidateRequest(request);
            if (validation is not null)
            {
                return new TimingCommandResult(false, validation, CreateSnapshot());
            }

            var rider = riders.First(riderItem => riderItem.RiderId == request.RiderId);
            var sessionIndex = timingSessions.FindLastIndex(session => session.RiderId == rider.RiderId && session.StoppedAt is null);
            if (sessionIndex < 0)
            {
                return new TimingCommandResult(false, $"No active timing session exists for {rider.FullName}.", CreateSnapshot());
            }

            var currentSession = timingSessions[sessionIndex];
            timingSessions[sessionIndex] = currentSession with { StoppedAt = DateTimeOffset.UtcNow };
            UpdateAuditTrail(request.OfficialName);

            return new TimingCommandResult(true, $"Stopped timing for {rider.FullName}.", CreateSnapshot());
        }
    }

    private string? ValidateRequest(TimingCommandRequest request)
    {
        if (!string.Equals(request.EventCode, EventCode, StringComparison.OrdinalIgnoreCase))
        {
            return "The event code is not valid for this event.";
        }

        if (string.IsNullOrWhiteSpace(request.OfficialName))
        {
            return "Enter the official name before timing riders.";
        }

        if (string.IsNullOrWhiteSpace(request.Pin))
        {
            return "Enter the official PIN before timing riders.";
        }

        if (!officialPins.TryGetValue(request.OfficialName.Trim(), out var expectedPin) ||
            !string.Equals(expectedPin, request.Pin.Trim(), StringComparison.Ordinal))
        {
            return "The official name or PIN is not recognized.";
        }

        if (!riders.Any(rider => rider.RiderId == request.RiderId))
        {
            return "Choose a rider before starting or stopping timing.";
        }

        return null;
    }

    private void UpdateAuditTrail(string officialName)
    {
        lastUpdatedBy = officialName;
        lastUpdatedAt = DateTimeOffset.UtcNow;
    }

    private EventSnapshot CreateSnapshot()
    {
        var status = timingSessions.Any(session => session.StoppedAt is null) ? "Live" : "Ready";

        return new EventSnapshot(
            EventCode,
            "SFA Sportive 2025",
            "Marden Railway Station, TN12 9DR",
            new DateOnly(2025, 5, 11),
            status,
            riders,
            timingSessions.OrderByDescending(session => session.StartedAt).ToArray(),
            lastUpdatedBy,
            lastUpdatedAt);
    }
}