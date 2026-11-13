using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using NaijaPrimeSchool.Mobile.Contracts.Common;
using NaijaPrimeSchool.Mobile.Shared.Services.Auth;

namespace NaijaPrimeSchool.Mobile.Shared.Services.Api;

// Raised for anything that stops a request from producing data. Pages show
// Message to the user (it carries the API's own business-rule messages).
public sealed class ApiException(string message, HttpStatusCode? status = null, IReadOnlyList<string>? errors = null)
    : Exception(message)
{
    public HttpStatusCode? Status { get; } = status;
    public IReadOnlyList<string> Errors { get; } = errors ?? [message];
}

// Result of a command (POST / PUT / DELETE). Mirrors OperationResult.
public sealed class ApiResult<T>
{
    public bool Succeeded { get; init; }
    public T? Data { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public string ErrorText => string.Join(" ", Errors);
}

public sealed class ApiResult
{
    public bool Succeeded { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public string ErrorText => string.Join(" ", Errors);
}

// Authenticated JSON client for NaijaPrimeSchool.Mobile.Api: attaches the
// bearer token, transparently refreshes it once on a 401, and translates
// HTTP failures into friendly messages.
public sealed class ApiClient(HttpClient http, ApiSettings settings, SessionManager sessions)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string BaseUrl => settings.BaseUrl;

    // GET returning null on 404 (resource missing or not visible to the caller).
    public async Task<T?> GetAsync<T>(string url, bool anonymous = false, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Get, url, null, anonymous, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return default;
        await ThrowIfFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<T>(Json, ct);
    }

    public async Task<List<T>> GetListAsync<T>(string url, CancellationToken ct = default) =>
        await GetAsync<List<T>>(url, ct: ct) ?? [];

    public Task<ApiResult> PostAsync(string url, object? body = null, CancellationToken ct = default) =>
        CommandAsync(HttpMethod.Post, url, body, ct);

    public Task<ApiResult> PutAsync(string url, object? body, CancellationToken ct = default) =>
        CommandAsync(HttpMethod.Put, url, body, ct);

    public Task<ApiResult> DeleteAsync(string url, CancellationToken ct = default) =>
        CommandAsync(HttpMethod.Delete, url, null, ct);

    public async Task<ApiResult<T>> PostAsync<T>(string url, object? body, CancellationToken ct = default)
    {
        try
        {
            using var response = await SendAsync(HttpMethod.Post, url, body, false, ct);
            if (response.IsSuccessStatusCode)
            {
                var data = response.StatusCode == HttpStatusCode.NoContent
                    ? default
                    : await response.Content.ReadFromJsonAsync<T>(Json, ct);
                return new ApiResult<T> { Succeeded = true, Data = data };
            }
            return new ApiResult<T> { Errors = await ReadErrorsAsync(response) };
        }
        catch (ApiException ex)
        {
            return new ApiResult<T> { Errors = ex.Errors };
        }
    }

    private async Task<ApiResult> CommandAsync(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        try
        {
            using var response = await SendAsync(method, url, body, false, ct);
            return response.IsSuccessStatusCode
                ? new ApiResult { Succeeded = true }
                : new ApiResult { Errors = await ReadErrorsAsync(response) };
        }
        catch (ApiException ex)
        {
            return new ApiResult { Errors = ex.Errors };
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body, bool anonymous, CancellationToken ct)
    {
        await sessions.GetSessionAsync(); // makes sure the saved server address is loaded
        try
        {
            var token = anonymous ? null : await sessions.GetAccessTokenAsync();
            if (!anonymous && token is null)
                throw new ApiException("Your session has ended. Please sign in again.", HttpStatusCode.Unauthorized);

            var response = await http.SendAsync(Build(method, url, body, token), ct);
            if (response.StatusCode != HttpStatusCode.Unauthorized || anonymous) return response;

            // Access token rejected: try one refresh, then give up.
            response.Dispose();
            token = await sessions.GetAccessTokenAsync(forceRefresh: true);
            if (token is null)
                throw new ApiException("Your session has ended. Please sign in again.", HttpStatusCode.Unauthorized);

            response = await http.SendAsync(Build(method, url, body, token), ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                response.Dispose();
                await sessions.ClearAsync();
                throw new ApiException("Your session has ended. Please sign in again.", HttpStatusCode.Unauthorized);
            }
            return response;
        }
        catch (HttpRequestException)
        {
            throw new ApiException($"Cannot reach the school server at {settings.BaseUrl}. Check your internet connection and try again.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ApiException("The school server took too long to answer. Please try again.");
        }
    }

    private HttpRequestMessage Build(HttpMethod method, string url, object? body, string? token)
    {
        var request = new HttpRequestMessage(method, settings.Resolve(url));
        if (body is not null) request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static async Task ThrowIfFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var errors = await ReadErrorsAsync(response);
        throw new ApiException(string.Join(" ", errors), response.StatusCode, errors);
    }

    private static async Task<IReadOnlyList<string>> ReadErrorsAsync(HttpResponseMessage response)
    {
        switch (response.StatusCode)
        {
            case HttpStatusCode.Forbidden:
                return ["You do not have permission to do that."];
            case HttpStatusCode.NotFound:
                return ["That record was not found. It may have been deleted."];
            case HttpStatusCode.TooManyRequests:
                return ["Too many requests. Please wait a moment and try again."];
        }

        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(Json);
            if (error?.Errors.Count > 0) return error.Errors;
        }
        catch
        {
            // Not an ApiError body.
        }
        return (int)response.StatusCode >= 500
            ? ["The school server ran into a problem. Please try again shortly."]
            : ["The request could not be completed."];
    }
}

// Builds "path?a=1&b=2" from optional parameters, skipping nulls.
public static class Query
{
    public static string Build(string path, params (string Key, object? Value)[] parameters)
    {
        var sb = new StringBuilder(path);
        var first = !path.Contains('?');
        foreach (var (key, value) in parameters)
        {
            var text = value switch
            {
                null => null,
                string s when string.IsNullOrWhiteSpace(s) => null,
                string s => s.Trim(),
                bool b => b ? "true" : "false",
                DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DateTime d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString(),
            };
            if (text is null) continue;
            sb.Append(first ? '?' : '&').Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(text));
            first = false;
        }
        return sb.ToString();
    }
}
