using EventTimings.Api;
using EventTimings.Api.Data;
using EventTimings.Contracts;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("client", policy => policy
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowAnyOrigin());
});
builder.Services.AddDbContextFactory<EventTimingsDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("EventTimingsDb");
    var configuredProvider = builder.Configuration["DatabaseProvider"];
    var useSqlServer = string.Equals(configuredProvider, "SqlServer", StringComparison.OrdinalIgnoreCase)
        || (!string.IsNullOrWhiteSpace(connectionString)
            && (connectionString.Contains("Server=tcp:", StringComparison.OrdinalIgnoreCase)
                || connectionString.Contains("Authentication=", StringComparison.OrdinalIgnoreCase)));

    if (!useSqlServer)
    {
        if (string.IsNullOrWhiteSpace(connectionString)
            || !connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            connectionString = "Data Source=eventtimings.db";
        }

        options.UseSqlite(connectionString);
        return;
    }

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("The EventTimingsDb connection string must be configured when DatabaseProvider is SqlServer.");
    }

    options.UseSqlServer(connectionString, sqlOptions => sqlOptions.EnableRetryOnFailure());
});
builder.Services.AddSingleton<TimingStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders();
    app.UseHttpsRedirection();
}
app.UseRouting();
// Request/response logging: enabled for local debugging to help diagnose client fetches
app.Use(async (context, next) =>
{
    try
    {
        Console.WriteLine($"[REQ] {DateTimeOffset.Now:HH:mm:ss} {context.Request.Method} {context.Request.Path}{context.Request.QueryString}");
    }
    catch { }

    await next();

    try
    {
        Console.WriteLine($"[RES] {DateTimeOffset.Now:HH:mm:ss} {context.Response.StatusCode} for {context.Request.Method} {context.Request.Path}");
    }
    catch { }
});
app.UseCors("client");
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok("healthy"));

app.MapGet("/api/event/current", (TimingStore store) => Results.Ok(store.GetSnapshot()))
    .WithName("GetCurrentEvent");

app.MapGet("/api/event/timings", (int page, int pageSize, TimingStore store) =>
{
    var validatedPage = Math.Max(0, page);
    var validatedPageSize = Math.Max(1, Math.Min(pageSize, 100)); // Cap at 100 for safety
    return Results.Ok(store.GetTimingSessionsPaged(validatedPage, validatedPageSize));
})
    .WithName("GetTimingSessionsPaged");

app.MapGet("/api/reports/finished-times", (TimingStore store) =>
    Results.Ok(store.GetFinishedTimeReport()))
    .WithName("GetFinishedTimesReport");

app.MapGet("/api/reports/finished-times.csv", (TimingStore store) =>
{
    var csv = BuildFinishedTimesCsv(store.GetFinishedTimeReport());
    return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", "finished-times-report.csv");
})
    .WithName("ExportFinishedTimesCsv");

app.MapPost("/api/event/timing/start", (TimingCommandRequest request, TimingStore store) =>
{
    var result = store.StartTiming(request);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
})
    .WithName("StartTiming");

app.MapPost("/api/event/timing/stop", (TimingCommandRequest request, TimingStore store) =>
{
    var result = store.StopTiming(request);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
})
    .WithName("StopTiming");

app.MapPost("/api/event/participants/import", (IEnumerable<RiderImportDto> participants, TimingStore store) =>
    Results.Ok(store.ImportParticipants(participants)))
    .WithName("ImportParticipants");

app.MapPost("/api/event/participants/add", (RiderImportDto participant, TimingStore store) =>
{
    var (snapshot, error) = store.AddParticipant(participant);
    return error is null ? Results.Ok(snapshot) : Results.BadRequest(new { error });
})
    .WithName("AddParticipant");

app.MapPatch("/api/event/participants/{riderId}/route", (string riderId, UpdateRouteRequest request, TimingStore store) =>
{
    var (snapshot, error) = store.UpdateRiderRoute(riderId, request.Route);
    return error is null ? Results.Ok(snapshot) : Results.NotFound(new { error });
})
    .WithName("UpdateRiderRoute");

