using EventTimings.Api.Data;
using EventTimings.Api.Security;
using EventTimings.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EventTimings.Api;

internal sealed class TimingStore
{
    private const string EventCode = "marden-2026";

    private readonly IDbContextFactory<EventTimingsDbContext> dbContextFactory;
    private readonly object syncRoot = new();
    private readonly List<TimingSessionDto> timingSessions = [];
    private readonly Dictionary<string, string> waveNames = [];
    private readonly Dictionary<string, List<string>> waveRiders = [];
    private string lastUpdatedBy = "System";
    private DateTimeOffset lastUpdatedAt = DateTimeOffset.UtcNow;

    public TimingStore(IDbContextFactory<EventTimingsDbContext> dbContextFactory)
    {
        this.dbContextFactory = dbContextFactory;
        EnsureDatabaseInitialized();
    }

    public EventSnapshot GetSnapshot()
    {
        lock (syncRoot)
        {
            using var dbContext = dbContextFactory.CreateDbContext();
            return CreateSnapshot(dbContext);
        }
    }

    public IReadOnlyList<FinishedTimeReportRowDto> GetFinishedTimeReport()
    {
        lock (syncRoot)
        {
            using var dbContext = dbContextFactory.CreateDbContext();

            var riders = dbContext.Riders
                .AsNoTracking()
                .Include(item => item.RouteType)
                .OrderBy(item => item.BibNumber)
                .ThenBy(item => item.FullName)
                .ToArray();

            var reportRows = riders
                .Select(rider =>
                {
                    var riderSessions = timingSessions
                        .Where(session => session.RiderId == rider.RiderId)
                        .OrderByDescending(session => session.StartedAt)
                        .ToArray();

                    var completedSession = riderSessions.FirstOrDefault(session => session.StoppedAt is not null);
                    var activeSession = riderSessions.FirstOrDefault(session => session.StoppedAt is null);

                    var selectedSession = completedSession ?? activeSession;
                    var elapsedSeconds = completedSession is null
                        ? null
                        : (long?)Math.Max(0, (completedSession.StoppedAt!.Value - completedSession.StartedAt).TotalSeconds);

                    var status = completedSession is not null
                        ? "Finished"
                        : activeSession is not null
                            ? "Running"
                            : "Not started";

                    return new FinishedTimeReportRowDto(
                        rider.RiderId,
                        rider.BibNumber,
                        rider.FullName,
                        rider.Category,
                        rider.RouteType is null ? null : FormatRouteLabel(rider.RouteType),
                        selectedSession?.StartedAt,
                        completedSession?.StoppedAt,
                        elapsedSeconds,
                        status);
                })
                .ToArray();

            return reportRows;
        }
    }

