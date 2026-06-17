using System.Collections.Generic;
using Xunit;

namespace Jellyfin.Plugin.Jimaku.Tests;

public static class RequestHandlerTests
{
    public static TheoryData<string, Dictionary<string, string>, string> AddQueryString_TestData()
        => new ()
        {
            {
                "/entries/search",
                new Dictionary<string, string>
                {
                    { "query", "Frieren" },
                    { "anime", "true" }
                },
                "/entries/search?anime=true&query=Frieren"
            },
            {
                "/entries/search",
                new Dictionary<string, string>
                {
                    { "tmdb_id", "tv:12345" },
                    { "anime", "false" }
                },
                "/entries/search?anime=false&tmdb_id=tv%3a12345"
            },
            {
                "/entries/search",
                new Dictionary<string, string>(),
                "/entries/search"
            }
        };

    [Theory]
    [MemberData(nameof(AddQueryString_TestData))]
    public static void AddQueryString(string path, Dictionary<string, string> param, string expected)
        => Assert.Equal(expected, RequestHandler.AddQueryString(path, param));
}
