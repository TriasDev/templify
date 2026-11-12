# .NET 11 Target Framework Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Templify 1.10.0 targeting `net11.0;net10.0`: fix the .NET 11 float/double→decimal regression, add `net11.0`, drop the EOL `net8.0`/`net9.0`.

**Architecture:** Three PRs, one release. PR 1 (`fix:`) makes `NumericValue` independent of the runtime's float→decimal conversion. PR 2 (`feat:`) adds `net11.0` and moves the SDK, tools and test projects to .NET 11. PR 3 (`build:`) drops `net8.0`/`net9.0` with a package-validation suppression and a manual release-notes notice. Each PR can be reverted on its own.

**Tech Stack:** .NET SDK 11.0.100, DocumentFormat.OpenXml 3.5.1, xUnit v3 (`xunit.v3.mtp-off`, VSTest), GitHub Actions, release-please.

**Spec:** Issue #154 ("Remaining" section) plus the maintainer decisions of 2026-11-12: one release, separate PRs for add and drop. The final library TFMs are `net11.0;net10.0`.

## Global Constraints

- SDK: `global.json` → `"version": "11.0.100"`, `"rollForward": "latestFeature"`.
- Library + `TriasDev.Templify.Tests`: after PR 2 `net11.0;net10.0;net9.0;net8.0`; after PR 3 `net11.0;net10.0` (both files are kept in sync).
- Tools, `Tools.Tests`, `Converter.Tests`, Demo: `net11.0`. **Benchmarks stay `net10.0`**: BenchmarkDotNet 0.15.8 (the latest stable; 0.16 is preview only) crashes on net11 with `NotImplementedException: GetRuntimeVersion not implemented for NotRecognized`.
- Public API impact: none in all three PRs (no `PublicAPI.*.txt` changes). PR 1 changes behavior only on .NET 11 runtimes, back to the pre-11 results.
- Every PR: `dotnet build templify.slnx -c Release -p:ContinuousIntegrationBuild=true` with 0 warnings, `dotnet format --verify-no-changes --no-restore`, lock files committed (`dotnet restore templify.slnx` without locked mode).
- PR 2 and PR 3 touch the shared engine/TFMs → LibreOffice round trips are mandatory, with 0 skipped, and the result goes in the PR description.
- Commit/PR conventions: Conventional Commits, `(#154)` or the new issue number in the subject, public API impact stated in each PR body.

## Review Focus

1. **A net8/net9 consumer updates to 1.10.0** → NuGet `NU1202` ("not compatible"). Expected: the release notes and README tell them to stay on 1.9.x. Checked in Task 4 (release notes), no test.
2. **A 1.9.0-style `net10.0` asset running on the .NET 11 runtime**: JSON `0.1`, `decimal 0.1m` or `float 0.1f` compared with the literal `0.1` → was false. Pinned by the Task 1 tests, which run on runtime 11 from Task 2 on.
3. **Floating values at decimal-range edges** (`1e-30`, `-0.0`, `7.8e28`, 18-digit doubles) must convert exactly as the legacy `Convert.ToDecimal` did → Task 1 `FloatingExtremes_ConvertLikeLegacyConvertToDecimal`.
4. **Non-English cultures**: the new rounding formats numbers to text, which must be invariant under de-DE/tr-TR/ar-SA → `InvariantCulture` in Task 1. The CI culture job runs on `net11.0` after Task 2.
5. **Other .NET 11 runtime behavior changes in output** (number/date formatting, ICU). The spike on 2026-11-12 found only the decimal conversion (2,346/2,349 green before the fix). Task 2 runs the full suite and the LibreOffice round trips on net11.0.

---

### Task 1: PR 1 — runtime-independent float/double → decimal comparison

.NET 11 made `Convert.ToDecimal(double/float)` exact (`0.1d` → `0.1000000000000000055511151231`, `0.1f` → `0.100000001490116119384765625`). Before, it rounded to 15/7 significant digits. `NumericValue.FromFloating` (`src/TriasDev.Templify/Utilities/NumericValue.cs:128-143`) relies on that rounding. Verified on runtime 11: `X = 0.1` is **false** for `X` = `0.1m`, `0.1f` or JSON `0.1`, and `X = Y` with `0.1` / `0.1m` is false too. This affects 1.9.0 consumers on .NET 11 today, because they get the `lib/net10.0` asset.

