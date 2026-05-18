using EventTimings.Api;
using EventTimings.Api.Data;
using EventTimings.Contracts;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("client", policy => policy
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowAnyOrigin());
});
builder.Services.AddDbContextFactory<EventTimingsDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("EventTimingsDb") ?? "Data Source=eventtimings.db";
    options.UseSqlite(connectionString);
});
builder.Services.AddSingleton<TimingStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("client");

app.MapGet("/api/event/current", (TimingStore store) => Results.Ok(store.GetSnapshot()))
    .WithName("GetCurrentEvent");

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

app.Run();
