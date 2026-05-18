namespace EventTimings.Contracts;

public sealed record RiderSummary(
	string RiderId,
	string BibNumber,
	string FullName,
	string Category);

public sealed record TimingSessionDto(
	string SessionId,
	string RiderId,
	string RiderName,
	string OfficialName,
	DateTimeOffset StartedAt,
	DateTimeOffset? StoppedAt);

public sealed record EventSnapshot(
	string EventCode,
	string EventName,
	string Venue,
	DateOnly EventDate,
	string Status,
	IReadOnlyList<RiderSummary> Riders,
	IReadOnlyList<TimingSessionDto> TimingSessions,
	string LastUpdatedBy,
	DateTimeOffset LastUpdatedAt);

public sealed record TimingCommandRequest(
	string EventCode,
	string OfficialName,
	string Pin,
	string RiderId);

public sealed record TimingCommandResult(
	bool Success,
	string Message,
	EventSnapshot Snapshot);
