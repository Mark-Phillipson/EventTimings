using System.Net.Http.Json;
using EventTimings.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;

namespace EventTimings.App.Services;

public sealed class EventTimingApiClient(NavigationManager navigationManager, IConfiguration configuration)
{
    public Task<EventSnapshot?> GetCurrentEventAsync(CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(client => client.GetFromJsonAsync<EventSnapshot>("api/event/current", cancellationToken), cancellationToken);

    public Task<TimingCommandResult?> StartTimingAsync(TimingCommandRequest request, CancellationToken cancellationToken = default) =>
        PostTimingCommandAsync("api/event/timing/start", request, cancellationToken);

    public Task<TimingCommandResult?> StopTimingAsync(TimingCommandRequest request, CancellationToken cancellationToken = default) =>
        PostTimingCommandAsync("api/event/timing/stop", request, cancellationToken);

    private Task<TimingCommandResult?> PostTimingCommandAsync(string route, TimingCommandRequest request, CancellationToken cancellationToken) =>
        SendWithFallbackAsync(async client =>
        {
            using var response = await client.PostAsJsonAsync(route, request, cancellationToken);
            return await response.Content.ReadFromJsonAsync<TimingCommandResult>(cancellationToken);
        }, cancellationToken);

    private async Task<T?> SendWithFallbackAsync<T>(Func<HttpClient, Task<T?>> sendAsync, CancellationToken cancellationToken)
    {
        foreach (var baseAddress in GetCandidateBaseAddresses())
        {
            using var httpClient = new HttpClient { BaseAddress = baseAddress };

            try
            {
                var result = await sendAsync(httpClient);
                if (result is not null)
                {
                    return result;
                }
            }
            catch (HttpRequestException)
            {
                continue;
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException)
            {
                continue;
            }
        }

        return default;
    }

    private IEnumerable<Uri> GetCandidateBaseAddresses()
    {
        var candidateUrls = new[]
        {
            navigationManager.BaseUri,
            configuration["ApiBaseUrl"],
            "http://localhost:5038/",
            "https://localhost:7290/"
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidateUrl in candidateUrls)
        {
            if (string.IsNullOrWhiteSpace(candidateUrl))
            {
                continue;
            }

            if (seen.Add(candidateUrl))
            {
                yield return new Uri(candidateUrl, UriKind.Absolute);
            }
        }
    }
}