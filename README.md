# Dynastia

Dynastia is a modular C#/.NET desktop family-dynasty simulation built with Avalonia.

The simulation is organized around a small core and dynamically loaded mechanics plugins. Gameplay state and time belong to the core; individual systems such as households, careers, health, relationships, inheritance, loans and thoughts live in `plugins/` and communicate through contracts, components, tags, events and the ordered year pipeline.

## Current baseline

- .NET 10
- Avalonia desktop application
- selectable New Game start year from 1700 to 2000 in 10-year steps
- dynamically loaded mechanics plugins
- JSON/CSV gameplay data
- versioned save/load support
- deterministic game RNG infrastructure

Existing saves keep their stored year and state. New historical mechanics should be layered onto the current year-aware systems rather than reintroducing fixed 1900 assumptions.

## Repository layout

```text
src/
  Dynastia.App/          Avalonia application and presentation layer
  Dynastia.Contracts/    Shared interfaces and DTOs used by core/plugins
  Dynastia.Core/         Simulation state, actions, events and year processing
  Dynastia.PluginHost/   Runtime plugin discovery/loading

plugins/                 Mechanics plugin projects
data/                    Shared names, careers, towns and common data
tests/                   Automated regression tests
build/                   Development, cleanup and source-packaging scripts
docs/                    Maintenance/development maps
```

For gameplay/design rules, start with `docs/GameDesign/00-Index.md`. For the current implementation responsibility map, see `docs/DevelopmentMap.md`.

## Development build

From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File build\build-dev.ps1
```

Then run:

```powershell
dotnet run --project src\Dynastia.App\Dynastia.App.csproj
```

The development build removes stale dynamically loaded plugin binaries, builds the solution, runs tests unless `-SkipTests` is supplied, and installs fresh plugin outputs into the app build directory.

## Repository cleanup

Generated development output can be removed with:

```powershell
powershell -ExecutionPolicy Bypass -File build\cleanup-repo.ps1
```

This only removes generated/local development artifacts. It does not delete source, gameplay data, documentation or saves.

## Source-only handoff

For a clean source archive:

```powershell
powershell -ExecutionPolicy Bypass -File build\pack-source.ps1
```

The archive excludes Git metadata, build outputs, logs, saves and existing archives.

## Development rule

Keep mechanics in their owning plugin and keep presentation code free of simulation rules wherever possible. For maintenance refactors, separate responsibilities without changing public contracts, action IDs, plugin IDs, save fields or year-system ordering in the same pass.
