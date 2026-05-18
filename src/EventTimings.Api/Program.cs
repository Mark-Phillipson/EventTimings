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

app.Run();
