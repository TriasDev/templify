# Project Structure Aligned with Tabular Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move Templify's projects into `src/`, `tests/`, `benchmarks/`, `samples/` and `tools/`, replace
`templify.sln` with `templify.slnx`, and split the build props into root, `src/` and `tests/` files, without changing
the shipped package.

**Architecture:** Pure repository restructuring in one pull request with four logical commits (move → slnx → props →
docs). No library source changes. Every commit builds and passes the tests on its own.

**Tech Stack:** .NET SDK 10 (`global.json` 10.0.100), MSBuild `Directory.Build.props`, Central Package Management,
xUnit v3 through VSTest, GitHub Actions, release-please, MkDocs.

**Spec:** `docs/superpowers/specs/2026-10-03-project-structure-design.md`

## Global Constraints

- Branch: `chore/project-structure` (the spec is already committed there).
- Public API impact: none. No `.cs` file in `src/TriasDev.Templify` changes; `PublicAPI.*.txt` untouched.
- Package content must stay the same: same files in `.nupkg`/`.snupkg`, same `.nuspec` metadata (except the
  repository commit).
- Moves use `git mv` only (history must follow).
- Out of scope: Central Package Management, `packages.lock.json`, VSTest vs MTP, analyzers, Tabular, target
  frameworks, package ID, namespaces.
- `Version` and `PackageValidationBaselineVersion` stay in `src/TriasDev.Templify/TriasDev.Templify.csproj`.
- `docs/archive/**` and `docs/superpowers/**` (other than this plan and its spec) are not edited.
- Run all commands from the repository root `/Users/vaceslavustinov/Documents/work/triasdev/templify`.
- Scratch files (baseline packages, test logs) go to
  `/private/tmp/claude-501/-Users-vaceslavustinov-Documents-work-triasdev-templify/10f81cd0-4b6d-459a-b6a1-298912a1a9f2/scratchpad`
  (written below as `$SCRATCH`; set `SCRATCH=<that path>` in each shell).

## Review Focus

1. Release pipeline paths (`release-please.yml`, `release-please-config.json`): a wrong path only fails at the next
   release → Task 2 Step 6 checks every path in both files exists.
2. Package drift (README, icon, symbols, Source Link lost after moving settings to `src/Directory.Build.props`) →
   Task 4 Step 5 compares the package file list and nuspec against the baseline from Task 1.
3. Windows scripts (`scripts/*.cmd`) use backslash paths a `/`-based grep misses → Task 2 Step 5 edits them
   explicitly and Step 6 checks the referenced project exists.
4. Test count silently dropping (a test project missing from the solution is just not run) → Task 1 records the
   counts, Task 6 Step 2 compares them.
5. DocumentGenerator run from a new working directory no longer finding the repository root → Task 3 test plus
   Task 6 Step 5 runs the generator.

---

### Task 1: Baseline

**Files:** none in the repository (scratch only).

**Interfaces:**
- Produces: `$SCRATCH/baseline/` with `TriasDev.Templify.1.9.0.nupkg`, `.snupkg`, `files.txt`, `nuspec.xml`,
  `tests.txt` (per-project test counts) for Tasks 4 and 6.

- [ ] **Step 1: Pack the current layout**

```bash
SCRATCH=/private/tmp/claude-501/-Users-vaceslavustinov-Documents-work-triasdev-templify/10f81cd0-4b6d-459a-b6a1-298912a1a9f2/scratchpad
mkdir -p $SCRATCH/baseline
dotnet pack TriasDev.Templify/TriasDev.Templify.csproj -c Release -p:ContinuousIntegrationBuild=true -o $SCRATCH/baseline
unzip -l $SCRATCH/baseline/TriasDev.Templify.1.9.0.nupkg | awk 'NR>3 {print $4}' | grep -v '^$' | sort > $SCRATCH/baseline/files.txt
unzip -l $SCRATCH/baseline/TriasDev.Templify.1.9.0.snupkg | awk 'NR>3 {print $4}' | grep -v '^$' | sort > $SCRATCH/baseline/symbols.txt
unzip -p $SCRATCH/baseline/TriasDev.Templify.1.9.0.nupkg TriasDev.Templify.nuspec > $SCRATCH/baseline/nuspec.xml
```

Expected: pack succeeds (package validation against 1.9.0 passes); `files.txt` lists `README.md`, `icon.png`,
`lib/net8.0/...`, `lib/net9.0/...`, `lib/net10.0/...`.

- [ ] **Step 2: Record test counts**

```bash
dotnet test templify.sln -c Release 2>&1 | grep -E "^(Passed|Failed)!" | sed -E 's/Duration: [^-]+- //' | sort > $SCRATCH/baseline/tests.txt
cat $SCRATCH/baseline/tests.txt
```

