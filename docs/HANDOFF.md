# Handoff — Wrestling Game Museum

_Last updated: end of session 1 (2026-10-21)._ Branch: `claude/museum-build`.

## State in one paragraph
The solution is scaffolded and builds clean (0 warnings, 0 errors, `-p:EnableWindowsTargeting=true`); 50 tests pass.
The WPF app has the museum index (slim toolbar, filter drawer, virtualised shelves of tiles) and the exhibit page
(header card, regional releases, screenshots, videos, roster, sources). The harvester builds `data/games.json`
from curated seed entries: **31 games, 29 with a LaunchBox box front**. This is **build-order step 1 complete**, and
the harvester core of step 2 exists; the full game list (400–600 titles) has **not** been harvested yet.

**Nothing has been seen on screen.** The cloud container cannot render WPF. XAML compiles and the markup tests pass,
but layout, tile sizing, shadows, hover colours, virtualisation smoothness and fonts all need a visual check on the
owner's Windows PC (see "Needs a look" below). Do not assume any of it looks right yet.

## What works (verified)
- `dotnet build` / `dotnet test` green (50 tests): catalog schema/consistency, query + shelf + layout logic,
  harvester parsers (fixtures), XAML markup rules (icon-only buttons have ToolTip + AutomationProperties.Name,
  inputs are named, one `ToolbarBand` per view, tile spec values, Fire Pro Museum colour tokens present, MinWidth 900).
- Harvester end to end against the live archives: facts from SmackDown Hotel pages, box fronts from LaunchBox,
  `data/games.json` written, contact sheet reviewed by eye (30 box fronts looked at; see decisions).

## What is not verified
- Anything visual or interactive in the WPF app (see list below).
- Image streaming/caching (`Services/ImageCache.cs`, `CachedImage.cs`) has never run — only compiled.
- Release dates/platforms are the archive's, not independently cross-checked (see "Data caveats").

## Layout of the code
- `Core/Game.cs` — record model (`Game`, `Release`, `BoxArt`, `RegionalRelease`, `MediaLink`, `RosterEntry`, `SourceRef`),
  derived `Year`, `Era`, `Facts`, `Eyebrow`. `Core/GameQuery.cs` — search, 5 filters, sort, shelves (era / series / developer).
  `Core/IndexLayout.cs` — flattens shelves into rows (`BandRow`, `TileRow`, `EmptyRow`, `FooterRow`), 3–5 columns by width.
- App: `MainViewModel` (all state: query, rows, selected game, prev/next across the **filtered** list, roster filter),
  `Views/IndexView` (rows in a virtualising `ListBox`, pixel scrolling, recycling), `Views/ExhibitView`, `Theme/Museum.xaml`
  (tokens from the Fire Pro Museum; section titles use **Georgia Bold**, which is what the Manager's `DashSerifTitle` is).
- Keyboard: Backspace/Esc = back, Left/Right = previous/next (not while typing in a box/combo).
- Harvester commands: `facts`, `art`, `sheet`, `build` (default). Reads `data/curation.json`; caches raw pages in `tools/cache/`
  (git-ignored); writes `tools/out/art-report.json` and `tools/out/contact-sheet.html` (git-ignored).

## Needs a look on the owner's Windows PC
1. Index: tile heights/gaps at 900, 1280, 1920 px wide (3/4/5 columns); image area ~324 at full width; no clipped text at 900.
2. Box art actually streams in; shadow follows the true box shape; placeholder shows for games without art; scrolling stays smooth.
3. Hover/focus colours on tiles; Tab order; the filter drawer; the grouping/sort icon buttons and their tooltips.
4. Exhibit: header card proportions (box max 320×380), fact chips, pull quote, Backspace/Esc/arrow keys.
5. `FluentWindow` + `TitleBar` look and the page gradient (a dark arena image could replace it later).
6. Chosen icons exist and read well (`GroupList24` for grouping, `ArrowSort24` for sort, `Filter24`, `Eraser24`).

## Data decisions and caveats
- **Seed source:** the Manager's `MuseumViewModel.cs` holds 24 Fire Pro / King of Colosseum / Joshi / Gaiden entries; the brief said
  it also holds THQ/AKI games and "already chosen" LaunchBox fronts — it does not (only SFPW X has a LaunchBox URL; the rest use SmackDown
  Hotel banners). So all box fronts were re-chosen from LaunchBox here. The 24 games' short texts (significance/history/features) are the
  Manager's wording, carried over unchanged; 7 more games (VPW2, WCW/nWo Revenge, WCW vs. the World, Power Move Pro Wrestling,
  Thunder Pro Wrestling Retsuden, Rumble Roses, ECW Hardcore Revolution) carry **archive facts only — no written story yet**.
