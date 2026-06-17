using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Jimaku.Models;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Jimaku.API;

/// <summary>
/// The Jimaku plugin controller.
/// </summary>
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[Authorize(Policy = Policies.SubtitleManagement)]
public class JimakuController : ControllerBase
{
    /// <summary>
    /// Validates an API key.
    /// </summary>
    /// <remarks>
    /// Accepts API key as JSON body.
    /// </remarks>
    /// <response code="200">API key valid.</response>
    /// <response code="400">API key is missing.</response>
    /// <response code="401">API key not valid.</response>
    /// <param name="body">The request body.</param>
    /// <returns>
    /// An <see cref="OkResult"/> if the API key is valid, a <see cref="BadRequestResult"/> if missing,
    /// or <see cref="UnauthorizedResult"/> if invalid.
    /// </returns>
    [HttpPost("Jellyfin.Plugin.Jimaku/ValidateApiKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> ValidateApiKey([FromBody] ApiKeyInput body)
    {
        var response = await JimakuApi.ValidateApiKeyAsync(
            body.ApiKey,
            CancellationToken.None).ConfigureAwait(false);

        if (!response.Ok)
        {
            var msg = $"{response.Code}{(response.Body.Length < 150 ? $" - {response.Body}" : string.Empty)}";

            if (response.Body.Contains("error\":", StringComparison.Ordinal))
            {
                var err = JsonSerializer.Deserialize<ApiError>(response.Body);
                if (err is not null)
                {
                    msg = err.Error ?? msg;
                }
            }

            return Unauthorized(new { Message = msg });
        }

        return Ok(new { Message = "API key is valid" });
    }
}
