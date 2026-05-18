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
    private readonly Dictionary<string, string> waveNames = [];
    private readonly Dictionary<string, List<string>> waveRiders = [];
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

    public ImportResults ImportParticipants(IEnumerable<RiderImportDto> imports)
    {
        lock (syncRoot)
        {
            var errors = new List<string>();
            var successCount = 0;
            var skippedCount = 0;

            foreach (var dto in imports)
            {
                if (string.IsNullOrWhiteSpace(dto.BibNumber) ||
                    string.IsNullOrWhiteSpace(dto.FullName) ||
                    string.IsNullOrWhiteSpace(dto.Category))
                {
                    errors.Add($"Skipped entry with bib '{dto.BibNumber}': missing required field(s).");
                    skippedCount++;
                    continue;
                }

                if (riders.Any(r => r.BibNumber == dto.BibNumber.Trim()))
                {
                    errors.Add($"Skipped bib #{dto.BibNumber} ({dto.FullName}): bib number already exists.");
                    skippedCount++;
                    continue;
                }

                var riderId = string.IsNullOrWhiteSpace(dto.Id)
                    ? $"rider-{Guid.NewGuid():N}"
                    : dto.Id;
                riders.Add(new RiderSummary(riderId, dto.BibNumber.Trim(), dto.FullName.Trim(), dto.Category.Trim(), dto.AssignedRoute?.Trim()));
                successCount++;
            }

            if (successCount > 0)
            {
                UpdateAuditTrail("Import");
            }

            return new ImportResults(successCount, skippedCount, errors);
        }
    }

    public (EventSnapshot Snapshot, string? Error) AddParticipant(RiderImportDto dto)
    {
        lock (syncRoot)
        {
            if (string.IsNullOrWhiteSpace(dto.BibNumber) ||
                string.IsNullOrWhiteSpace(dto.FullName) ||
                string.IsNullOrWhiteSpace(dto.Category))
            {
                return (CreateSnapshot(), "BibNumber, FullName and Category are required.");
            }

            if (riders.Any(r => r.BibNumber == dto.BibNumber.Trim()))
            {
                return (CreateSnapshot(), $"Bib #{dto.BibNumber} is already registered.");
            }

            var riderId = string.IsNullOrWhiteSpace(dto.Id)
                ? $"rider-{Guid.NewGuid():N}"
                : dto.Id;
            riders.Add(new RiderSummary(riderId, dto.BibNumber.Trim(), dto.FullName.Trim(), dto.Category.Trim(), dto.AssignedRoute?.Trim()));
            UpdateAuditTrail("On-the-day add");
            return (CreateSnapshot(), null);
        }
    }

    public (EventSnapshot Snapshot, string? Error) UpdateRiderRoute(string riderId, string route)
    {
        lock (syncRoot)
        {
            var index = riders.FindIndex(r => r.RiderId == riderId);
            if (index < 0)
            {
                return (CreateSnapshot(), "Rider not found.");
            }

            riders[index] = riders[index] with { AssignedRoute = route };
            UpdateAuditTrail("Route update");
            return (CreateSnapshot(), null);
        }
    }

    public IReadOnlyList<WaveDto> GetWaves()
    {
        lock (syncRoot)
        {
            return waveNames
                .Select(kv => new WaveDto(
                    kv.Key,
                    kv.Value,
                    waveRiders.TryGetValue(kv.Key, out var ids) ? ids.AsReadOnly() : []))
                .ToArray();
        }
    }

    public WaveDto CreateWave(string waveName)
    {
        lock (syncRoot)
        {
            var waveId = $"wave-{Guid.NewGuid().ToString("N")[..8]}";
            waveNames[waveId] = waveName;
            waveRiders[waveId] = [];
            return new WaveDto(waveId, waveName, []);
        }
    }

    public (WaveDto? Wave, string? Error) AssignRiderToWave(string waveId, string riderId)
    {
        lock (syncRoot)
        {
            if (!waveNames.TryGetValue(waveId, out var waveName))
            {
                return (null, "Wave not found.");
            }

            if (!riders.Any(r => r.RiderId == riderId))
            {
                return (null, "Rider not found.");
            }

            var riderList = waveRiders[waveId];
            if (!riderList.Contains(riderId))
            {
                riderList.Add(riderId);
            }

            var riderIndex = riders.FindIndex(r => r.RiderId == riderId);
            if (riderIndex >= 0)
            {
                riders[riderIndex] = riders[riderIndex] with { WaveId = waveId, WaveName = waveName };
            }

            return (new WaveDto(waveId, waveName, riderList.AsReadOnly()), null);
        }
    }

    public (WaveStartResult? Result, string? Error) StartWave(string waveId)
    {
        lock (syncRoot)
        {
            if (!waveRiders.TryGetValue(waveId, out var riderIds))
            {
                return (null, "Wave not found.");
            }

            var waveName = waveNames[waveId];
            var startTime = DateTimeOffset.UtcNow;
            var startedCount = 0;
            var skipped = new List<string>();

            foreach (var riderId in riderIds)
            {
                var rider = riders.FirstOrDefault(r => r.RiderId == riderId);
                if (rider is null)
                {
                    continue;
                }

                var existingSession = timingSessions.LastOrDefault(s => s.RiderId == riderId && s.StoppedAt is null);
                if (existingSession is not null)
                {
                    skipped.Add(rider.FullName);
                    continue;
                }

                timingSessions.Add(new TimingSessionDto(
                    Guid.NewGuid().ToString("N"),
                    rider.RiderId,
                    rider.FullName,
                    $"Wave: {waveName}",
                    startTime,
                    null));
                startedCount++;
            }

            if (startedCount > 0)
            {
                UpdateAuditTrail($"Wave: {waveName}");
            }

            return (new WaveStartResult(startTime, startedCount, skipped), null);
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