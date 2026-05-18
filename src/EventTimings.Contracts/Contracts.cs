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

public sealed record RiderContactImportDto(
	string FullName,
	string? Email = null,
	string? Phone = null);

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

public sealed record FinishedTimeReportRowDto(
	string RiderId,
	string BibNumber,
	string FullName,
	string Category,
	string? RouteName,
	DateTimeOffset? StartedAt,
	DateTimeOffset? FinishedAt,
	long? ElapsedSeconds,
	string Status);

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

public sealed record RiderManagementDto(
	string RiderId,
	string BibNumber,
	string FullName,
	string Category,
	string? RouteTypeId,
	string? RouteTypeName,
	string? Email = null,
	string? Phone = null);

public sealed record RiderCreateRequest(
	string BibNumber,
	string FullName,
	string Category,
	string? RouteTypeId,
	string? Email = null,
	string? Phone = null);

public sealed record RiderUpdateRequest(
	string BibNumber,
	string FullName,
	string Category,
	string? RouteTypeId,
	string? Email = null,
	string? Phone = null);

public sealed record RouteTypeDto(
	string RouteTypeId,
	string Name,
	int DistanceMiles,
	bool IsActive);

public sealed record RouteTypeCreateRequest(
	string Name,
	int DistanceMiles);

public sealed record RouteTypeUpdateRequest(
	string Name,
	int DistanceMiles,
	bool IsActive);

public sealed record OfficialDto(
	string OfficialId,
	string FullName,
	bool IsActive,
	DateTimeOffset UpdatedAt);

public sealed record OfficialCreateRequest(
	string FullName,
	string Pin);

public sealed record OfficialUpdateRequest(
	string FullName,
	string? Pin,
	bool IsActive);

public sealed record PagedTimingSessionsDto(
	IReadOnlyList<TimingSessionDto> Items,
	int TotalCount,
	int Page,
	int PageSize);
