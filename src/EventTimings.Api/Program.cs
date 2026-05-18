using EventTimings.Api;
using EventTimings.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("client", policy => policy
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowAnyOrigin());
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

app.Run();