**Files:**
- Modify: `src/TriasDev.Templify/Utilities/NumericValue.cs:14-22` (remarks), `:128-143` (`FromFloating`)
- Test: `tests/TriasDev.Templify.Tests/Utilities/NumericValueTests.cs`, `tests/TriasDev.Templify.Tests/Conditionals/Engine/NumericSemanticsTests.cs`

**Interfaces:** none new; `NumericValue` keeps its internal surface.

- [ ] **Step 0: Issue + branch.** Ask the maintainer before `gh issue create` (outward-facing). Title: `fix: numeric comparisons with fractional float/double values fail on .NET 11`. Body: the repro from this task's intro. Then `git switch -c fix/net11-decimal-conversion main` and commit this plan file as the first commit (`docs: add .NET 11 plan (#154)`).

- [ ] **Step 1: Write the failing tests**

`NumericValueTests`:
```csharp
[Fact]
public void DoubleAgainstDecimal_ComparesThroughDecimal()
{
    Assert.True(NumericValue.TryFrom(0.1d, out NumericValue dbl));
    Assert.True(dbl.NumericEquals(ExactOf(0.1m)));
}

[Theory]
[InlineData(1e-30, "0")]
[InlineData(-0.0, "0")]
[InlineData(7.8e28, "78000000000000000000000000000")]
[InlineData(123456789012345678.0, "123456789012346000")]
[InlineData(1.0 / 3, "0.333333333333333")]
[InlineData(2.5e-7, "0.00000025")]
public void FloatingExtremes_ConvertLikeLegacyConvertToDecimal(double value, string expected)
{
    Assert.True(NumericValue.TryFrom(value, out NumericValue number));
    Assert.True(number.NumericEquals(ExactOf(decimal.Parse(expected, CultureInfo.InvariantCulture))));
}
```
`NumericSemanticsTests`: add `{ 0.1, "0.1" }` to `EqualNumbers`, plus
```csharp
[Fact]
public void Cond_Equality_JsonFraction()
    => Assert.True(EvalJson("Rate = 0.1", "{\"Rate\": 0.1}"));

[Fact]
public void Cond_Equality_DoubleVsDecimalVariable()
    => Assert.True(Eval("X = Y", new() { ["X"] = 0.1, ["Y"] = 0.1m }));
```
(Do **not** add `0.1 + 0.2` vs `"0.3"`: in the default dialect a double variable against a numeric literal compares as `double` and is false on every runtime. That is existing behavior.)

- [ ] **Step 2: Show red on runtime 11.** `DOTNET_ROLL_FORWARD` is not honored by the VSTest testhost, so the red run uses a temporary `net11.0` TFM:
  temporarily set `global.json` → `11.0.100` and both `TargetFrameworks` → `net11.0;net10.0;net9.0;net8.0`, then run
  `dotnet test tests/TriasDev.Templify.Tests -c Release -f net11.0 --filter "FullyQualifiedName~NumericValueTests|FullyQualifiedName~NumericSemanticsTests"`
  Expected: FAIL. The cases are `FloatAgainstDecimal_ComparesThroughDecimal`, `Equality_AcrossNumericTypes_IsNumeric(0.1m / 1.1f / 0.1)`, `DoubleAgainstDecimal_…`, `Cond_Equality_JsonFraction` and `Cond_Equality_DoubleVsDecimalVariable`. The same filter with `-f net10.0` passes.

- [ ] **Step 3: Implement.** In `FromFloating`, replace the `Convert.ToDecimal` try/catch with an explicit legacy rounding: format with `"G7"` (Single, cast to `float`) or `"G15"` (Double) using `CultureInfo.InvariantCulture`, then `decimal.TryParse(..., NumberStyles.Float, CultureInfo.InvariantCulture, out decimal parsed)`. On failure `dec = null`. Keep the existing NaN/Infinity/`7.9e28` guard. In the remarks, replace the `Convert.ToDecimal(double)` sentence with: rounded to the type's significant digits (7 for `float`, 15 for `double`), as `Convert.ToDecimal` did before .NET 11, so the result does not depend on the runtime.

- [ ] **Step 4: Green on all runtimes** (temporary TFMs still in place): run the same filter for `-f net11.0`, `net10.0`, `net9.0` and `net8.0`. Expected: PASS everywhere. Then revert the temporary changes:
  `git checkout -- global.json src/TriasDev.Templify/TriasDev.Templify.csproj src/TriasDev.Templify/packages.lock.json tests/TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj tests/TriasDev.Templify.Tests/packages.lock.json`
  and run the full core suite once: `dotnet test tests/TriasDev.Templify.Tests -c Release`, all TFMs green.

