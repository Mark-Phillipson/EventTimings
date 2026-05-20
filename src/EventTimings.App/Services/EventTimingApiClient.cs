using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EventTimings.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;

namespace EventTimings.App.Services;

public sealed class EventTimingApiClient(IConfiguration configuration, NavigationManager navigationManager)
{
    private const int MaxAttemptsPerBaseAddress = 2;

    public Task<EventSnapshot?> GetCurrentEventAsync(CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(client => client.GetFromJsonAsync<EventSnapshot>("api/event/current", cancellationToken), cancellationToken);

    public Task<PagedTimingSessionsDto?> GetTimingSessionsPagedAsync(int page = 0, int pageSize = 20, CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(client => client.GetFromJsonAsync<PagedTimingSessionsDto>($"api/event/timings?page={page}&pageSize={pageSize}", cancellationToken), cancellationToken);

    public Task<TimingCommandResult?> StartTimingAsync(TimingCommandRequest request, CancellationToken cancellationToken = default) =>
        PostTimingCommandAsync("api/event/timing/start", request, cancellationToken);

    public Task<TimingCommandResult?> StopTimingAsync(TimingCommandRequest request, CancellationToken cancellationToken = default) =>
        PostTimingCommandAsync("api/event/timing/stop", request, cancellationToken);

    public Task<ImportResults?> ImportParticipantsAsync(IEnumerable<RiderImportDto> participants, CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(async client =>
        {
            using var response = await client.PostAsJsonAsync("api/event/participants/import", participants, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ImportResults>(cancellationToken);
        }, cancellationToken);

    public Task<EventSnapshot?> AddParticipantAsync(RiderImportDto participant, CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(async client =>
        {
            using var response = await client.PostAsJsonAsync("api/event/participants/add", participant, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<EventSnapshot>(cancellationToken);
        }, cancellationToken);

    public Task<EventSnapshot?> UpdateRiderRouteAsync(string riderId, string route, CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(async client =>
        {
            using var response = await client.PatchAsJsonAsync($"api/event/participants/{riderId}/route", new UpdateRouteRequest(route), cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<EventSnapshot>(cancellationToken);
        }, cancellationToken);

    public Task<IReadOnlyList<WaveDto>?> GetWavesAsync(CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(client => client.GetFromJsonAsync<IReadOnlyList<WaveDto>>("api/event/waves", cancellationToken), cancellationToken);

    public Task<WaveDto?> CreateWaveAsync(WaveCreateRequest request, CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(async client =>
        {
            using var response = await client.PostAsJsonAsync("api/event/waves", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<WaveDto>(cancellationToken);
        }, cancellationToken);

    public Task<WaveDto?> AssignRiderToWaveAsync(string waveId, WaveAssignRiderRequest request, CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(async client =>
        {
            using var response = await client.PutAsJsonAsync($"api/event/waves/{waveId}/riders", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<WaveDto>(cancellationToken);
        }, cancellationToken);

    public Task<WaveStartResult?> StartWaveAsync(string waveId, CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(async client =>
        {
            using var response = await client.PostAsync($"api/event/waves/{waveId}/start", null, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<WaveStartResult>(cancellationToken);
        }, cancellationToken);

    public Task<IReadOnlyList<RiderManagementDto>?> GetRidersAsync(CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(client => client.GetFromJsonAsync<IReadOnlyList<RiderManagementDto>>("api/admin/riders", cancellationToken), cancellationToken);

    public Task<RiderManagementDto?> CreateRiderAsync(RiderCreateRequest request, CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(async client =>
        {
            using var response = await client.PostAsJsonAsync("api/admin/riders", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<RiderManagementDto>(cancellationToken);
        }, cancellationToken);

    public Task<RiderManagementDto?> UpdateRiderAsync(string riderId, RiderUpdateRequest request, CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(async client =>
        {
            using var response = await client.PutAsJsonAsync($"api/admin/riders/{riderId}", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<RiderManagementDto>(cancellationToken);
        }, cancellationToken);

    public Task<bool> DeleteRiderAsync(string riderId, CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(async client =>
        {
            using var response = await client.DeleteAsync($"api/admin/riders/{riderId}", cancellationToken);
            return response.IsSuccessStatusCode;
        }, cancellationToken);

    public Task<ImportResults?> SeedRiderContactsAsync(CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(async client =>
        {
            using var response = await client.PostAsync("api/admin/riders/seed-contacts", null, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ImportResults>(cancellationToken);
        }, cancellationToken);

    public Task<IReadOnlyList<RouteTypeDto>?> GetRouteTypesAsync(CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(client => client.GetFromJsonAsync<IReadOnlyList<RouteTypeDto>>("api/admin/route-types", cancellationToken), cancellationToken);

    public Task<RouteTypeDto?> CreateRouteTypeAsync(RouteTypeCreateRequest request, CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(async client =>
        {
            using var response = await client.PostAsJsonAsync("api/admin/route-types", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<RouteTypeDto>(cancellationToken);
        }, cancellationToken);

    public Task<RouteTypeDto?> UpdateRouteTypeAsync(string routeTypeId, RouteTypeUpdateRequest request, CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(async client =>
        {
            using var response = await client.PutAsJsonAsync($"api/admin/route-types/{routeTypeId}", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<RouteTypeDto>(cancellationToken);
        }, cancellationToken);

    public Task<bool> DeleteRouteTypeAsync(string routeTypeId, CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(async client =>
        {
            using var response = await client.DeleteAsync($"api/admin/route-types/{routeTypeId}", cancellationToken);
            return response.IsSuccessStatusCode;
        }, cancellationToken);

    public Task<IReadOnlyList<OfficialDto>?> GetOfficialsAsync(CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(client => client.GetFromJsonAsync<IReadOnlyList<OfficialDto>>("api/admin/officials", cancellationToken), cancellationToken);

    public Task<OfficialVerificationResult?> VerifyOfficialAsync(OfficialVerificationRequest request, CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(async client =>
        {
            using var response = await client.PostAsJsonAsync("api/admin/officials/verify", request, cancellationToken);
            // The API returns a structured OfficialVerificationResult even on 400/401, so read the body.
            return await response.Content.ReadFromJsonAsync<OfficialVerificationResult>(cancellationToken);
        }, cancellationToken);

    public Task<TimingCommandResult?> ResetTimingsAsync(TimingCommandRequest request, CancellationToken cancellationToken = default) =>
        PostTimingCommandAsync("api/admin/timings/reset", request, cancellationToken);

    public Task<OfficialDto?> CreateOfficialAsync(OfficialCreateRequest request, CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(async client =>
        {
            using var response = await client.PostAsJsonAsync("api/admin/officials", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<OfficialDto>(cancellationToken);
        }, cancellationToken);

    public Task<OfficialDto?> UpdateOfficialAsync(string officialId, OfficialUpdateRequest request, CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(async client =>
        {
            using var response = await client.PutAsJsonAsync($"api/admin/officials/{officialId}", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<OfficialDto>(cancellationToken);
        }, cancellationToken);

    public Task<bool> DeleteOfficialAsync(string officialId, CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(async client =>
        {
            using var response = await client.DeleteAsync($"api/admin/officials/{officialId}", cancellationToken);
            return response.IsSuccessStatusCode;
        }, cancellationToken);

    public Task<IReadOnlyList<FinishedTimeReportRowDto>?> GetFinishedTimeReportAsync(CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(client => client.GetFromJsonAsync<IReadOnlyList<FinishedTimeReportRowDto>>("api/reports/finished-times", cancellationToken), cancellationToken);

    public Task<string?> GetFinishedTimesCsvAsync(CancellationToken cancellationToken = default) =>
        SendWithFallbackAsync(async client =>
        {
            using var response = await client.GetAsync("api/reports/finished-times.csv", cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }, cancellationToken);

    private Task<TimingCommandResult?> PostTimingCommandAsync(string route, TimingCommandRequest request, CancellationToken cancellationToken) =>
        SendWithFallbackAsync(async client =>
        {
            using var response = await client.PostAsJsonAsync(route, request, cancellationToken);

            // Start/stop endpoints return a structured TimingCommandResult even on validation failures (400).
            var result = await response.Content.ReadFromJsonAsync<TimingCommandResult>(cancellationToken);
            if (result is not null)
            {
                return result;
            }

            response.EnsureSuccessStatusCode();
            return null;
        }, cancellationToken);

    private async Task<T?> SendWithFallbackAsync<T>(Func<HttpClient, Task<T?>> sendAsync, CancellationToken cancellationToken)
    {
        foreach (var baseAddress in GetCandidateBaseAddresses())
        {
            for (var attempt = 1; attempt <= MaxAttemptsPerBaseAddress; attempt++)
            {
                using var httpClient = new HttpClient
                {
                    BaseAddress = baseAddress,
                    Timeout = TimeSpan.FromSeconds(15)
                };

                try
                {
                    var result = await sendAsync(httpClient);
                    if (result is not null)
                    {
                        return result;
                    }

                    break;
                }
                catch (HttpRequestException ex) when (IsTransient(ex) && attempt < MaxAttemptsPerBaseAddress)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
                }
                catch (HttpRequestException)
                {
                    break;
                }
                catch (JsonException)
                {
                    break;
                }
                catch (NotSupportedException)
                {
                    break;
                }
                catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (TaskCanceledException) when (attempt < MaxAttemptsPerBaseAddress)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        return default;
    }

    private static bool IsTransient(HttpRequestException exception)
    {
        if (exception.StatusCode is null)
        {
            return true;
        }

        return exception.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            || (int)exception.StatusCode >= 500;
    }

    private IEnumerable<Uri> GetCandidateBaseAddresses()
    {
        var candidateUrls = configuration
            .GetSection("ApiBaseUrls")
            .GetChildren()
            .Select(item => item.Value)
            .Append(configuration["ApiBaseUrl"]);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var appBaseUri = new Uri(navigationManager.BaseUri, UriKind.Absolute);

        var appIsLocal = appBaseUri.IsLoopback
            || string.Equals(appBaseUri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || appBaseUri.Host.StartsWith("127.", StringComparison.OrdinalIgnoreCase);

        // Prefer configured candidate URLs first (explicit API hosts), then fall back to the app origin.
        foreach (var candidateUrl in candidateUrls)
        {
            if (string.IsNullOrWhiteSpace(candidateUrl))
            {
                continue;
            }

            var normalizedUrl = candidateUrl.EndsWith("/", StringComparison.Ordinal) ? candidateUrl : $"{candidateUrl}/";

            if (!Uri.TryCreate(normalizedUrl, UriKind.RelativeOrAbsolute, out var parsedUri))
            {
                continue;
            }

            if (!parsedUri.IsAbsoluteUri)
            {
                parsedUri = new Uri(appBaseUri, parsedUri);
            }

            if (!appIsLocal && (parsedUri.IsLoopback || string.Equals(parsedUri.Host, "localhost", StringComparison.OrdinalIgnoreCase) || parsedUri.Host.StartsWith("127.", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (seen.Add(parsedUri.AbsoluteUri))
            {
                yield return parsedUri;
            }
        }

        if (seen.Add(appBaseUri.AbsoluteUri))
        {
            yield return appBaseUri;
        }
    }
}