# Project Structure Aligned with Tabular (#3)

**Date:** 2026-10-03
**Issue:** #3 (Evaluate project structure improvements)
**Public API impact:** none (no source changes in the library; the package content stays the same)

## Goal

Templify and Tabular (`TriasDev/tabular`) are both TriasDev open source .NET libraries. They should have the same
repository layout, so that a contributor who knows one knows the other:

- projects grouped in `src/`, `tests/`, `benchmarks/`, `samples/` (plus `tools/` in Templify);
- an `.slnx` solution with solution folders that mirror those directories;
- a root `Directory.Build.props` with shared settings, and `src/` / `tests/` props that import it and add the
  settings of their group.

## Non-Goals

Build tooling stays as it is; aligning it is separate work (own issues if wanted):

- Central Package Management and `packages.lock.json` stay (Tabular has neither).
- Tests keep running through VSTest (`xunit.v3.mtp-off`); no switch to Microsoft.Testing.Platform.
- No new analyzers (Tabular uses Roslynator and SonarAnalyzer).
- No change in Tabular.
- No change of target frameworks, package metadata values, the package ID or namespaces.

## Layout

| Today | After |
|---|---|
| `TriasDev.Templify/` | `src/TriasDev.Templify/` |
| `TriasDev.Templify.Tests/` | `tests/TriasDev.Templify.Tests/` |
| `TriasDev.Templify.Tools.Tests/` | `tests/TriasDev.Templify.Tools.Tests/` |
| `TriasDev.Templify.Converter.Tests/` | `tests/TriasDev.Templify.Converter.Tests/` |
| `TriasDev.Templify.Benchmarks/` | `benchmarks/TriasDev.Templify.Benchmarks/` |
| `TriasDev.Templify.Demo/` | `samples/TriasDev.Templify.Demo/` |
| `TriasDev.Templify.Converter/` | `tools/TriasDev.Templify.Converter/` |
| `TriasDev.Templify.Gui/` | `tools/TriasDev.Templify.Gui/` |
| `TriasDev.Templify.DocumentGenerator/` | `tools/TriasDev.Templify.DocumentGenerator/` |
| `templify.sln` | `templify.slnx` |

`src/` holds only what ships to NuGet, as in Tabular. The applications that are not packages (Converter CLI, Avalonia
GUI, documentation generator) go to `tools/`, which Tabular does not need.

These stay at the root: `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `.editorconfig`,
`assets/`, `docs/`, `examples/`, `scripts/`, `mkdocs.yml`, `requirements.txt`, release-please files and the community
files (README, CHANGELOG, CONTRIBUTING, ...). Each `packages.lock.json` moves with its project.

Moves use `git mv` so that history follows the files (`git log --follow`, GitHub rename view).

## Solution

`templify.sln` is replaced by `templify.slnx` (`dotnet sln migrate`; supported by SDK 10, Visual Studio 2022 17.13+,
Rider 2024.3+). Solution folders: `/src/`, `/tests/`, `/benchmarks/`, `/samples/`, `/tools/`, each listing the
projects of that directory. `templify.sln` is deleted; the untracked `templify.sln.DotSettings.user` is the
developer's own file and is not touched.

## Build Props

**Root `Directory.Build.props`:** unchanged content (Nullable, ImplicitUsings, LangVersion, AnalysisLevel,
EnforceCodeStyleInBuild, Deterministic, nullable as errors, lock files, CI settings). The header comment is updated to
describe the layered props.

**`src/Directory.Build.props`** (new) imports the root file
(`$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))`) and takes from
`TriasDev.Templify.csproj` what every shipped package carries:

- `GenerateDocumentationFile`;
- `Authors`, `Company`, `Copyright`, `PackageLicenseExpression`, `PackageProjectUrl`, `RepositoryUrl`,
  `RepositoryType`, `PackageReadmeFile`, `PackageIcon`;
- symbols and Source Link: `IncludeSymbols`, `SymbolPackageFormat`, `PublishRepositoryUrl`, `EmbedUntrackedSources`;
- the public API guard: `PublicApiAnalyzers` package reference, RS0016/RS0017 as errors, `EnablePackageValidation`;
- the icon item (`$(MSBuildThisFileDirectory)../assets/icon-128.png` packed as `icon.png`).

These stay in `TriasDev.Templify.csproj`, because they are specific to this package or read by tools:

- `Version` (release-please updates it through `extra-files` with the XPath `//Project/PropertyGroup/Version`);
- `PackageValidationBaselineVersion` (bumped after each release, see CONTRIBUTING);
- `TargetFrameworks`, `PackageId`, `Description`, `PackageTags`, `NoWarn` RS0026/RS0027 (with its comment);
- `DocumentFormat.OpenXml`, `InternalsVisibleTo`, the embedded resource, the README item.

**`tests/Directory.Build.props`** (new) imports the root file and holds what every test project repeats today:

