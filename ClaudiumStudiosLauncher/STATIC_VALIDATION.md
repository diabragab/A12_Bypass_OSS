# Claudium Studios Launcher Static Validation

This repository was statically validated in an environment where `dotnet` reports `command not found`.
That is an environment limitation, not an application failure.

Before release, run the final build and smoke test on Windows with:

- .NET 8 SDK
- Windows App SDK / WinUI 3 workload support
- Visual Studio 2022 or equivalent Windows build tools

Recommended final validation commands:

```powershell
dotnet restore ClaudiumStudiosLauncher.sln
dotnet build ClaudiumStudiosLauncher.sln -c Release
```

Static checks performed without the SDK:

- Project structure inspection.
- C# review for missing references and obvious compile-time issues.
- XAML XML syntax validation.
- Navigation route review.
- DI registration review.
- Localization JSON parse review for English and Arabic resources.
- Theme resource dictionary review.
- Configuration JSON parse review.
- Branding scan to ensure only the official Claudium Studios identity is used in launcher files.
- Architecture review for data-driven games, installation, update, repair, downloads, launching, server status, authentication, offline mode, settings, and localization.


Premium UI pass added:

- Custom integrated title bar and launcher chrome.
- Layered cinematic dark background with ambient glow, particles, beam lighting, and vignette overlay.
- Floating sidebar navigation using reusable themed styles.
- Premium button, glass button, danger button, card, hero, store, download, news, event, login, settings, and game-detail treatments.
- Frame navigation transition using WinUI navigation transition APIs.

Cinematic home slider pass added:

- Data-driven Home slider binds to `Games` and `SelectedGame` from `ShellViewModel` / `GameService`.
- Slider autoplay interval is configurable through `Slider:IntervalSeconds` in `appsettings.json`.
- Previous, next, indicator, hover-pause, keyboard navigation, empty-state, and primary action state plumbing were statically reviewed.
