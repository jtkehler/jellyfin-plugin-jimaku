# AGENTS.md — Jellyfin Plugin (Jimaku Migration)

## Commands

```bash
dotnet build                          # Build main project
dotnet build --configuration Release  # Release build
dotnet test                           # Unit tests (requires net9.0 runtime)
```

**Runtime note**: The host has .NET 10 SDK but no net9.0 runtime. The test project targets net9.0 and will fail with `app-launch-failed`. Building still works. If you can't install net9.0 runtime, convert test .csproj `<TargetFramework>` to `net10.0` temporarily.

## Architecture

Jellyfin plugin — three key contracts:

| Contract | Implementation | Registered in |
|---|---|---|
| `BasePlugin<PluginConfiguration>` | `OpenSubtitlesPlugin` / `JimakuPlugin` | Auto-discovered |
| `IHasWebPages` | (same plugin class) | Auto-discovered |
| `ISubtitleProvider` | `OpenSubtitleDownloader` / `JimakuSubtitleProvider` | `PluginServiceRegistrator` |
| `IPluginServiceRegistrator` | `PluginServiceRegistrator` | Auto-discovered |

The `ISubtitleProvider` interface has two methods: `Search(SubtitleSearchRequest)` → `IEnumerable<RemoteSubtitleInfo>` and `GetSubtitles(string id)` → `SubtitleResponse`.

## Plugin Identity

Two places must agree: `Plugin.Id` (C# Guid) and `build.yaml` guid. If they mismatch, the plugin manifest won't work. Generate a fresh GUID for Jimaku — never reuse the OpenSubtitles GUID `4b9ed42f-5185-48b5-9803-6ff2989014c4`.

## Web UI

- HTML+JS files live in `Web/` as `EmbeddedResource` in .csproj.
- `IHasWebPages.GetPages()` must return `GetType().Namespace + ".Web.<name>"` as `EmbeddedResourcePath`.
- The JS entry is an ES module (`export default function(view, params)`).
- The config page's `data-controller` attribute references the plugin JS: `__plugin/<jsname>`.
- Plugin unique ID in JS must match the plugin GUID.
- The API controller route pattern is `Jellyfin.Plugin.<Name>/Validate*`.

## HttpClient

Use `IHttpClientFactory` with a named client registered in `PluginServiceRegistrator`. The rate limiter is a `DelegatingHandler` (`ClientSideRateLimiter`) set as primary handler on the named client. Auth header is added per-request in the request helper, not globally.

## Namespace Trap

Jellyfin 10.11.x moved `VideoContentType` from `MediaBrowser.Model.Entities` to **`MediaBrowser.Controller.Providers`**. Any `ISubtitleProvider` implementation must include `using MediaBrowser.Controller.Providers;` to resolve it. The old OpenSubtitles downloader has both usings; new code should too.

## Migration Sequence

This repo is in the process of converting from OpenSubtitles to Jimaku. Both plugin projects coexist in the solution during migration. The Jimaku GUID is `859cd24d-e976-423d-9f24-38a9f037cc0b`.

PR sequence:

1. **Scaffold** — New project, solution, plugin class, config, registrator, Web stubs, build.yaml
2. **API client** — RequestHandler, JimakuRequestHelper, JimakuApi, models, tests
3. **Provider** — JimakuSubtitleProvider (Search + GetSubtitles with dual-mode dispatch)
4. **Config UI** — Controller, HTML/JS, configuration wireup
5. **Polish** — README, workflows, cleanup

## Conventions

- `Nullability` is enabled. All reference types are non-null by default.
- `TreatWarningsAsErrors` is on. Build must produce zero warnings.
- StyleCop analyzers with strict rules (see `.editorconfig`). SA1513, CA2016, etc. are errors.
- Plugin uses a static `Instance` singleton pattern for configuration change events.
- Subtitle IDs are encoded as compound strings (e.g., `srt-eng-12345-sdh-forced`) parsed by `GetSubtitlesInternal`.
- The `build.yaml` `targetAbi` field is the Jellyfin ABI version (currently `10.11.8.0`) — must match target server.
