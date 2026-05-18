using System.Net.Http.Json;
using System.Text.Json;
using EventTimings.Contracts;
using Microsoft.Extensions.Configuration;

namespace EventTimings.App.Services;

public sealed class EventTimingApiClient(IConfiguration configuration)
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
            response.EnsureSuccessStatusCode();
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
            catch (JsonException)
            {
                continue;
            }
            catch (NotSupportedException)
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
        var candidateUrls = configuration
            .GetSection("ApiBaseUrls")
            .GetChildren()
            .Select(item => item.Value)
            .Append(configuration["ApiBaseUrl"]);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidateUrl in candidateUrls)
        {
            if (string.IsNullOrWhiteSpace(candidateUrl))
            {
                continue;
            }

            var normalizedUrl = candidateUrl.EndsWith("/", StringComparison.Ordinal) ? candidateUrl : $"{candidateUrl}/";

            if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var parsedUri))
            {
                continue;
            }

            if (seen.Add(parsedUri.AbsoluteUri))
            {
                yield return parsedUri;
            }
        }
    }
}