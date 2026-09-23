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

The routine build is incremental: it retains project `bin/obj` directories and removes only installed runtime plugin copies before building. MSBuild follows project references, including Contracts changes. Missing plugin outputs and plugins omitted from the solution receive an incremental direct-project build. Plugin installation continues to exclude plugin-local `Dynastia.Contracts.dll` copies.

Unless `-SkipTests` is supplied, the build runs the repository-tooling regression checks and every `*Tests.csproj` under `tests/`, sorted by full path. Discovered test projects may build/restore even when they are not yet listed in the solution.

For authoritative clean verification, including after applying a refactoring patch:

```powershell
powershell -ExecutionPolicy Bypass -File build\build-dev.ps1 -Clean
```

`-Clean` removes repository build-output directories before the same build, tests and installation. It preserves source, data, Git metadata and saves. `-SkipTests` is for iteration, not final verification.

## Repository cleanup

Optional local cleanup of generated development output:

```powershell
powershell -ExecutionPolicy Bypass -File build\cleanup-repo.ps1
```

Cleanup is not a prerequisite for a build or source package. It removes build-output directories and generated diagnostics/user/temporary files, reports removal counts, and preserves source, gameplay data, documentation, Git metadata and saves. Add `-IncludeArchives` to delete local ZIPs as well; ZIPs inside saves and Git metadata remain protected.

## Source-only handoff

For a clean source archive:

```powershell
powershell -ExecutionPolicy Bypass -File build\pack-source.ps1
```

Packaging uses `build/repository-artifact-policy.ps1`, shared with cleanup and clean builds. It excludes Git/IDE metadata, build/publish/benchmark outputs, test results, releases, logs, saves, package caches, generated diagnostics and existing ZIPs. Shared `.vscode` settings allowed by `.gitignore` remain included.

The staged tree is validated for forbidden entries and required repository files/directories before compression. File count and byte size are reported without imposing an archive-size cap. Source, plugins, data, tests, docs and build scripts are retained, including legitimate hidden files such as `.gitignore`. The default output is `<repository-name>-source.zip` beside the repository; `-OutputPath` accepts a custom ZIP path.

The dependency-free tooling checks can also run on their own:

```powershell
powershell -ExecutionPolicy Bypass -File build\test-repository-tooling.ps1
```

These checks exercise cleanup and packaging on temporary fixtures, verify `.gitignore` policy coverage, reject injected artifacts, and check build modes and failure handling using a mocked `dotnet` command. They do not replace a real clean build and gameplay test run. The scripts support Windows PowerShell 5.1 and PowerShell 7 (`pwsh`).

## Development rule

Keep mechanics in their owning plugin and keep presentation code free of simulation rules wherever possible. For maintenance refactors, separate responsibilities without changing public contracts, action IDs, plugin IDs, save fields or year-system ordering in the same pass.
