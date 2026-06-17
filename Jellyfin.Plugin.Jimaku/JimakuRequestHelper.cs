using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Jimaku;

/// <summary>
/// HTTP utility helper for Jimaku API requests.
/// </summary>
public class JimakuRequestHelper
{
    private readonly IHttpClientFactory _clientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="JimakuRequestHelper"/> class.
    /// </summary>
    /// <param name="factory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    public JimakuRequestHelper(IHttpClientFactory factory)
    {
        _clientFactory = factory;
    }

    /// <summary>
    /// Gets or sets the current instance.
    /// </summary>
    public static JimakuRequestHelper? Instance { get; set; }

    internal async Task<(string body, Dictionary<string, string> headers, HttpStatusCode statusCode)> SendRequestAsync(
        string url,
        HttpMethod method,
        object? body,
        Dictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        var client = _clientFactory.CreateClient(nameof(Jimaku));

        HttpContent? content = null;
        if (method != HttpMethod.Get && body is not null)
        {
            content = JsonContent.Create(body);
        }

        using var request = new HttpRequestMessage
        {
            Method = method,
            RequestUri = new Uri(url),
            Content = content
        };

        foreach (var (key, value) in headers)
        {
            if (string.Equals(key, "Authorization", StringComparison.OrdinalIgnoreCase))
            {
                request.Headers.TryAddWithoutValidation("Authorization", value);
            }
            else
            {
                request.Headers.Add(key, value);
            }
        }

        using var result = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var resHeaders = result.Headers.ToDictionary(x => x.Key, x => x.Value.First(), StringComparer.OrdinalIgnoreCase);
        var resBody = await result.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        return (resBody, resHeaders, result.StatusCode);
    }
}