Expected: one `Passed!` line per test assembly and TFM (Templify.Tests ×3, Tools.Tests, Converter.Tests), 0 failed.
LibreOffice tests show as skipped only if `soffice` is missing; note the numbers.

---

### Task 2: Move the projects

**Files:**
- Move (git mv): the nine project directories (table below)
- Modify: every moved `*.csproj` with a `ProjectReference`, `templify.sln`, `.github/workflows/ci.yml:166`,
  `.github/workflows/release-please.yml:61,67,73,93`, `release-please-config.json`, `.gitignore:338,350`,
  `scripts/{analyze,clean,convert,validate}.{sh,cmd}`,
  `src/TriasDev.Templify/TriasDev.Templify.csproj` (icon path)

**Interfaces:**
- Produces: the new layout used by every later task:

| Old | New |
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

- [ ] **Step 1: Remove stale build output, then move**

Untracked `bin/`/`obj/` folders would otherwise stay behind in the old directories.

```bash
git clean -fdX -- 'TriasDev.Templify*/bin' 'TriasDev.Templify*/obj'
mkdir -p src tests benchmarks samples tools
git mv TriasDev.Templify src/TriasDev.Templify
git mv TriasDev.Templify.Tests tests/TriasDev.Templify.Tests
git mv TriasDev.Templify.Tools.Tests tests/TriasDev.Templify.Tools.Tests
git mv TriasDev.Templify.Converter.Tests tests/TriasDev.Templify.Converter.Tests
git mv TriasDev.Templify.Benchmarks benchmarks/TriasDev.Templify.Benchmarks
git mv TriasDev.Templify.Demo samples/TriasDev.Templify.Demo
git mv TriasDev.Templify.Converter tools/TriasDev.Templify.Converter
git mv TriasDev.Templify.Gui tools/TriasDev.Templify.Gui
git mv TriasDev.Templify.DocumentGenerator tools/TriasDev.Templify.DocumentGenerator
ls TriasDev.Templify* 2>&1   # expected: no such file (remaining untracked files, e.g. Demo/output, move by hand)
```

If `ls` shows leftover untracked directories (ignored files such as `samples` demo output or `.env`), move their
contents with plain `mv` into the new directory and delete the empty old directory.

- [ ] **Step 2: Fix project references**

