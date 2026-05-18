using System.Net.Http.Json;
using EventTimings.Contracts;

namespace EventTimings.App.Services;

public sealed class EventTimingApiClient(HttpClient httpClient)
{
    public Task<EventSnapshot?> GetCurrentEventAsync(CancellationToken cancellationToken = default) =>
        httpClient.GetFromJsonAsync<EventSnapshot>("api/event/current", cancellationToken);

    public Task<TimingCommandResult?> StartTimingAsync(TimingCommandRequest request, CancellationToken cancellationToken = default) =>
        PostTimingCommandAsync("api/event/timing/start", request, cancellationToken);

    public Task<TimingCommandResult?> StopTimingAsync(TimingCommandRequest request, CancellationToken cancellationToken = default) =>
        PostTimingCommandAsync("api/event/timing/stop", request, cancellationToken);

    private async Task<TimingCommandResult?> PostTimingCommandAsync(string route, TimingCommandRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(route, request, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TimingCommandResult>(cancellationToken);
    }
}