- `IsPackable=false`, `OutputType=Exe` (with the xunit.v3 comment);
- package references `coverlet.collector`, `Microsoft.NET.Test.Sdk`, `xunit.v3.mtp-off`,
  `xunit.runner.visualstudio` (versions stay in `Directory.Packages.props`);
- `<Using Include="Xunit" />`.

Test csproj files keep their target frameworks, project references and project-specific items (ODT fixtures).

`tools/`, `benchmarks/` and `samples/` get no props of their own (as in Tabular); they inherit the root file.

## References to Update

Build and release:
- `.github/workflows/ci.yml`: `dotnet pack src/TriasDev.Templify/...`; the root `dotnet restore/build/test` commands
  find `templify.slnx` without changes.
- `.github/workflows/release-please.yml`: build, test and pack paths.
- `.github/workflows/codeql.yml`, `build-sonarqube.yml`: `templify.sln` → `templify.slnx`.
- `.github/workflows/documentation.yml`: no change (its trigger paths are `docs/**`, `mkdocs.yml`, `requirements.txt`).
- `release-please-config.json`: `extra-files` path → `src/TriasDev.Templify/TriasDev.Templify.csproj`.
- `.github/dependabot.yml`: nuget `directory: "/"` stays (Central Package Management lives at the root); verified
  by the Dependabot run after merge.
- `.gitignore`: `TriasDev.Templify.Demo/Data/` and `/output/` → `samples/TriasDev.Templify.Demo/...`.

Code:
- `tools/TriasDev.Templify.DocumentGenerator/RepositoryPaths.cs` and `Program.cs`: the repository root is the
  directory with `templify.slnx` and `examples/` (message and doc comment updated).
- `tests/TriasDev.Templify.Tools.Tests/DocumentGenerator/ExampleGeneratorSmokeTests.cs`: asserts `templify.slnx`.
- Relative `ProjectReference` paths: `..\X\X.csproj` becomes `..\..\<group>\X\X.csproj` where groups differ.

Scripts:
- `scripts/*.sh` and `scripts/*.cmd`: Converter project path → `tools/TriasDev.Templify.Converter/...`.

Documentation:
- `CLAUDE.md`, `CONTRIBUTING.md`, `README.md`, and in `src/TriasDev.Templify/` the package `README.md`,
  `ARCHITECTURE.md`, `Examples.md` and `PERFORMANCE.md`, `scripts/README.md`, `examples/README.md`, `docs/**` (outside
  `docs/archive/`, which is historical and stays as is), `.github/ISSUE_TEMPLATE/question.yml`: project paths,
  commands and `github.com/TriasDev/templify/blob|tree/main/<old path>` links.
- Earlier specs and plans in `docs/superpowers/` stay unchanged: they describe the repository at their date.

External links to old paths (for example to `TriasDev.Templify/Examples.md` on GitHub) break, because GitHub does
not redirect moved files. The NuGet package README uses absolute links; they are updated, and the package page on
NuGet changes with the next release.

## Commits

One pull request with logical commits, so that `main` never has a half-moved layout:

1. `git mv` of all projects, with the `ProjectReference`, workflow, script, `.gitignore`, release-please and code
   path updates needed to build and test;
2. `templify.sln` → `templify.slnx` (with the `RepositoryPaths` / smoke test and workflow updates);
3. `src/` and `tests/` `Directory.Build.props`, with the moved settings removed from the csproj files;
4. documentation updates.

## Verification

- `dotnet build templify.slnx -c Release -p:ContinuousIntegrationBuild=true`: 0 warnings, 0 errors.
- `dotnet test` of all test projects (core tests on net10.0, net9.0, net8.0): same test count as on `main`, all pass.
- `dotnet format --verify-no-changes --no-restore`: clean.
- `dotnet restore --locked-mode`: lock files still valid.
- Package equivalence: `dotnet pack` before and after the change; the `.nupkg` and `.snupkg` contain the same files,
  and the `.nuspec` metadata is the same (except the repository commit). Package validation against 1.9.0 passes.
- `scripts/analyze.sh`, `convert.sh`, `validate.sh`, `clean.sh` run against a sample document.
- `dotnet run --project tools/TriasDev.Templify.DocumentGenerator -- --skip-images` finds the repository root and
  produces no changes in `examples/`.
- `mkdocs build --strict` passes; no link to a moved path is left (`git grep` for the old project paths outside
  `docs/archive/` and `docs/superpowers/`).
- LibreOffice round trips: `dotnet test tests/TriasDev.Templify.Tests -c Release -f net10.0 --filter
  "Category=LibreOffice"`, all passed, 0 skipped (the shared engine project moves).
- CI on the pull request is green, including Pack NuGet, CodeQL and SonarQube.

## Risks

- **Release pipeline:** a wrong path in `release-please.yml` or `release-please-config.json` only shows at the next
  release. Mitigation: review both against the new layout; CI's Pack NuGet job builds the same project.
- **Open branches:** branches made before the move conflict on rebase. Today no pull request is open.
- **Developer IDE state:** local `.idea/`, `.vs/` and `bin/obj` folders under the old paths become stale; delete them
  after pulling.