Every moved project referencing another group needs `..\..\<group>\`:

```bash
for f in benchmarks/*/*.csproj samples/*/*.csproj tools/*/*.csproj tests/TriasDev.Templify.Tests/*.csproj tests/TriasDev.Templify.Converter.Tests/*.csproj; do
  sed -i '' 's#Include="\.\.\\TriasDev\.Templify\\TriasDev\.Templify\.csproj"#Include="..\\..\\src\\TriasDev.Templify\\TriasDev.Templify.csproj"#' "$f"
done
sed -i '' 's#Include="\.\.\\TriasDev\.Templify\.Converter\\#Include="..\\..\\tools\\TriasDev.Templify.Converter\\#' tests/TriasDev.Templify.Converter.Tests/TriasDev.Templify.Converter.Tests.csproj
sed -i '' -e 's#Include="\.\.\\TriasDev\.Templify\.Gui\\#Include="..\\..\\tools\\TriasDev.Templify.Gui\\#' \
          -e 's#Include="\.\.\\TriasDev\.Templify\.DocumentGenerator\\#Include="..\\..\\tools\\TriasDev.Templify.DocumentGenerator\\#' \
          tests/TriasDev.Templify.Tools.Tests/TriasDev.Templify.Tools.Tests.csproj
sed -i '' 's#Include="\.\.\\assets\\icon-128\.png"#Include="..\\..\\assets\\icon-128.png"#' src/TriasDev.Templify/TriasDev.Templify.csproj
git grep -n 'ProjectReference\|assets' -- '*.csproj'
```

Expected: every `ProjectReference` starts with `..\..\src\` or `..\..\tools\`; the icon path is `..\..\assets\`.

- [ ] **Step 3: Update `templify.sln` project paths**

```bash
sed -i '' -E \
  -e 's#"TriasDev\.Templify\\TriasDev\.Templify\.csproj"#"src\\TriasDev.Templify\\TriasDev.Templify.csproj"#' \
  -e 's#"(TriasDev\.Templify\.(Tests|Tools\.Tests|Converter\.Tests))\\#"tests\\\1\\#' \
  -e 's#"(TriasDev\.Templify\.Benchmarks)\\#"benchmarks\\\1\\#' \
  -e 's#"(TriasDev\.Templify\.Demo)\\#"samples\\\1\\#' \
  -e 's#"(TriasDev\.Templify\.(Converter|Gui|DocumentGenerator))\\#"tools\\\1\\#' \
  templify.sln
grep -n '\.csproj' templify.sln
```

Expected: nine project lines, each with its group prefix.

- [ ] **Step 4: Update CI, release and ignore paths**

```bash
sed -i '' 's#dotnet pack TriasDev\.Templify/TriasDev\.Templify\.csproj#dotnet pack src/TriasDev.Templify/TriasDev.Templify.csproj#' .github/workflows/ci.yml
sed -i '' -e 's# TriasDev\.Templify/TriasDev\.Templify\.csproj# src/TriasDev.Templify/TriasDev.Templify.csproj#' \
          -e 's# TriasDev\.Templify\.Tests/TriasDev\.Templify\.Tests\.csproj# tests/TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj#' \
          .github/workflows/release-please.yml
sed -i '' 's#"path": "TriasDev\.Templify/TriasDev\.Templify\.csproj"#"path": "src/TriasDev.Templify/TriasDev.Templify.csproj"#' release-please-config.json
sed -i '' 's#^TriasDev\.Templify\.Demo/#samples/TriasDev.Templify.Demo/#' .gitignore
```

- [ ] **Step 5: Update converter scripts**

```bash
sed -i '' 's#\$SCRIPT_DIR/\.\./TriasDev\.Templify\.Converter/#$SCRIPT_DIR/../tools/TriasDev.Templify.Converter/#' scripts/*.sh
sed -i '' 's#%~dp0\.\.\\TriasDev\.Templify\.Converter\\#%~dp0..\\tools\\TriasDev.Templify.Converter\\#' scripts/*.cmd
grep -n 'Converter' scripts/*.sh scripts/*.cmd | grep -- '--project\|dotnet run'
```

Expected: all eight scripts point to `../tools/TriasDev.Templify.Converter/` (`..\tools\...` in `.cmd`).

- [ ] **Step 6: Check that every referenced path exists**

```bash
for p in $(grep -ohE '(src|tests|tools)/TriasDev\.Templify[A-Za-z.]*/[A-Za-z.]+\.csproj' .github/workflows/*.yml release-please-config.json scripts/*.sh | sort -u) \
         $(grep -ohE '(src|tests|benchmarks|samples|tools)\\TriasDev\.Templify[A-Za-z.]*\\[A-Za-z.]+\.csproj' templify.sln scripts/*.cmd | tr '\\' '/' | sort -u); do
  test -f "$p" && echo "ok  $p" || echo "MISSING $p"
done
```

Expected: only `ok` lines.

- [ ] **Step 7: Build and test**

```bash
dotnet restore templify.sln --locked-mode
dotnet build templify.sln -c Release -p:ContinuousIntegrationBuild=true 2>&1 | tail -3
dotnet test templify.sln -c Release --no-build 2>&1 | grep -E "^(Passed|Failed)!"
```

Expected: restore succeeds (lock files unchanged), 0 warnings / 0 errors, same counts as `$SCRATCH/baseline/tests.txt`.

- [ ] **Step 8: Commit**

```bash
git add -A
git status --short | grep -v '^R ' | head -30   # expected: only the modified files from Steps 2-5
git commit -m "chore: move projects into src/, tests/, benchmarks/, samples/ and tools/ (#3)"
```

---

### Task 3: Replace `templify.sln` with `templify.slnx`

**Files:**
- Create: `templify.slnx`
- Delete: `templify.sln`
- Modify: `tools/TriasDev.Templify.DocumentGenerator/RepositoryPaths.cs:9,27`,
  `tools/TriasDev.Templify.DocumentGenerator/Program.cs:12`,
  `tests/TriasDev.Templify.Tools.Tests/DocumentGenerator/ExampleGeneratorSmokeTests.cs:204`,
  `.github/workflows/codeql.yml:46`, `.github/workflows/build-sonarqube.yml:75`

**Interfaces:**
- Consumes: layout from Task 2.
- Produces: `templify.slnx` with solution folders `/src/`, `/tests/`, `/benchmarks/`, `/samples/`, `/tools/`;
  `RepositoryPaths.FindRepositoryRoot(string)` keys on `templify.slnx` + `examples/`.

- [ ] **Step 1: Change the smoke test to expect `templify.slnx`**

In `tests/TriasDev.Templify.Tools.Tests/DocumentGenerator/ExampleGeneratorSmokeTests.cs` line 204:

```csharp
        Assert.True(File.Exists(Path.Combine(root, "templify.slnx")));