- [ ] **Step 5: CI build + format**, then commit and open the PR:
```bash
git add src/TriasDev.Templify/Utilities/NumericValue.cs tests/TriasDev.Templify.Tests/Utilities/NumericValueTests.cs tests/TriasDev.Templify.Tests/Conditionals/Engine/NumericSemanticsTests.cs
git commit -m "fix: compare float/double with decimal the same way on .NET 11 (#<issue>)"
```
  PR body: public API none; behavior change only on .NET 11 runtimes, restoring the results of .NET ≤ 10. Mention that CI cannot show the runtime-11 failure until PR 2 adds `net11.0`, and paste the red/green output from steps 2 and 4.

### Task 2: PR 2 — add `net11.0`, SDK 11, tools on .NET 11

**Files:**
- Modify: `global.json`; `src/TriasDev.Templify/TriasDev.Templify.csproj:4-6` (TFMs + comment); `tests/TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj:5`; `TargetFramework` → `net11.0` in `tools/TriasDev.Templify.{Converter,Gui,DocumentGenerator}`, `tests/TriasDev.Templify.{Tools,Converter}.Tests`, `samples/TriasDev.Templify.Demo`; `benchmarks/…Benchmarks.csproj` gets only a comment (stays `net10.0`); all `packages.lock.json`
- Modify CI: `.github/workflows/ci.yml` (build-and-test `dotnet-version`: `8.0.x`, `9.0.x`, `10.0.x` + comment; culture-tests `--framework net11.0` + comment), `.github/workflows/release-please.yml` (`dotnet-version` + `10.0.x`)
- Modify docs: `README.md` (263, 288, 370, 384, 419, 425), `src/TriasDev.Templify/README.md` (32, 1424), `SECURITY.md:13`, `CONTRIBUTING.md` (199-200, 361 `-f net11.0`), `docs/FAQ.md:29`, `docs/for-developers/quick-start.md:7`, `tools/TriasDev.Templify.Gui/README.md` (.NET 11 SDK, `bin/Release/net11.0/…` paths), `.github/PULL_REQUEST_TEMPLATE.md` and `.github/ISSUE_TEMPLATE/bug_report.yml` (add `.NET 11` on top), `CLAUDE.md` (Target Frameworks line, Build Configuration TFMs, LibreOffice command `-f net11.0`, "tools use net11.0, benchmarks net10.0")

**Interfaces:** Consumes Task 1 (merged first; otherwise 3 core tests fail on net11.0).

- [ ] **Step 0:** `git switch -c feat/net11-target-framework main` (after PR 1 is merged).
- [ ] **Step 1: Retarget** the files above. Benchmarks csproj comment: `<!-- Stays on net10.0: BenchmarkDotNet 0.15.8 does not recognize net11.0 (NotImplementedException in GetRuntimeVersion). Move when a stable BenchmarkDotNet supports it. -->`. Then run `dotnet restore templify.slnx`; expected: 9 lock files updated.
- [ ] **Step 2: CI build + format.** Expected: 0 warnings, format exit 0 (both verified in the 2026-11-12 spike).
- [ ] **Step 3: Full tests, every TFM:** `dotnet test templify.slnx -c Release`. Expected: core green on net11/10/9/8, Tools.Tests 64/64 and Converter.Tests 95/95 on net11.0.
- [ ] **Step 4: LibreOffice round trips:** `dotnet test tests/TriasDev.Templify.Tests -c Release -f net11.0 --filter "Category=LibreOffice"`. Expected: all passed, 0 skipped.
- [ ] **Step 5: Pack + benchmarks smoke.**
  - `dotnet pack src/TriasDev.Templify -c Release -o ./artifacts -p:ContinuousIntegrationBuild=true`, then `unzip -l artifacts/*.nupkg | grep lib/`. Expected: `lib/net11.0`, `net10.0`, `net9.0`, `net8.0`, and no PKV/CP errors.
  - `dotnet run --project benchmarks/TriasDev.Templify.Benchmarks -c Release -- --filter '*ConditionEngineBenchmarks*' --job dry`. Expected: it completes without exception.
  - Delete `./artifacts` and `BenchmarkDotNet.Artifacts/`.
