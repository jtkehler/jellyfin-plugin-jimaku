using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Jellyfin.Plugin.Jimaku.Models;

namespace Jellyfin.Plugin.Jimaku;

/// <summary>
/// The request handler for the Jimaku API.
/// </summary>
public static class RequestHandler
{
    private const string BaseApiUrl = "https://jimaku.cc/api";
    private const int RetryLimit = 5;

    /// <summary>
    /// Send the request.
    /// </summary>
    /// <param name="endpoint">The endpoint to send request to.</param>
    /// <param name="method">The method.</param>
    /// <param name="body">The request body.</param>
    /// <param name="headers">The headers.</param>
    /// <param name="attempt">The request attempt key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="isFullUrl">The flag to not append baseUrl.</param>
    /// <param name="apiKeyOverride">Optional API key to use instead of the configured one.</param>
    /// <returns>The response.</returns>
    public static async Task<HttpResponse> SendRequestAsync(
        string endpoint,
        HttpMethod method,
        object? body,
        Dictionary<string, string>? headers,
        int attempt,
        CancellationToken cancellationToken,
        bool isFullUrl = false,
        string? apiKeyOverride = null)
    {
        headers ??= new Dictionary<string, string>();

        var apiKey = apiKeyOverride ?? JimakuPlugin.Instance?.Configuration.ApiKey;
        if (!string.IsNullOrEmpty(apiKey))
        {
            headers.TryAdd("Authorization", apiKey);
        }

        var url = isFullUrl ? endpoint : BaseApiUrl + endpoint;
        var response = await JimakuRequestHelper.Instance!.SendRequestAsync(url, method, body, headers, cancellationToken).ConfigureAwait(false);

        if (response.statusCode == HttpStatusCode.TooManyRequests
            && attempt < RetryLimit
            && response.headers.TryGetValue("x-ratelimit-reset-after", out var retryAfterStr)
            && float.TryParse(retryAfterStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var retryAfter))
        {
            var delay = TimeSpan.FromSeconds(Math.Ceiling(retryAfter));
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            return await SendRequestAsync(endpoint, method, body, headers, attempt + 1, cancellationToken, isFullUrl, apiKeyOverride).ConfigureAwait(false);
        }

        if (response.statusCode == HttpStatusCode.BadGateway && attempt < RetryLimit)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
            return await SendRequestAsync(endpoint, method, body, headers, attempt + 1, cancellationToken, isFullUrl, apiKeyOverride).ConfigureAwait(false);
        }

        if (!response.headers.TryGetValue("x-reason", out var responseReason))
        {
            responseReason = string.Empty;
        }

        return new HttpResponse
        {
            Body = response.body,
            Code = response.statusCode,
            Reason = responseReason
        };
    }

    /// <summary>
    /// Append the given query keys and values to the URI.
    /// </summary>
    /// <param name="path">The base URI.</param>
    /// <param name="param">A dictionary of query keys and values to append.</param>
    /// <returns>The combined result.</returns>
    public static string AddQueryString(string path, Dictionary<string, string> param)
    {
        if (param.Count == 0)
        {
            return path;
        }

        var url = new StringBuilder(path);
        url.Append('?');
        foreach (var (key, value) in param.OrderBy(x => x.Key))
        {
            url.Append(HttpUtility.UrlEncode(key.ToLowerInvariant()))
                .Append('=')
                .Append(HttpUtility.UrlEncode(value))
                .Append('&');
        }

        url.Length -= 1;
        return url.ToString();
    }
}
