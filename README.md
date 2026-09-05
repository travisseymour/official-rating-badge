# Official Rating Badge — Jellyfin Plugin

---

Draws the item's `OfficialRating` (PG-13, R, TV-MA, etc.) as a small badge
directly onto poster images, server-side. No client JavaScript injection,
no third-party image service — the whole thing runs inside your own
Jellyfin server process as ASP.NET Core middleware, the same technique
JellyTag and Jellyfin-Quality-Overlay use for their quality badges.

## Before you build

1. **Generate your own plugin GUID** and replace the placeholder in both
   `Plugin.cs` (`Id => Guid.Parse(...)`) and `build.yaml` (`guid:`). They
   must match each other and must never change across future releases.
   On Linux/macOS: `uuidgen`. On Windows: PowerShell `[guid]::NewGuid()`.
2. **Match the Jellyfin.Controller / Jellyfin.Model package versions** in
   `OfficialRatingBadge.csproj` to your actual server version (Dashboard →
   General). A mismatch makes Jellyfin show the plugin as "NotSupported"
   even if it would otherwise work fine.
3. Confirm your server's .NET runtime is 9.0+ (bundled with Jellyfin
   10.11+). If you're on an older Jellyfin, target `net8.0` instead and
   use the matching package/ABI versions.

## Build

```bash
dotnet restore
dotnet publish --configuration Release --output bin
```

This produces `bin/OfficialRatingBadge.dll` (plus SkiaSharp's native
dependencies alongside it — leave those in the output, Jellyfin needs
them at runtime).

## Test it locally first

Before wiring up a repository, just drop the built files straight into
your plugins folder and restart:

- Linux: `~/.local/share/jellyfin/plugins/OfficialRatingBadge/`
- Docker: the mapped `/config/plugins/OfficialRatingBadge/` volume path
- Windows: `%LocalAppData%\jellyfin\plugins\OfficialRatingBadge\`

Restart Jellyfin, then load a poster for a movie/show that has an
OfficialRating set and confirm the badge appears in the bottom-left
corner. Check Dashboard → Logs if it doesn't — most first-run issues are
either the GUID/version mismatch above, or SkiaSharp's native library not
being present on your OS/architecture.

## Package for a self-hosted repository (once it's working)

This is what lets you install it the normal Jellyfin way (Repositories →
Catalog) instead of manually copying files after every update.

```bash
pip install jellyfin-plugin-repo-manager  # jprm, the standard packaging tool
jprm plugin build . --output ./dist
jprm repo add ./repo.json ./dist/officialratingbadge-1.0.0.0.zip
```

`jprm repo add` creates (or updates) `repo.json` — Jellyfin expects the
file to be named `manifest.json`, so rename it:

```bash
mv repo.json manifest.json
```

## Host it

Commit `manifest.json` and the built zip to your own GitHub repo (a
public repo is fine — this is the same trust model as every other
community plugin, except now you're the one who wrote and controls it).
Then add the repository in Jellyfin using your own raw GitHub URL:

```
https://raw.githubusercontent.com/<your-username>/<your-repo>/main/manifest.json
```

## Known limitations of this v1

- Position/size are hardcoded (bottom-left, ~5.5% of poster width). Add
  fields to `PluginConfiguration.cs` and a config page later if you want
  this adjustable without recompiling.
- Only handles `Primary` (poster) images — thumbnails/backdrops are
  untouched. Extend the regex in `OfficialRatingBadgeMiddleware.cs` if
  you want those too.
- No disk caching — every image request re-decodes and re-draws. Fine
  for personal-scale libraries; JellyTag's disk-cache-by-image-tag
  approach is worth copying if you notice CPU load on a large library.
- Font fallback: `SKTypeface.FromFamilyName("Arial", ...)` may not
  resolve on a minimal Linux container without common fonts installed.
  If badges render with a blank/fallback glyph, install `fontconfig` and
  a basic font package in your Jellyfin container, or bundle a .ttf file
  with the plugin and load it via `SKTypeface.FromFile(...)` instead.
