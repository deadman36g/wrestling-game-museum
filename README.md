# Wrestling Game Museum

A browsable museum of professional-wrestling video games, as a portable Windows desktop app
(C# / .NET 10 WPF, WPF-UI, CommunityToolkit.Mvvm). Read-only: browse, search and open any game's exhibit.
Sibling of the Fire Pro Museum inside the Fire Pro Wrestling World Manager.

| Path | What |
| --- | --- |
| `src/WrestlingGameMuseum` | The WPF app (views, theme, image streaming/caching) |
| `src/WrestlingGameMuseum.Core` | Catalog model, search/filter/sort/shelf logic, index layout (no WPF, testable anywhere) |
| `tools/Harvester` | Console tool that builds `data/games.json` from the archives |
| `data/games.json` | The catalog (facts + links only; never artwork) |
| `data/curation.json` | Hand-written, sourced additions/corrections fed to the harvester |
| `tests/` | xUnit: catalog rules, query/layout logic, parsers, XAML markup rules |
| `docs/HANDOFF.md` | Current state and next steps — start here |

## Build and test

```
dotnet build -c Debug -p:EnableWindowsTargeting=true
dotnet test  -p:EnableWindowsTargeting=true
dotnet run --project src/WrestlingGameMuseum        # on Windows
dotnet run --project tools/Harvester -- build        # refresh data/games.json (needs network)
```

Artwork is never bundled: box scans are streamed from the credited source and cached under
`%LOCALAPPDATA%\WrestlingGameMuseum\cache`.