app.MapGet("/api/event/waves", (TimingStore store) =>
    Results.Ok(store.GetWaves()))
    .WithName("GetWaves");

app.MapPost("/api/event/waves", (WaveCreateRequest request, TimingStore store) =>
{
    var wave = store.CreateWave(request.WaveName);
    return Results.Created($"/api/event/waves/{wave.WaveId}", wave);
})
    .WithName("CreateWave");

app.MapPut("/api/event/waves/{waveId}/riders", (string waveId, WaveAssignRiderRequest request, TimingStore store) =>
{
    var (wave, error) = store.AssignRiderToWave(waveId, request.RiderId);
    return error is null ? Results.Ok(wave) : Results.NotFound(new { error });
})
    .WithName("AssignRiderToWave");

app.MapPost("/api/event/waves/{waveId}/start", (string waveId, TimingStore store) =>
{
    var (result, error) = store.StartWave(waveId);
    return error is null ? Results.Ok(result) : Results.NotFound(new { error });
})
    .WithName("StartWave");

app.MapGet("/api/admin/riders", (TimingStore store) =>
    Results.Ok(store.GetRiders()))
    .WithName("GetRiders");

app.MapGet("/api/admin/riders/{riderId}", (string riderId, TimingStore store) =>
{
    var rider = store.GetRider(riderId);
    return rider is null ? Results.NotFound(new { error = "Rider not found." }) : Results.Ok(rider);
})
    .WithName("GetRider");

app.MapPost("/api/admin/riders", (RiderCreateRequest request, TimingStore store) =>
{
    var (rider, error) = store.CreateRider(request);
    return error is null
        ? Results.Created($"/api/admin/riders/{rider!.RiderId}", rider)
        : Results.BadRequest(new { error });
})
    .WithName("CreateRider");

app.MapPut("/api/admin/riders/{riderId}", (string riderId, RiderUpdateRequest request, TimingStore store) =>
{
    var (rider, error) = store.UpdateRider(riderId, request);
    if (error is null)
    {
        return Results.Ok(rider);
    }

    return error == "Rider not found."
        ? Results.NotFound(new { error })
        : Results.BadRequest(new { error });
})
    .WithName("UpdateRider");

app.MapDelete("/api/admin/riders/{riderId}", (string riderId, TimingStore store) =>
{
    var error = store.DeleteRider(riderId);
    return error is null ? Results.NoContent() : Results.NotFound(new { error });
})
    .WithName("DeleteRider");

app.MapPost("/api/admin/riders/import-contacts", (IEnumerable<RiderContactImportDto> contacts, TimingStore store) =>
    Results.Ok(store.ImportRiderContacts(contacts)))
    .WithName("ImportRiderContacts");

app.MapPost("/api/admin/riders/seed-contacts", (TimingStore store) =>
    Results.Ok(store.ImportRiderContacts(RiderContactsData.Contacts)))
    .WithName("SeedRiderContacts");

app.MapGet("/api/admin/route-types", (TimingStore store) =>
    Results.Ok(store.GetRouteTypes()))
    .WithName("GetRouteTypes");

app.MapPost("/api/admin/route-types", (RouteTypeCreateRequest request, TimingStore store) =>
{
    var (routeType, error) = store.CreateRouteType(request);
    return error is null
        ? Results.Created($"/api/admin/route-types/{routeType!.RouteTypeId}", routeType)
        : Results.BadRequest(new { error });
})
    .WithName("CreateRouteType");

app.MapPut("/api/admin/route-types/{routeTypeId}", (string routeTypeId, RouteTypeUpdateRequest request, TimingStore store) =>
{
    var (routeType, error) = store.UpdateRouteType(routeTypeId, request);
    if (error is null)
    {
        return Results.Ok(routeType);
    }

    return error == "Route type not found."
        ? Results.NotFound(new { error })
        : Results.BadRequest(new { error });
})
    .WithName("UpdateRouteType");