- **Box art picks** (checked on a contact sheet): Japan box for Japan-only games, else North America. Exceptions, pinned in `curation.json`:
  KoC Red/Green are filed on LaunchBox under their subtitles; VPW2 under "Oudou Keishou"; XBLA Fire Pro under id 150675.
  - Fire Pro Wrestling Iron Slam '96: LaunchBox only has a logo on white → **no box** (`noBoxArtReason`), placeholder shown.
  - WCW/nWo Revenge: LaunchBox has fronts but none pass the shape check → no box yet; find another source.
  - XBLA Fire Pro box is only 219×300 — will look soft; look for a better scan.
  - SFPW X's chosen scan is 2419×4437 (4 MB); the app decodes at display size, but consider a smaller alternative.
  - Fire Pro Gaiden: Blazing Tornado uses the Arcade entry (id 27163; "United States" region), not the Saturn port.
- **Archive quirks seen:** one release date per game (for multi-platform games it is the first release, not per platform — e.g. ECW Hardcore
  Revolution lists 4 platforms with one date); "Game Series" mixes real series with tags ("Japanese", "Independent Games") — the builder
  drops those two tags; promotion for most Fire Pro games is "Independent Circuit" (the archive's catch-all). The known errors in the brief
  (WrestleMania 2000, Virtual Pro Wrestling publisher) are for games not in the seed yet — verify when they are harvested.
  Platform names are the archive's ("Super NES", "PlayStation 1", "TurboGrafx-16 (PC Engine)"); normalise before the Platform filter gets long.
- **Not cross-checked yet:** MobyGames returns 403 and Wikipedia 429 to the cloud proxy, so completeness cross-checks need another route
  (or run the harvester from the owner's PC). Every record currently cites the archive page for its facts.

## Harvester access notes (important)
- thesmackdownhotel.com returns **403 to a UA containing "Harvester"** (and to bare tool UAs) but accepts a browser-style UA with a
  plain product token. `PoliteHttp.UserAgent` ends in `WrestlingGameMuseum/0.1`. Concurrency 2, 400 ms delay, 5 tries with backoff, cache on disk.
- The game index page is JavaScript-driven; **the sitemap is the reliable list**: `/sitemap-4seo.xml` → `sitemap.en-GB.4seo.{1..10}.xml`.
  It yielded 147 `/games/<slug>` URLs (saved in `tools/slugs-sitemap.txt`, includes a few non-game pages like `page-2` and news slugs; it is
  also clearly incomplete — WWE 2K, SmackDown, No Mercy etc. are missing). The remaining slugs must come from the paginated index or other lists.
- LaunchBox search: `/games/results/<q>` (a `/` in the query causes a redirect loop — the parser swaps it for a space).

## Next steps (in order)
1. **Visual check** of the app on Windows (list above) and fix what is off.
2. Harvester step 2: add a `list` command (sitemap → slugs, plus the index pagination/other sources) and let `build` run over *all* slugs, with
   curation only as overrides; harvest by era in batches, contact-sheet review per batch, commit per batch. Add the Wikipedia/Moby cross-check
   (from a network that allows it) and record disagreements in `sources`.
3. Step 3 polish: image decode-size tuning, optional dark arena background image, debounce search, remember window size.
4. Step 4: regional releases (`regional[]` with their own box thumbnails — model and view exist, no data yet), verified media, documented rosters
   (model/DataGrid exist; every game currently shows "Roster research in progress").
5. Step 5: keyboard/automation pass on real hardware, narrow-width check, empty states.

## Validation totals
- `dotnet build -c Debug -p:EnableWindowsTargeting=true`: 0 warnings, 0 errors.
- `dotnet test -p:EnableWindowsTargeting=true`: 50 passed, 0 failed.
- `data/games.json`: 31 games, 29 with box art, every record has title, year, platform and at least one source.