```

- [ ] **Step 2: Create the solution and remove the old one**

`dotnet sln add` puts each project into solution folders that mirror its directory.

```bash
dotnet new sln --format slnx --name templify
for p in src/*/*.csproj tests/*/*.csproj benchmarks/*/*.csproj samples/*/*.csproj tools/*/*.csproj; do dotnet sln templify.slnx add "$p"; done
git rm -q templify.sln
cat templify.slnx
```

Expected: five `<Folder Name="/.../">` elements (`/benchmarks/`, `/samples/`, `/src/`, `/tests/`, `/tools/`) holding
nine `<Project Path=...>` entries. If `dotnet sln add` nests an extra folder per project (e.g. `/src/TriasDev.Templify/`),
edit the file by hand to the flat shape:

```xml
<Solution>
  <Folder Name="/benchmarks/">
    <Project Path="benchmarks/TriasDev.Templify.Benchmarks/TriasDev.Templify.Benchmarks.csproj" />
  </Folder>
  <Folder Name="/samples/">
    <Project Path="samples/TriasDev.Templify.Demo/TriasDev.Templify.Demo.csproj" />
  </Folder>
  <Folder Name="/src/">
    <Project Path="src/TriasDev.Templify/TriasDev.Templify.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/TriasDev.Templify.Converter.Tests/TriasDev.Templify.Converter.Tests.csproj" />
    <Project Path="tests/TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj" />
    <Project Path="tests/TriasDev.Templify.Tools.Tests/TriasDev.Templify.Tools.Tests.csproj" />
  </Folder>
  <Folder Name="/tools/">
    <Project Path="tools/TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj" />
    <Project Path="tools/TriasDev.Templify.DocumentGenerator/TriasDev.Templify.DocumentGenerator.csproj" />
    <Project Path="tools/TriasDev.Templify.Gui/TriasDev.Templify.Gui.csproj" />
  </Folder>
</Solution>
```

- [ ] **Step 3: Run the smoke test to see it fail**

```bash
dotnet test tests/TriasDev.Templify.Tools.Tests --filter "FullyQualifiedName~ExampleGeneratorSmokeTests" 2>&1 | grep -E "Failed|Passed!"
```

Expected: FAIL — `FindRepositoryRoot` still looks for `templify.sln`, so `root` is null or the assert fails.

- [ ] **Step 4: Update `RepositoryPaths` and the generator message**

`tools/TriasDev.Templify.DocumentGenerator/RepositoryPaths.cs`:

```csharp
    /// Finds the repository root (the directory containing <c>templify.slnx</c> and <c>examples/</c>).
```

```csharp
            if (File.Exists(Path.Combine(dir.FullName, "templify.slnx"))
```

`tools/TriasDev.Templify.DocumentGenerator/Program.cs` line 12:

```csharp
    Console.WriteLine("Error: Could not find the repository root (directory containing templify.slnx and examples/).");
```

- [ ] **Step 5: Run the smoke test to see it pass**

```bash
dotnet test tests/TriasDev.Templify.Tools.Tests --filter "FullyQualifiedName~ExampleGeneratorSmokeTests" 2>&1 | grep -E "Failed|Passed!"
```

Expected: `Passed!`, 0 failed.

- [ ] **Step 6: Update workflows**

```bash
sed -i '' 's#templify\.sln#templify.slnx#' .github/workflows/codeql.yml .github/workflows/build-sonarqube.yml
git grep -nP 'templify\.sln(?!x)' -- .github
```

Expected: both build lines read `templify.slnx`; no `templify.sln` without `x` remains in `.github/`.

- [ ] **Step 7: Build and test through the new solution**

```bash
dotnet restore --locked-mode
dotnet build -c Release -p:ContinuousIntegrationBuild=true 2>&1 | tail -3
dotnet test -c Release --no-build 2>&1 | grep -E "^(Passed|Failed)!"
```

Expected: the root commands pick up `templify.slnx` (the only solution file); 0 warnings / 0 errors; counts equal the
baseline.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "chore: replace templify.sln with templify.slnx with solution folders (#3)"
```

---

### Task 4: Layered build props

**Files:**
- Modify: `Directory.Build.props:2-3` (header comment)
- Create: `src/Directory.Build.props`, `tests/Directory.Build.props`
- Modify: `src/TriasDev.Templify/TriasDev.Templify.csproj`, `tests/*/*.csproj` (three files)

**Interfaces:**
- Consumes: baseline from Task 1 (`$SCRATCH/baseline/files.txt`, `symbols.txt`, `nuspec.xml`).
- Produces: `src/Directory.Build.props` and `tests/Directory.Build.props`, each importing the root file.

- [ ] **Step 1: Update the root header comment**

`Directory.Build.props` lines 2-3 become:

```xml
  <!-- Settings every project in the repository shares. src/ and tests/ each add their own on top through a
       Directory.Build.props that imports this one; tools/, benchmarks/ and samples/ use this file alone.
       Project-specific settings (target frameworks, package identity) stay in the individual .csproj files. -->