app.MapDelete("/api/admin/route-types/{routeTypeId}", (string routeTypeId, TimingStore store) =>
{
    var error = store.DeleteRouteType(routeTypeId);
    if (error is null)
    {
        return Results.NoContent();
    }

    return error == "Route type not found."
        ? Results.NotFound(new { error })
        : Results.BadRequest(new { error });
})
    .WithName("DeleteRouteType");

app.MapGet("/api/admin/officials", (TimingStore store) =>
    Results.Ok(store.GetOfficials()))
    .WithName("GetOfficials");

app.MapPost("/api/admin/officials", (OfficialCreateRequest request, TimingStore store) =>
{
    var (official, error) = store.CreateOfficial(request);
    return error is null
        ? Results.Created($"/api/admin/officials/{official!.OfficialId}", official)
        : Results.BadRequest(new { error });
})
    .WithName("CreateOfficial");

app.MapPut("/api/admin/officials/{officialId}", (string officialId, OfficialUpdateRequest request, TimingStore store) =>
{
    var (official, error) = store.UpdateOfficial(officialId, request);
    if (error is null)
    {
        return Results.Ok(official);
    }

    return error == "Official not found."
        ? Results.NotFound(new { error })
        : Results.BadRequest(new { error });
})
    .WithName("UpdateOfficial");

app.MapDelete("/api/admin/officials/{officialId}", (string officialId, TimingStore store) =>
{
    var error = store.DeleteOfficial(officialId);
    return error is null ? Results.NoContent() : Results.NotFound(new { error });
})
    .WithName("DeleteOfficial");

app.MapPost("/api/admin/officials/verify", (OfficialVerificationRequest request, TimingStore store) =>
{
    var (official, error) = store.VerifyOfficial(request);
    if (official is not null)
    {
        return Results.Ok(new OfficialVerificationResult(true, "Verified", official));
    }

    return Results.BadRequest(new OfficialVerificationResult(false, error ?? "Invalid credentials", null));
})
    .WithName("VerifyOfficial");

// Admin: reset/clear all timing sessions (requires official verification)
app.MapPost("/api/admin/timings/reset", (TimingCommandRequest request, TimingStore store) =>
{
    var result = store.ResetAllTimings(request);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
})
    .WithName("ResetAllTimings");

static string BuildFinishedTimesCsv(IReadOnlyList<FinishedTimeReportRowDto> rows)
{
    var builder = new StringBuilder();
    builder.AppendLine("BibNumber,FullName,Category,Route,StartedAtUtc,FinishedAtUtc,Elapsed,Status");

    foreach (var row in rows)
    {
        builder.AppendLine(string.Join(',',
            EscapeCsv(row.BibNumber),
            EscapeCsv(row.FullName),
            EscapeCsv(row.Category),
            EscapeCsv(row.RouteName ?? string.Empty),
            EscapeCsv(row.StartedAt?.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty),
            EscapeCsv(row.FinishedAt?.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty),
            EscapeCsv(FormatElapsed(row.ElapsedSeconds)),
            EscapeCsv(row.Status)));
    }

    return builder.ToString();
}

static string EscapeCsv(string value)
{
    if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    return value;
}

static string FormatElapsed(long? elapsedSeconds)
{
    if (elapsedSeconds is null)
    {
        return string.Empty;
    }

    return TimeSpan.FromSeconds(elapsedSeconds.Value).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
}

app.MapFallbackToFile("index.html");

// Ensure CORS preflight requests for API routes are handled even when
// the SPA fallback would otherwise match. This returns a 204 for
// any OPTIONS request under /api/* so the CORS middleware can add
// the appropriate headers.
app.MapMethods("/api/{**slug}", new[] { "OPTIONS" }, () => Results.NoContent()).WithName("ApiOptions");

app.Run();