    public TimingCommandResult StartTiming(TimingCommandRequest request)
    {
        lock (syncRoot)
        {
            using var dbContext = dbContextFactory.CreateDbContext();

            var validation = ValidateRequest(dbContext, request);
            if (validation is not null)
            {
                return new TimingCommandResult(false, validation, CreateSnapshot(dbContext));
            }

            var rider = dbContext.Riders.First(riderItem => riderItem.RiderId == request.RiderId);
            var existingSession = timingSessions.LastOrDefault(session => session.RiderId == rider.RiderId && session.StoppedAt is null);
            if (existingSession is not null)
            {
                return new TimingCommandResult(false, $"{rider.FullName} is already running.", CreateSnapshot(dbContext));
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

            return new TimingCommandResult(true, $"Started timing for {rider.FullName}.", CreateSnapshot(dbContext));
        }
    }

    public TimingCommandResult StopTiming(TimingCommandRequest request)
    {
        lock (syncRoot)
        {
            using var dbContext = dbContextFactory.CreateDbContext();

            var validation = ValidateRequest(dbContext, request);
            if (validation is not null)
            {
                return new TimingCommandResult(false, validation, CreateSnapshot(dbContext));
            }

            var rider = dbContext.Riders.First(riderItem => riderItem.RiderId == request.RiderId);
            var sessionIndex = timingSessions.FindLastIndex(session => session.RiderId == rider.RiderId && session.StoppedAt is null);
            if (sessionIndex < 0)
            {
                return new TimingCommandResult(false, $"No active timing session exists for {rider.FullName}.", CreateSnapshot(dbContext));
            }

            var currentSession = timingSessions[sessionIndex];
            timingSessions[sessionIndex] = currentSession with { StoppedAt = DateTimeOffset.UtcNow };
            UpdateAuditTrail(request.OfficialName);

            return new TimingCommandResult(true, $"Stopped timing for {rider.FullName}.", CreateSnapshot(dbContext));
        }
    }

    public ImportResults ImportParticipants(IEnumerable<RiderImportDto> imports)
    {
        lock (syncRoot)
        {
            using var dbContext = dbContextFactory.CreateDbContext();

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

                var bibNumber = dto.BibNumber.Trim();
                if (dbContext.Riders.Any(rider => rider.BibNumber == bibNumber))
                {
                    errors.Add($"Skipped bib #{dto.BibNumber} ({dto.FullName}): bib number already exists.");
                    skippedCount++;
                    continue;
                }

                var routeType = ResolveRouteType(dbContext, dto.AssignedRoute);

                dbContext.Riders.Add(new RiderEntity
                {
                    RiderId = string.IsNullOrWhiteSpace(dto.Id) ? $"rider-{Guid.NewGuid():N}" : dto.Id.Trim(),
                    BibNumber = bibNumber,
                    FullName = dto.FullName.Trim(),
                    Category = dto.Category.Trim(),
                    RouteTypeId = routeType?.RouteTypeId,
                    UpdatedAt = DateTimeOffset.UtcNow
                });

                successCount++;
            }

            if (successCount > 0)
            {
                dbContext.SaveChanges();
                UpdateAuditTrail("Import");
            }

            return new ImportResults(successCount, skippedCount, errors);
        }
    }