```

- [ ] **Step 2: Create `src/Directory.Build.props`**

```xml
<Project>

  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />

  <!-- What every shipped package carries. Package identity (PackageId, Version, Description, tags) and the
       validation baseline stay in the project file. -->
  <PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <Authors>TriasDev GmbH &amp; Co. KG</Authors>
    <Company>TriasDev GmbH &amp; Co. KG</Company>
    <Copyright>Copyright © TriasDev GmbH &amp; Co. KG</Copyright>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/TriasDev/templify</PackageProjectUrl>
    <RepositoryUrl>https://github.com/TriasDev/templify</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageIcon>icon.png</PackageIcon>
  </PropertyGroup>

  <!-- Symbols and Source Link -->
  <PropertyGroup>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
  </PropertyGroup>

  <!-- Public API guard: PublicAPI.Shipped.txt / PublicAPI.Unshipped.txt (RS0016: public symbol not declared;
       RS0017: declared symbol removed/changed), and package validation against the last released version
       (PackageValidationBaselineVersion in the project file). See CONTRIBUTING.md -> "Public API Compatibility". -->
  <PropertyGroup>
    <WarningsAsErrors>$(WarningsAsErrors);RS0016;RS0017</WarningsAsErrors>
    <EnablePackageValidation>true</EnablePackageValidation>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <None Include="$(MSBuildThisFileDirectory)../assets/icon-128.png" Pack="true" PackagePath="icon.png" Visible="false" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Remove the moved settings from the library project**

