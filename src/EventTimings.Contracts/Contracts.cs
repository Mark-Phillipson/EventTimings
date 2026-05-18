namespace EventTimings.Contracts;

public sealed record RiderSummary(
	string RiderId,
	string BibNumber,
	string FullName,
	string Category,
	string? AssignedRoute = null,
	string? WaveId = null,
	string? WaveName = null);

public sealed record RiderImportDto(
	string? Id,
	string BibNumber,
	string FullName,
	string Category,
	string? AssignedRoute = null);

public sealed record ImportResults(
	int SuccessCount,
	int SkippedCount,
	IReadOnlyList<string> Errors);

public sealed record WaveDto(
	string WaveId,
	string WaveName,
	IReadOnlyList<string> RiderIds);

public sealed record WaveCreateRequest(string WaveName);

public sealed record WaveAssignRiderRequest(string RiderId);

public sealed record WaveStartResult(
	DateTimeOffset StartedAt,
	int StartedCount,
	IReadOnlyList<string> SkippedRiderNames);

public sealed record UpdateRouteRequest(string Route);

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