    public (EventSnapshot Snapshot, string? Error) AddParticipant(RiderImportDto dto)
    {
        lock (syncRoot)
        {
            using var dbContext = dbContextFactory.CreateDbContext();

            if (string.IsNullOrWhiteSpace(dto.BibNumber) ||
                string.IsNullOrWhiteSpace(dto.FullName) ||
                string.IsNullOrWhiteSpace(dto.Category))
            {
                return (CreateSnapshot(dbContext), "BibNumber, FullName and Category are required.");
            }

            var bibNumber = dto.BibNumber.Trim();
            if (dbContext.Riders.Any(rider => rider.BibNumber == bibNumber))
            {
                return (CreateSnapshot(dbContext), $"Bib #{dto.BibNumber} is already registered.");
            }

            var routeType = ResolveRouteType(dbContext, dto.AssignedRoute);

            dbContext.Riders.Add(new RiderEntity
            {
                RiderId = string.IsNullOrWhiteSpace(dto.Id) ? $"rider-{Guid.NewGuid():N}" : dto.Id.Trim(),
                BibNumber = bibNumber,
                FullName = dto.FullName.Trim(),
                Category = dto.Category.Trim(),
                RouteTypeId = routeType?.RouteTypeId,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            dbContext.SaveChanges();
            UpdateAuditTrail("On-the-day add");
            return (CreateSnapshot(dbContext), null);
        }
    }

    public (EventSnapshot Snapshot, string? Error) UpdateRiderRoute(string riderId, string route)
    {
        lock (syncRoot)
        {
            using var dbContext = dbContextFactory.CreateDbContext();

            var rider = dbContext.Riders.FirstOrDefault(item => item.RiderId == riderId);
            if (rider is null)
            {
                return (CreateSnapshot(dbContext), "Rider not found.");
            }

            var routeType = ResolveRouteType(dbContext, route);
            rider.RouteTypeId = routeType?.RouteTypeId;
            rider.UpdatedAt = DateTimeOffset.UtcNow;

            dbContext.SaveChanges();
            UpdateAuditTrail("Route update");
            return (CreateSnapshot(dbContext), null);
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

            using var dbContext = dbContextFactory.CreateDbContext();
            var riderExists = dbContext.Riders.Any(item => item.RiderId == riderId);
            if (!riderExists)
            {
                return (null, "Rider not found.");
            }

            var riderList = waveRiders[waveId];
            if (!riderList.Contains(riderId))
            {
                riderList.Add(riderId);
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

            using var dbContext = dbContextFactory.CreateDbContext();

            var waveName = waveNames[waveId];
            var startTime = DateTimeOffset.UtcNow;
            var startedCount = 0;
            var skipped = new List<string>();

            foreach (var riderId in riderIds)
            {
                var rider = dbContext.Riders.FirstOrDefault(item => item.RiderId == riderId);
                if (rider is null)
                {
                    continue;
                }

                var existingSession = timingSessions.LastOrDefault(session => session.RiderId == riderId && session.StoppedAt is null);
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

    public IReadOnlyList<RiderManagementDto> GetRiders()
    {
        lock (syncRoot)
        {
            using var dbContext = dbContextFactory.CreateDbContext();
            return dbContext.Riders
                .AsNoTracking()
                .Include(item => item.RouteType)
                .OrderBy(item => item.BibNumber)
                .Select(item => new RiderManagementDto(
                    item.RiderId,
                    item.BibNumber,
                    item.FullName,
                    item.Category,
                    item.RouteTypeId,
                    item.RouteType != null ? FormatRouteLabel(item.RouteType) : null,
                    item.Email,
                    item.Phone))
                .ToArray();
        }
    }

    public RiderManagementDto? GetRider(string riderId)
    {
        lock (syncRoot)
        {
            using var dbContext = dbContextFactory.CreateDbContext();
            return dbContext.Riders
                .AsNoTracking()
                .Include(item => item.RouteType)
                .Where(item => item.RiderId == riderId)
                .Select(item => new RiderManagementDto(
                    item.RiderId,
                    item.BibNumber,
                    item.FullName,
                    item.Category,
                    item.RouteTypeId,
                    item.RouteType != null ? FormatRouteLabel(item.RouteType) : null,
                    item.Email,
                    item.Phone))
                .FirstOrDefault();
        }
    }

    public (RiderManagementDto? Rider, string? Error) CreateRider(RiderCreateRequest request)
    {
        lock (syncRoot)
        {
            using var dbContext = dbContextFactory.CreateDbContext();

            var error = ValidateRiderInput(dbContext, request.BibNumber, request.FullName, request.Category, request.RouteTypeId, null);
            if (error is not null)
            {
                return (null, error);
            }

            var rider = new RiderEntity
            {
                RiderId = $"rider-{Guid.NewGuid():N}",
                BibNumber = request.BibNumber.Trim(),
                FullName = request.FullName.Trim(),
                Category = request.Category.Trim(),
                RouteTypeId = NormalizeRouteTypeId(request.RouteTypeId),
                Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
                UpdatedAt = DateTimeOffset.UtcNow
            };

            dbContext.Riders.Add(rider);
            dbContext.SaveChanges();
            UpdateAuditTrail("Rider management");

            return (GetRider(rider.RiderId), null);
        }
    }

    public (RiderManagementDto? Rider, string? Error) UpdateRider(string riderId, RiderUpdateRequest request)
    {
        lock (syncRoot)
        {
            using var dbContext = dbContextFactory.CreateDbContext();

            var rider = dbContext.Riders.FirstOrDefault(item => item.RiderId == riderId);
            if (rider is null)
            {
                return (null, "Rider not found.");
            }

            var error = ValidateRiderInput(dbContext, request.BibNumber, request.FullName, request.Category, request.RouteTypeId, riderId);
            if (error is not null)
            {
                return (null, error);
            }

            rider.BibNumber = request.BibNumber.Trim();
            rider.FullName = request.FullName.Trim();
            rider.Category = request.Category.Trim();
            rider.RouteTypeId = NormalizeRouteTypeId(request.RouteTypeId);
            rider.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
            rider.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
            rider.UpdatedAt = DateTimeOffset.UtcNow;

            dbContext.SaveChanges();
            UpdateAuditTrail("Rider management");

            return (GetRider(riderId), null);
        }
    }

    public string? DeleteRider(string riderId)
    {
        lock (syncRoot)
        {
            using var dbContext = dbContextFactory.CreateDbContext();

            var rider = dbContext.Riders.FirstOrDefault(item => item.RiderId == riderId);
            if (rider is null)
            {
                return "Rider not found.";
            }

            dbContext.Riders.Remove(rider);
            dbContext.SaveChanges();

            foreach (var riderList in waveRiders.Values)
            {
                riderList.RemoveAll(item => item == riderId);
            }

            timingSessions.RemoveAll(session => session.RiderId == riderId);

            UpdateAuditTrail("Rider management");
            return null;
        }
    }

    public IReadOnlyList<RouteTypeDto> GetRouteTypes()
    {
        lock (syncRoot)
        {
            using var dbContext = dbContextFactory.CreateDbContext();
            return dbContext.RouteTypes
                .AsNoTracking()
                .OrderBy(item => item.DistanceMiles)
                .ThenBy(item => item.Name)
                .Select(item => new RouteTypeDto(item.RouteTypeId, item.Name, item.DistanceMiles, item.IsActive))
                .ToArray();
        }
    }

    public (RouteTypeDto? RouteType, string? Error) CreateRouteType(RouteTypeCreateRequest request)
    {
        lock (syncRoot)
        {
            using var dbContext = dbContextFactory.CreateDbContext();

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return (null, "Route type name is required.");
            }

            if (request.DistanceMiles <= 0)
            {
                return (null, "Distance miles must be greater than zero.");
            }

            if (dbContext.RouteTypes.Any(item => item.Name == request.Name.Trim()))
            {
                return (null, "A route type with this name already exists.");
            }

            var routeType = new RouteTypeEntity
            {
                RouteTypeId = $"route-{Guid.NewGuid():N}",
                Name = request.Name.Trim(),
                DistanceMiles = request.DistanceMiles,
                IsActive = true
            };

            dbContext.RouteTypes.Add(routeType);
            dbContext.SaveChanges();
            UpdateAuditTrail("Route type management");

            return (new RouteTypeDto(routeType.RouteTypeId, routeType.Name, routeType.DistanceMiles, routeType.IsActive), null);
        }
    }

    public (RouteTypeDto? RouteType, string? Error) UpdateRouteType(string routeTypeId, RouteTypeUpdateRequest request)
    {
        lock (syncRoot)
        {
            using var dbContext = dbContextFactory.CreateDbContext();

            var routeType = dbContext.RouteTypes.FirstOrDefault(item => item.RouteTypeId == routeTypeId);
            if (routeType is null)
            {
                return (null, "Route type not found.");
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return (null, "Route type name is required.");
            }

            if (request.DistanceMiles <= 0)
            {
                return (null, "Distance miles must be greater than zero.");
            }

            var normalizedName = request.Name.Trim();
            if (dbContext.RouteTypes.Any(item => item.RouteTypeId != routeTypeId && item.Name == normalizedName))
            {
                return (null, "A route type with this name already exists.");
            }

            routeType.Name = normalizedName;
            routeType.DistanceMiles = request.DistanceMiles;
            routeType.IsActive = request.IsActive;

            dbContext.SaveChanges();
            UpdateAuditTrail("Route type management");

            return (new RouteTypeDto(routeType.RouteTypeId, routeType.Name, routeType.DistanceMiles, routeType.IsActive), null);
        }
    }

    public string? DeleteRouteType(string routeTypeId)
    {
        lock (syncRoot)
        {
            using var dbContext = dbContextFactory.CreateDbContext();

            var routeType = dbContext.RouteTypes.FirstOrDefault(item => item.RouteTypeId == routeTypeId);
            if (routeType is null)
            {
                return "Route type not found.";
            }

            var hasRiders = dbContext.Riders.Any(item => item.RouteTypeId == routeTypeId);
            if (hasRiders)
            {
                return "Cannot delete this route type because riders are assigned to it.";
            }

            dbContext.RouteTypes.Remove(routeType);
            dbContext.SaveChanges();
            UpdateAuditTrail("Route type management");
            return null;
        }
    }

    public IReadOnlyList<OfficialDto> GetOfficials()
    {
        lock (syncRoot)
        {
            using var dbContext = dbContextFactory.CreateDbContext();
            return dbContext.Officials
                .AsNoTracking()
                .OrderBy(item => item.FullName)
                .Select(item => new OfficialDto(item.OfficialId, item.FullName, item.IsActive, item.UpdatedAt))
                .ToArray();
        }
    }

    public (OfficialDto? Official, string? Error) CreateOfficial(OfficialCreateRequest request)
    {
        lock (syncRoot)
        {
            using var dbContext = dbContextFactory.CreateDbContext();

            if (string.IsNullOrWhiteSpace(request.FullName))
            {
                return (null, "Official name is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pin) || request.Pin.Trim().Length < 4)
            {
                return (null, "Official PIN must be at least 4 digits.");
            }

            var fullName = request.FullName.Trim();
            if (dbContext.Officials.Any(item => item.FullName == fullName))
            {
                return (null, "An official with this name already exists.");
            }

            var (hash, salt) = PinHasher.HashPin(request.Pin.Trim());
            var official = new OfficialEntity
            {
                OfficialId = $"official-{Guid.NewGuid():N}",
                FullName = fullName,
                PinHash = hash,
                PinSalt = salt,
                IsActive = true,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            dbContext.Officials.Add(official);
            dbContext.SaveChanges();
            UpdateAuditTrail("Official management");

            return (new OfficialDto(official.OfficialId, official.FullName, official.IsActive, official.UpdatedAt), null);
        }
    }

    public (OfficialDto? Official, string? Error) UpdateOfficial(string officialId, OfficialUpdateRequest request)
    {
        lock (syncRoot)
        {
            using var dbContext = dbContextFactory.CreateDbContext();

            var official = dbContext.Officials.FirstOrDefault(item => item.OfficialId == officialId);
            if (official is null)
            {
                return (null, "Official not found.");
            }

            if (string.IsNullOrWhiteSpace(request.FullName))
            {
                return (null, "Official name is required.");
            }

            var fullName = request.FullName.Trim();
            if (dbContext.Officials.Any(item => item.OfficialId != officialId && item.FullName == fullName))
            {
                return (null, "An official with this name already exists.");
            }

            official.FullName = fullName;
            official.IsActive = request.IsActive;

            if (!string.IsNullOrWhiteSpace(request.Pin))
            {
                if (request.Pin.Trim().Length < 4)
                {
                    return (null, "Official PIN must be at least 4 digits.");
                }

                var (hash, salt) = PinHasher.HashPin(request.Pin.Trim());
                official.PinHash = hash;
                official.PinSalt = salt;
            }

            official.UpdatedAt = DateTimeOffset.UtcNow;
            dbContext.SaveChanges();
            UpdateAuditTrail("Official management");

            return (new OfficialDto(official.OfficialId, official.FullName, official.IsActive, official.UpdatedAt), null);
        }
    }

    public string? DeleteOfficial(string officialId)
    {
        lock (syncRoot)
        {
            using var dbContext = dbContextFactory.CreateDbContext();

            var official = dbContext.Officials.FirstOrDefault(item => item.OfficialId == officialId);
            if (official is null)
            {
                return "Official not found.";
            }

            dbContext.Officials.Remove(official);
            dbContext.SaveChanges();
            UpdateAuditTrail("Official management");
            return null;
        }
    }

    private void EnsureDatabaseInitialized()
    {
        lock (syncRoot)
        {
            using var dbContext = dbContextFactory.CreateDbContext();
            dbContext.Database.EnsureCreated();
            ApplySchemaUpdates(dbContext);

            if (!dbContext.RouteTypes.Any())
            {
                dbContext.RouteTypes.AddRange(
                    new RouteTypeEntity { RouteTypeId = "route-100", Name = "Kent Tiger Ride", DistanceMiles = 100, IsActive = true },
                    new RouteTypeEntity { RouteTypeId = "route-57", Name = "Cruiser Classic", DistanceMiles = 57, IsActive = true });
            }

            if (!dbContext.Riders.Any())
            {
                dbContext.Riders.AddRange(
                    new RiderEntity { RiderId = "rider-001", BibNumber = "101", FullName = "Ava Mitchell", Category = "Women Open", RouteTypeId = "route-100", UpdatedAt = DateTimeOffset.UtcNow },
                    new RiderEntity { RiderId = "rider-002", BibNumber = "204", FullName = "Ben Carter", Category = "Men 40+", RouteTypeId = "route-57", UpdatedAt = DateTimeOffset.UtcNow },
                    new RiderEntity { RiderId = "rider-003", BibNumber = "317", FullName = "Chloe King", Category = "Junior", RouteTypeId = "route-57", UpdatedAt = DateTimeOffset.UtcNow },
                    new RiderEntity { RiderId = "rider-004", BibNumber = "422", FullName = "Daniel Evans", Category = "Men Open", RouteTypeId = "route-100", UpdatedAt = DateTimeOffset.UtcNow });
            }

            if (!dbContext.Officials.Any())
            {
                var (markHash, markSalt) = PinHasher.HashPin("2468");
                var (lornaHash, lornaSalt) = PinHasher.HashPin("1357");

                dbContext.Officials.AddRange(
                    new OfficialEntity
                    {
                        OfficialId = "official-001",
                        FullName = "Mark Phillipson",
                        PinHash = markHash,
                        PinSalt = markSalt,
                        IsActive = true,
                        UpdatedAt = DateTimeOffset.UtcNow
                    },
                    new OfficialEntity
                    {
                        OfficialId = "official-002",
                        FullName = "Lorna Stafford",
                        PinHash = lornaHash,
                        PinSalt = lornaSalt,
                        IsActive = true,
                        UpdatedAt = DateTimeOffset.UtcNow
                    });
            }

            dbContext.SaveChanges();
        }
    }

    private RouteTypeEntity? ResolveRouteType(EventTimingsDbContext dbContext, string? routeLabel)
    {
        if (string.IsNullOrWhiteSpace(routeLabel))
        {
            return null;
        }

        var trimmed = routeLabel.Trim();

        var existingRouteById = dbContext.RouteTypes.FirstOrDefault(item => item.RouteTypeId == trimmed);
        if (existingRouteById is not null)
        {
            return existingRouteById;
        }

        var existingRouteByName = dbContext.RouteTypes.FirstOrDefault(item => item.Name == trimmed);
        if (existingRouteByName is not null)
        {
            return existingRouteByName;
        }

        if (int.TryParse(new string(trimmed.Where(char.IsDigit).ToArray()), out var distanceMiles) && distanceMiles > 0)
        {
            var createdRoute = new RouteTypeEntity
            {
                RouteTypeId = $"route-{Guid.NewGuid():N}",
                Name = $"{distanceMiles} mile route",
                DistanceMiles = distanceMiles,
                IsActive = true
            };
            dbContext.RouteTypes.Add(createdRoute);
            dbContext.SaveChanges();
            return createdRoute;
        }

        return null;
    }

    private string? ValidateRequest(EventTimingsDbContext dbContext, TimingCommandRequest request)
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

        var officialName = request.OfficialName.Trim();
        var official = dbContext.Officials.FirstOrDefault(item => item.FullName == officialName && item.IsActive);
        if (official is null || !PinHasher.VerifyPin(request.Pin.Trim(), official.PinHash, official.PinSalt))
        {
            return "The official name or PIN is not recognized.";
        }

        if (!dbContext.Riders.Any(rider => rider.RiderId == request.RiderId))
        {
            return "Choose a rider before starting or stopping timing.";
        }

        return null;
    }

    private static string? ValidateRiderInput(
        EventTimingsDbContext dbContext,
        string bibNumber,
        string fullName,
        string category,
        string? routeTypeId,
        string? riderId)
    {
        if (string.IsNullOrWhiteSpace(bibNumber) || string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(category))
        {
            return "Bib number, full name, and category are required.";
        }

        var normalizedBib = bibNumber.Trim();
        var duplicateBibExists = dbContext.Riders.Any(item => item.BibNumber == normalizedBib && item.RiderId != riderId);
        if (duplicateBibExists)
        {
            return "Bib number is already in use.";
        }

        var normalizedRouteTypeId = NormalizeRouteTypeId(routeTypeId);
        if (normalizedRouteTypeId is not null && !dbContext.RouteTypes.Any(item => item.RouteTypeId == normalizedRouteTypeId))
        {
            return "Selected route type was not found.";
        }

        return null;
    }

    private static void ApplySchemaUpdates(EventTimingsDbContext dbContext)
    {
        // Add new nullable columns to the Riders table; silently ignored if they already exist.
        try { dbContext.Database.ExecuteSqlRaw("ALTER TABLE Riders ADD COLUMN Email TEXT"); } catch { }
        try { dbContext.Database.ExecuteSqlRaw("ALTER TABLE Riders ADD COLUMN Phone TEXT"); } catch { }
    }

    public ImportResults ImportRiderContacts(IEnumerable<RiderContactImportDto> contacts)
    {
        lock (syncRoot)
        {
            using var dbContext = dbContextFactory.CreateDbContext();

            var errors = new List<string>();
            var successCount = 0;
            var skippedCount = 0;
            var nextBib = (dbContext.Riders.Any()
                ? dbContext.Riders
                    .AsEnumerable()
                    .Select(r => int.TryParse(r.BibNumber, out var n) ? n : 0)
                    .DefaultIfEmpty(0)
                    .Max()
                : 0) + 1;

            foreach (var contact in contacts)
            {
                if (string.IsNullOrWhiteSpace(contact.FullName))
                {
                    errors.Add("Skipped entry with empty name.");
                    skippedCount++;
                    continue;
                }

                var normalizedName = contact.FullName.Trim();
                if (dbContext.Riders.Any(r => r.FullName == normalizedName))
                {
                    errors.Add($"Skipped '{normalizedName}': rider with this name already exists.");
                    skippedCount++;
                    continue;
                }

                dbContext.Riders.Add(new RiderEntity
                {
                    RiderId = $"rider-{Guid.NewGuid():N}",
                    BibNumber = nextBib.ToString("D3"),
                    FullName = normalizedName,
                    Category = "Open",
                    Email = string.IsNullOrWhiteSpace(contact.Email) ? null : contact.Email.Trim(),
                    Phone = string.IsNullOrWhiteSpace(contact.Phone) ? null : contact.Phone.Trim(),
                    UpdatedAt = DateTimeOffset.UtcNow
                });

                nextBib++;
                successCount++;
            }

            if (successCount > 0)
            {
                dbContext.SaveChanges();
                UpdateAuditTrail("Contact import");
            }

            return new ImportResults(successCount, skippedCount, errors);
        }
    }

    private static string? NormalizeRouteTypeId(string? routeTypeId)
    {
        return string.IsNullOrWhiteSpace(routeTypeId) ? null : routeTypeId.Trim();
    }

    private void UpdateAuditTrail(string officialName)
    {
        lastUpdatedBy = officialName;
        lastUpdatedAt = DateTimeOffset.UtcNow;
    }

    public PagedTimingSessionsDto GetTimingSessionsPaged(int page = 0, int pageSize = 20)
    {
        lock (syncRoot)
        {
            var orderedSessions = timingSessions
                .OrderByDescending(session => session.StartedAt)
                .ToArray();

            var totalCount = orderedSessions.Length;
            var items = orderedSessions
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToArray();

            return new PagedTimingSessionsDto(items, totalCount, page, pageSize);
        }
    }

    private EventSnapshot CreateSnapshot(EventTimingsDbContext dbContext)
    {
        var status = timingSessions.Any(session => session.StoppedAt is null) ? "Live" : "Ready";

        var riders = dbContext.Riders
            .AsNoTracking()
            .Include(item => item.RouteType)
            .OrderBy(item => item.BibNumber)
            .ToArray()
            .Select(item => new RiderSummary(
                item.RiderId,
                item.BibNumber,
                item.FullName,
                item.Category,
                item.RouteType != null ? FormatRouteLabel(item.RouteType) : null,
                GetWaveIdForRider(item.RiderId),
                GetWaveNameForRider(item.RiderId)))
            .ToArray();

        return new EventSnapshot(
            EventCode,
            "SFA Sportive 2026",
            "Marden Railway Station, TN12 9DR",
            new DateOnly(2026, 5, 24),
            status,
            riders,
            timingSessions.OrderByDescending(session => session.StartedAt).ToArray(),
            lastUpdatedBy,
            lastUpdatedAt);
    }

    private static string FormatRouteLabel(RouteTypeEntity routeType)
    {
        return routeType.DistanceMiles > 0
            ? $"{routeType.Name} ({routeType.DistanceMiles} miles)"
            : routeType.Name;
    }

    private string? GetWaveIdForRider(string riderId)
    {
        return waveRiders.FirstOrDefault(item => item.Value.Contains(riderId)).Key;
    }

    private string? GetWaveNameForRider(string riderId)
    {
        var waveId = GetWaveIdForRider(riderId);
        if (waveId is null)
        {
            return null;
        }

        return waveNames.TryGetValue(waveId, out var waveName) ? waveName : null;
    }
}