Edit `src/TriasDev.Templify/TriasDev.Templify.csproj` by deleting elements (keep `Description` and `PackageTags`
exactly as they are, keep the UTF-8 BOM). Delete: `GenerateDocumentationFile` with its comment, the RS0016/RS0017
`WarningsAsErrors` line with its comment, `Authors`, `Company`, `Copyright`, `PackageLicenseExpression`,
`PackageProjectUrl`, `RepositoryUrl`, `RepositoryType`, `PackageReadmeFile`, `PackageIcon`, `EnablePackageValidation`,
the "Symbols and Source Link" block, the `PublicApiAnalyzers` package reference with its comment, and the icon
`None` item. Replace the `<!-- NuGet Package Metadata -->` comment with
`<!-- NuGet package identity; shared package metadata is in src/Directory.Build.props -->`.
The result has this shape (`Description` and `PackageTags` shortened here only):

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <!-- Supported TFMs: .NET versions in Microsoft support. EOL TFMs are dropped in a minor release
         with a release-notes notice (net6.0 dropped in 1.8.0, see #154). -->
    <TargetFrameworks>net10.0;net9.0;net8.0</TargetFrameworks>

    <!-- RS0026/RS0027: overload design advice for already-shipped API; changing it would be breaking. -->
    <NoWarn>$(NoWarn);RS0026;RS0027</NoWarn>

    <!-- NuGet package identity; shared package metadata is in src/Directory.Build.props -->
    <PackageId>TriasDev.Templify</PackageId>
    <Version>1.9.0</Version>
    <Description>High-performance templating engine for .NET. ...</Description>
    <PackageTags>word;docx;odt;...</PackageTags>

    <!-- Public API guard: compare the packed API against the last released version.
         Any breaking change fails `dotnet pack`. Bump the baseline after each release. -->
    <PackageValidationBaselineVersion>1.9.0</PackageValidationBaselineVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="DocumentFormat.OpenXml"/>
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="TriasDev.Templify.Tests"/>
  </ItemGroup>

  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\"/>
  </ItemGroup>

  <ItemGroup>
    <EmbeddedResource Include="Resources\WarningReportTemplate.docx"/>
  </ItemGroup>

</Project>
```


- [ ] **Step 4: Create `tests/Directory.Build.props` and slim the test projects**

```xml
<Project>

  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />

  <!-- What every test project runs on: xunit.v3 through VSTest (xunit.v3.mtp-off). -->
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <!-- xunit.v3: test projects are executables. -->
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit.v3.mtp-off" />
    <PackageReference Include="xunit.runner.visualstudio">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
```

In each of the three test csproj files delete `<IsPackable>`, `<OutputType>` with its comment, the `ItemGroup` with
the four package references, and the `ItemGroup` with `<Using Include="Xunit" />`. What remains, e.g.
`tests/TriasDev.Templify.Converter.Tests/TriasDev.Templify.Converter.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\TriasDev.Templify\TriasDev.Templify.csproj" />
    <ProjectReference Include="..\..\tools\TriasDev.Templify.Converter\TriasDev.Templify.Converter.csproj" />
  </ItemGroup>

</Project>
```

`tests/TriasDev.Templify.Tests` keeps its `TargetFrameworks` comment and line, the ODT fixtures `ItemGroup` and its
project reference; `tests/TriasDev.Templify.Tools.Tests` keeps `TargetFramework` and its two project references.

- [ ] **Step 5: Verify restore, build, tests and package equivalence**

```bash
SCRATCH=/private/tmp/claude-501/-Users-vaceslavustinov-Documents-work-triasdev-templify/10f81cd0-4b6d-459a-b6a1-298912a1a9f2/scratchpad
dotnet restore --locked-mode
dotnet build -c Release -p:ContinuousIntegrationBuild=true 2>&1 | tail -3
dotnet test -c Release --no-build 2>&1 | grep -E "^(Passed|Failed)!"
rm -rf $SCRATCH/after && mkdir -p $SCRATCH/after
dotnet pack src/TriasDev.Templify/TriasDev.Templify.csproj -c Release -p:ContinuousIntegrationBuild=true -o $SCRATCH/after
unzip -l $SCRATCH/after/TriasDev.Templify.1.9.0.nupkg | awk 'NR>3 {print $4}' | grep -v '^$' | sort > $SCRATCH/after/files.txt
unzip -l $SCRATCH/after/TriasDev.Templify.1.9.0.snupkg | awk 'NR>3 {print $4}' | grep -v '^$' | sort > $SCRATCH/after/symbols.txt
unzip -p $SCRATCH/after/TriasDev.Templify.1.9.0.nupkg TriasDev.Templify.nuspec > $SCRATCH/after/nuspec.xml
diff $SCRATCH/baseline/files.txt $SCRATCH/after/files.txt && echo FILES-SAME
diff $SCRATCH/baseline/symbols.txt $SCRATCH/after/symbols.txt && echo SYMBOLS-SAME
diff <(grep -v 'commit=' $SCRATCH/baseline/nuspec.xml) <(grep -v 'commit=' $SCRATCH/after/nuspec.xml) && echo NUSPEC-SAME
```

Expected: lock files valid; 0 warnings / 0 errors; test counts equal the baseline; `FILES-SAME`, `SYMBOLS-SAME`,
`NUSPEC-SAME`. If the nuspec differs only in the `<repository ... commit="...">` line, that is the expected commit
difference. Any other difference (missing `icon.png`, `README.md`, `.xml` docs, `.pdb` in the snupkg, different
dependency groups) is a defect: fix the props and repeat.

- [ ] **Step 6: Check that RS0016 still fires**

Temporarily add `public static class ProbeApi { }` to `src/TriasDev.Templify/Core/TemplateFormat.cs` (end of file),
then:

```bash
dotnet build src/TriasDev.Templify -c Release 2>&1 | grep -c RS0016
git checkout src/TriasDev.Templify/Core/TemplateFormat.cs
```

Expected: count ≥ 1 (the API guard moved to `src/Directory.Build.props` is active); the file is restored.

- [ ] **Step 7: Commit**

```bash
dotnet format --verify-no-changes --no-restore
git add -A
git commit -m "chore: split build props into root, src/ and tests/ like tabular (#3)"
```

---

### Task 5: Documentation

**Files:**
- Modify: `CLAUDE.md`, `CONTRIBUTING.md`, `README.md`, `src/TriasDev.Templify/README.md`,
  `src/TriasDev.Templify/ARCHITECTURE.md`, `src/TriasDev.Templify/PERFORMANCE.md`, `scripts/README.md`,
  `examples/README.md`, `tools/TriasDev.Templify.Converter/README.md`, `tools/TriasDev.Templify.Gui/README.md`,
  `tools/TriasDev.Templify.DocumentGenerator/README.md`, `tools/TriasDev.Templify.DocumentGenerator/Program.cs:112`,
  `docs/index.md`, `docs/FAQ.md`, `docs/for-developers/quick-start.md`,
  `docs/for-template-authors/boolean-expressions.md`, `docs/for-template-authors/format-specifiers.md`,
  `docs/tutorials/01-hello-world.md`, `docs/tutorials/02-invoice-generator.md`, `.github/ISSUE_TEMPLATE/question.yml`,
  comments in `tests/TriasDev.Templify.Tests/Documentation/DocSamplesTests.cs` and `OpenDocumentSamplesTests.cs`

**Interfaces:**
- Consumes: the layout and `templify.slnx` from Tasks 2-3.

- [ ] **Step 1: Rewrite path references mechanically**

Old project paths become new ones in GitHub links (`blob/main/`, `tree/main/`), `--project` arguments, `cd`/`dotnet`
commands and prose. Skip `docs/archive/` and `docs/superpowers/`.

```bash
files=$(git grep -lP 'TriasDev\.Templify[A-Za-z.]*/|templify\.sln(?!x)' -- ':!docs/archive/**' ':!docs/superpowers/**' ':!*.slnx' ':!.github/workflows/**' ':!release-please-config.json' ':!scripts/*.sh')
echo "$files"
for f in $files; do
  perl -pi -e '
    s#(?<![\w/.-])TriasDev\.Templify\.(Tests|Tools\.Tests|Converter\.Tests)/#tests/TriasDev.Templify.$1/#g;
    s#(?<![\w/.-])TriasDev\.Templify\.Benchmarks/#benchmarks/TriasDev.Templify.Benchmarks/#g;
    s#(?<![\w/.-])TriasDev\.Templify\.Demo/#samples/TriasDev.Templify.Demo/#g;
    s#(?<![\w/.-])TriasDev\.Templify\.(Converter|Gui|DocumentGenerator)/#tools/TriasDev.Templify.$1/#g;
    s#(?<![\w/.-])TriasDev\.Templify/#src/TriasDev.Templify/#g;
    s#(blob|tree)/main/TriasDev\.Templify\.(Tests|Tools\.Tests|Converter\.Tests)/#$1/main/tests/TriasDev.Templify.$2/#g;
    s#(blob|tree)/main/TriasDev\.Templify\.Benchmarks/#$1/main/benchmarks/TriasDev.Templify.Benchmarks/#g;
    s#(blob|tree)/main/TriasDev\.Templify\.Demo/#$1/main/samples/TriasDev.Templify.Demo/#g;
    s#(blob|tree)/main/TriasDev\.Templify\.(Converter|Gui|DocumentGenerator)/#$1/main/tools/TriasDev.Templify.$2/#g;
    s#(blob|tree)/main/TriasDev\.Templify/#$1/main/src/TriasDev.Templify/#g;
    s#templify\.sln(?!x)#templify.slnx#g;
  ' "$f"
done
git diff --stat
```

- [ ] **Step 2: Review relative links between moved files**

Relative Markdown links between files that moved together stay valid; links from a moved file to a root file need
one more `../`. Find and fix them:

```bash
git grep -nE '\]\((\.\./)+[^)]*\)' -- 'src/**/*.md' 'tools/**/*.md' 'samples/**/*.md' 'benchmarks/**/*.md' 'tests/**/*.md'
```

For each hit, resolve the target from the file's new directory; e.g. in `src/TriasDev.Templify/README.md` a link
`](../README.md)` becomes `](../../README.md)`, `](../docs/...)` becomes `](../../docs/...)`. Then check every
relative link resolves:

```bash
for f in $(git ls-files 'src/**/*.md' 'tools/**/*.md' 'samples/**/*.md' 'benchmarks/**/*.md' '*.md' 'scripts/*.md' 'examples/*.md'); do
  d=$(dirname "$f")
  grep -oE '\]\([^)#: ]+(#[^)]*)?\)' "$f" | sed -E 's/^\]\(([^)#]+).*/\1/' | while read -r l; do
    test -e "$d/$l" || echo "BROKEN $f -> $l"
  done