- [ ] **Step 6: Docs** (files listed above). For the "Supported .NET Versions" text in this PR: `net8.0`, `net9.0`, `net10.0` and `net11.0`.
- [ ] **Step 7: Commit + PR:** `feat: add net11.0 target framework (#154)`. PR body: API impact additive (new TFM asset, same API), LibreOffice result, and the benchmarks exception.

### Task 3: PR 3 — drop `net8.0` / `net9.0` (EOL 2026-11-10)

**Files:**
- Modify: library + core-tests `TargetFrameworks` → `net11.0;net10.0` (library comment: "net6.0 dropped in 1.8.0, net8.0/net9.0 in 1.10.0, see #154 and CompatibilitySuppressions.xml"); lock files
- Create: `src/TriasDev.Templify/CompatibilitySuppressions.xml`. Use the format of the 1.8.0 file (`git show 0f326e7:TriasDev.Templify/CompatibilitySuppressions.xml`) with two `PKV006` suppressions (`<Target>net8.0</Target>`, `<Target>net9.0</Target>`). The comment says: intentional (#154), consumers on net8/net9 stay on 1.9.x, delete when `PackageValidationBaselineVersion` ≥ 1.10.0.
- Modify CI: `ci.yml` and `release-please.yml` `dotnet-version` → `10.0.x` only (comments: the 10.0 runtime runs the net10.0 tests)
- Modify docs: the same files as Task 2 → `net10.0`/`net11.0` only. Add the warning `> ⚠️ **net8.0 and net9.0 are no longer supported as of 1.10.0** (end of support 2026-11-10). Projects on .NET 8 or 9 can stay on Templify 1.9.x.` next to the existing net6.0 note in `README.md`, the library README and `SECURITY.md`. Further changes: FAQ ("net8.0/net9.0 up to 1.9.x"), tutorial and quick-start ".NET 10 SDK or later", templates without .NET 8/9, CLAUDE.md (TFMs and "net8.0/net9.0 dropped in 1.10.0"), CONTRIBUTING (runtimes: only 10 besides SDK 11).

- [ ] **Step 0:** `git switch -c build/drop-net8-net9 main` (after PR 2 is merged).
- [ ] **Step 1: Prove the suppression is needed:** change the TFMs, run `dotnet restore templify.slnx` and `dotnet build … -c Release`, then `dotnet pack src/TriasDev.Templify -c Release -o ./artifacts` *without* the suppressions file. Expected: FAIL with `PKV006` for `net8.0` and `net9.0`, and nothing else.
- [ ] **Step 2: Add `CompatibilitySuppressions.xml`**, then pack again. Expected: success, `lib/` has only `net11.0` and `net10.0`.
- [ ] **Step 3: CI build + format, full tests, LibreOffice round trips** (same commands as Task 2, steps 2-4).
- [ ] **Step 4: CI workflows + docs** (files above).
- [ ] **Step 5: Commit + PR:**
```
build: drop net8.0 and net9.0 target frameworks (EOL) (#154)

The library now targets net11.0 and net10.0. .NET 8 and .NET 9 reached
end of support on 2026-11-10.

Release notes: net8.0 and net9.0 are no longer supported as of 1.10.0.
Projects that still target .NET 8 or 9 should stay on Templify 1.9.x.
```
  PR labels: `breaking-change`, `dependencies`. API impact: TFM removal (PKV006 suppressed only for the two TFMs; all other package validation stays active).

### Task 4: Release 1.10.0 and close #154

- [ ] **Step 1:** Comment on #154 with the status (docs tooling and suppressions cleanup already done; PR links; benchmarks stay on net10.0 until BenchmarkDotNet supports net11). Ask the maintainer before posting.
- [ ] **Step 2: Release PR (release-please, expected 1.10.0).** Right before the merge, in the release branch, add to both the release PR body **and** `CHANGELOG.md` an "⚠️ Upgrade notes" block. It must contain the text from Task 3's commit body, because `build:` is hidden in the changelog. Run the LibreOffice round trips on the release branch (0 skipped). Merge only on the maintainer's explicit go-ahead. The NuGet environment approval is the maintainer's.
- [ ] **Step 3: After publication** (package indexed on nuget.org), in a `chore:` PR: set `PackageValidationBaselineVersion` → `1.10.0`, delete `src/TriasDev.Templify/CompatibilitySuppressions.xml`, and move any `PublicAPI.Unshipped.txt` entries to Shipped. Close #154.
