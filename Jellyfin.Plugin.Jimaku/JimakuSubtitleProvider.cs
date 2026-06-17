using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Jimaku;

/// <summary>
/// The Jimaku subtitle provider.
/// </summary>
public class JimakuSubtitleProvider : ISubtitleProvider
{
    /// <inheritdoc />
    public string Name => "Jimaku";

    /// <inheritdoc />
    public IEnumerable<VideoContentType> SupportedMediaTypes
        => new[] { VideoContentType.Episode, VideoContentType.Movie };

    /// <inheritdoc />
    public Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        => throw new System.NotImplementedException();

    /// <inheritdoc />
    public Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
        => Task.FromResult(Enumerable.Empty<RemoteSubtitleInfo>());
}