done
```

Expected: no `BROKEN` lines.

- [ ] **Step 3: Update the structure sections by hand**

- `CLAUDE.md` "Solution Structure": list the projects under their groups (`src/`, `tests/`, `benchmarks/`,
  `samples/`, `tools/`); "Build Configuration": add one line — "`src/Directory.Build.props` (package metadata, Source
  Link, public API guard) and `tests/Directory.Build.props` (xUnit packages, `IsPackable=false`) import the root
  file." Commands use `templify.slnx` and the new project paths (Step 1 did the mechanical part; read the sections
  and fix anything Step 1 left odd).
- `CONTRIBUTING.md`: the same structure description where it lists projects or commands.
- `README.md` (root): any "Project structure" / "Repository layout" section shows the new tree.

- [ ] **Step 4: Verify docs and leftovers**

```bash
git grep -nP '(?<![\w/.-])TriasDev\.Templify(\.[A-Za-z]+)?/' -- ':!docs/archive/**' ':!docs/superpowers/**'
git grep -nP 'templify\.sln(?!x)' -- ':!docs/archive/**' ':!docs/superpowers/**'
SCRATCH=/private/tmp/claude-501/-Users-vaceslavustinov-Documents-work-triasdev-templify/10f81cd0-4b6d-459a-b6a1-298912a1a9f2/scratchpad
mkdocs build --strict -d $SCRATCH/site 2>&1 | grep -iE "warn|error|built"
```

Expected: both greps print nothing (every remaining `TriasDev.Templify.X/` is preceded by `src/`, `tests/`, ...);
MkDocs reports "Documentation built" with no warning.

- [ ] **Step 5: Commit**

```bash
dotnet build -c Release -p:ContinuousIntegrationBuild=true 2>&1 | tail -2   # DocumentGenerator Program.cs message changed
git add -A
git commit -m "docs: update project paths for the new repository layout (#3)"
```

---

### Task 6: Full verification and pull request

**Files:** none (verification); PR description.

- [ ] **Step 1: CI-equivalent build, format and locked restore**

```bash
git clean -fdX -- src tests benchmarks samples tools
dotnet restore --locked-mode
dotnet build -c Release -p:ContinuousIntegrationBuild=true 2>&1 | tail -3
dotnet format --verify-no-changes --no-restore; echo format-exit=$?
```

Expected: 0 warnings / 0 errors; `format-exit=0`.

- [ ] **Step 2: All tests, compared with the baseline**

```bash
SCRATCH=/private/tmp/claude-501/-Users-vaceslavustinov-Documents-work-triasdev-templify/10f81cd0-4b6d-459a-b6a1-298912a1a9f2/scratchpad
dotnet test -c Release --no-build 2>&1 | grep -E "^(Passed|Failed)!" | sed -E 's/Duration: [^-]+- //' | sort > $SCRATCH/after/tests.txt
diff $SCRATCH/baseline/tests.txt $SCRATCH/after/tests.txt && echo TESTS-SAME
```

Expected: `TESTS-SAME` (same assemblies, TFMs and counts, 0 failed).

- [ ] **Step 3: LibreOffice round trips**

```bash
dotnet test tests/TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj -c Release -f net10.0 --filter "Category=LibreOffice" 2>&1 | grep -E "^(Passed|Failed)!"
```

Expected: all passed, `Skipped: 0`. Note the count for the PR description.

- [ ] **Step 4: Converter scripts**

```bash
SCRATCH=/private/tmp/claude-501/-Users-vaceslavustinov-Documents-work-triasdev-templify/10f81cd0-4b6d-459a-b6a1-298912a1a9f2/scratchpad
sample=$(git ls-files 'examples/templates/*.docx' | head -1); cp "$sample" $SCRATCH/sample.docx
(cd $SCRATCH && /Users/vaceslavustinov/Documents/work/triasdev/templify/scripts/validate.sh sample.docx; echo exit=$?)
(cd $SCRATCH && /Users/vaceslavustinov/Documents/work/triasdev/templify/scripts/analyze.sh sample.docx --output report.md; echo exit=$?)
(cd $SCRATCH && /Users/vaceslavustinov/Documents/work/triasdev/templify/scripts/clean.sh sample.docx --output cleaned.docx; echo exit=$?)
(cd $SCRATCH && /Users/vaceslavustinov/Documents/work/triasdev/templify/scripts/convert.sh sample.docx --output converted.docx; echo exit=$?)
```

Expected: each script starts the converter (no "project file does not exist"); `exit=0` for each.

- [ ] **Step 5: DocumentGenerator**

```bash
dotnet run --project tools/TriasDev.Templify.DocumentGenerator -- --skip-images 2>&1 | tail -5
git status --short examples docs/images
```

Expected: no "Could not find the repository root"; `git status` shows no changes (outputs identical). If outputs
differ only in timestamps inside the documents, `git checkout -- examples` and note it in the PR.

- [ ] **Step 6: Push and open the PR**

```bash
git push -u origin chore/project-structure
gh pr create --title "chore: align the repository layout with tabular (#3)" --body "<body>"
```

Body: summary of the four commits, the layout table from the spec, out-of-scope list, verification results (build,
`TESTS-SAME` counts, package equivalence `FILES-SAME`/`SYMBOLS-SAME`/`NUSPEC-SAME`, LibreOffice `N/N passed`,
scripts, DocumentGenerator, mkdocs), note that external links to old GitHub paths break and local `bin/obj`/IDE
folders under the old paths can be deleted, and "Public API impact: none". Closes #3.

- [ ] **Step 7: Wait for CI**

```bash
gh pr checks --watch --interval 30
```

Expected: all checks pass, including Pack NuGet, CodeQL and SonarQube. Do not merge without the user's go-ahead.
