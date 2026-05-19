# Simplify to Space Blocker — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor the v0.1.5 codebase (Source → Transform → Sink pipeline with HID Raw Input, ToUnicodeEx, SendInput re-injection, and full DI/Hosting stack) into a minimal app that does ONE thing: suppress `VK_SPACE` keystrokes when (the configured HID scanner produced them) AND (the configured target window is in foreground). All other keystrokes — from the scanner OR from the human keyboard — pass through to the OS untouched.

**Architecture:** Pure-logic decision module (`SpaceSuppressor`) wired into the `WH_KEYBOARD_LL` hook. A `ScannerKeyTracker` (NativeWindow + Raw Input) updates a `LastScannerKeyTime` timestamp every time a `WM_INPUT` matches the configured VID/PID device. A `ForegroundWindow` helper queries `GetForegroundWindow` on demand. The hook callback combines these three signals. No `SendInput`, no payload accumulation, no `ToUnicodeEx`, no transform/sink/orchestrator, no DI container, no Hosting, no Serilog, no IOptionsMonitor, no hot-reload, no Serial source, no diagnostics tab.

**Tech Stack:** .NET 10 (`net10.0` for Core/Tests, `net10.0-windows` for WinForms exe), WinForms, Win32 P/Invokes (`SetWindowsHookEx`, `RegisterRawInputDevices`, `GetForegroundWindow`, `GetWindowText`, `GetWindowThreadProcessId`, `QueryFullProcessImageName`), xUnit + Shouldly tests, System.Management for HID enumeration.

**Versioning:** This is a behavior-breaking refactor (no more re-injection, no more terminator transform, no Serial). Bump to `v0.2.0`.

---

## File Structure (final state after this plan)

```
src/AutolumoBarcodeScannerTool.Core/                 (net10.0 — pure logic, cross-platform testable)
  AutolumoBarcodeScannerTool.Core.csproj
  ScannerConfig.cs                                   ← record with 5 fields
  ConfigIo.cs                                        ← Load/Save .ini (~50 lines, tolerates unknown keys)
  ForegroundWindowInfo.cs                            ← record (ProcessName, WindowTitle)
  SpaceSuppressor.cs                                 ← pure static method, all logic

src/AutolumoBarcodeScannerTool/                      (net10.0-windows — WinForms exe)
  AutolumoBarcodeScannerTool.csproj                  ← references Core, only System.Management package
  Program.cs                                         ← entry, mutex, loads config, installs hook+tracker, tray
  app.manifest                                       ← (unchanged)
  appsettings.default.ini                            ← 5 lines, copied to %LOCALAPPDATA% on first run
  Hid/LowLevelKeyboardHook.cs                        ← callback Func<int, bool>
  Hid/ScannerKeyTracker.cs                           ← NativeWindow + WM_INPUT + LastScannerKeyTime
  Hid/RawInputInterop.cs                             ← P/Invokes (unchanged)
  Hid/HidDeviceEnumerator.cs                         ← lists HID keyboards for the ComboBox (unchanged)
  Win32/ForegroundWindow.cs                          ← GetForegroundWindow + GetWindowText + process name
  Tray/TrayController.cs                             ← NotifyIcon + context menu (Configurar/Salir)
  Tray/SettingsForm.cs                               ← ONE tab: device combo + target proc + target title + autostart
  Autostart/AutostartManager.cs                      ← (unchanged)

tests/AutolumoBarcodeScannerTool.Tests/              (net10.0 — runs on macOS/Linux/Windows)
  AutolumoBarcodeScannerTool.Tests.csproj            ← references Core; xunit + Shouldly only
  SpaceSuppressorTests.cs                            ← ~12 tests (all AND/OR branches + boundaries)
  ConfigIoTests.cs                                   ← ~6 tests (round-trip, missing file, tolerance to unknown keys)
```

**Deleted from v0.1.5 (with explicit user authorization in Task 12):**
- `src/AutolumoBarcodeScannerTool.Core/Models/ScannerOptions.cs`, `Enums.cs`, `ScanEvent.cs`
- `src/AutolumoBarcodeScannerTool.Core/Transforms/` (whole folder)
- `src/AutolumoBarcodeScannerTool.Core/Orchestration/` (whole folder)
- `src/AutolumoBarcodeScannerTool.Core/Sinks/` (whole folder)
- `src/AutolumoBarcodeScannerTool.Core/Sources/` (whole folder)
- `src/AutolumoBarcodeScannerTool.Core/Configuration/IniConfigWriter.cs` (replaced by `ConfigIo`)
- `src/AutolumoBarcodeScannerTool/Hid/HidKeyboardInputSource.cs` (replaced by `ScannerKeyTracker`)
- `src/AutolumoBarcodeScannerTool/Hid/InjectionMarker.cs` (we don't inject)
- `src/AutolumoBarcodeScannerTool/Win32/SendInputInjector.cs` (we don't inject)
- `src/AutolumoBarcodeScannerTool/Win32/Win32ForegroundWindowProvider.cs` (replaced by simpler `ForegroundWindow`)
- `tests/.../Configuration/IniConfigWriterTests.cs` (writer is gone)
- `tests/.../Models/ScanEventTests.cs`
- `tests/.../Orchestration/` (whole folder)
- `tests/.../Sinks/` (whole folder)
- `tests/.../Sources/` (whole folder)
- `tests/.../Transforms/` (whole folder)
- `tests/.../Fakes/StaticOptionsMonitor.cs`

---

## Task 0: Clean working tree to last commit

The session leading into this plan left uncommitted experimental stubs (`SpaceBurstBlocker.cs`, simplified `Program.cs`, csproj without packages, modified `LowLevelKeyboardHook.cs`). The plan assumes the working tree matches the last commit `e670fb2` (which has the verbose diagnostics feature) so that every subsequent task starts from a coherent baseline.

**Files:**
- Reset: all uncommitted changes in `src/AutolumoBarcodeScannerTool/`

- [ ] **Step 1: Inspect current uncommitted state**

```bash
git status --short
```

Expected: shows modified `Program.cs`, `LowLevelKeyboardHook.cs`, `AutolumoBarcodeScannerTool.csproj`; untracked `SpaceBurstBlocker.cs`.

- [ ] **Step 2: Restore tracked files to HEAD**

```bash
git checkout -- src/AutolumoBarcodeScannerTool/
```

- [ ] **Step 3: Remove untracked stub**

```bash
rm src/AutolumoBarcodeScannerTool/SpaceBurstBlocker.cs
```

- [ ] **Step 4: Verify clean state**

```bash
git status --short
```

Expected: empty output (or only files that were untracked unrelated to this work).

---

## Task 1: Strip Core project to bare bones (remove obsolete packages)

The Core project will hold only 4 small files after this plan. Remove the unused NuGet packages now so subsequent tasks don't compile stale dependencies.

**Files:**
- Modify: `src/AutolumoBarcodeScannerTool.Core/AutolumoBarcodeScannerTool.Core.csproj`

- [ ] **Step 1: Replace csproj content**

Replace the entire file with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

(No package references — `ScannerConfig`, `ConfigIo`, `ForegroundWindowInfo`, `SpaceSuppressor` need no NuGet dependencies.)

- [ ] **Step 2: Verify Core still restores (will fail to compile because old files reference removed packages — that's OK; we delete those files in Task 12)**

```bash
dotnet restore src/AutolumoBarcodeScannerTool.Core/AutolumoBarcodeScannerTool.Core.csproj
```

Expected: restore succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/AutolumoBarcodeScannerTool.Core/AutolumoBarcodeScannerTool.Core.csproj
git commit -m "refactor(core): strip NuGet packages — Core will be 4 pure files"
```

---

## Task 2: TDD `ForegroundWindowInfo` record

A trivial value type, but it's referenced by `SpaceSuppressor` so it must exist first.

**Files:**
- Create: `src/AutolumoBarcodeScannerTool.Core/ForegroundWindowInfo.cs`

- [ ] **Step 1: Create the record**

```csharp
namespace AutolumoBarcodeScannerTool.Core;

public sealed record ForegroundWindowInfo(string ProcessName, string WindowTitle);
```

- [ ] **Step 2: Verify compile**

```bash
dotnet build src/AutolumoBarcodeScannerTool.Core/AutolumoBarcodeScannerTool.Core.csproj -c Release 2>&1 | tail -5
```

Expected: build fails because old Core files (Models, Transforms, etc.) still reference removed packages. That's expected — we'll delete them in Task 12. The new file itself should not introduce new errors. **Skip the build verification at this stage** — proceed to Step 3.

- [ ] **Step 3: Commit**

```bash
git add src/AutolumoBarcodeScannerTool.Core/ForegroundWindowInfo.cs
git commit -m "feat(core): ForegroundWindowInfo record"
```

---

## Task 3: TDD `SpaceSuppressor` — the core decision logic

This is the heart of the new system. Pure static method, all branches covered by tests.

**Files:**
- Create: `src/AutolumoBarcodeScannerTool.Core/SpaceSuppressor.cs`
- Create: `tests/AutolumoBarcodeScannerTool.Tests/SpaceSuppressorTests.cs`

- [ ] **Step 1: Write the test file with all failing tests**

Create `tests/AutolumoBarcodeScannerTool.Tests/SpaceSuppressorTests.cs`:

```csharp
using AutolumoBarcodeScannerTool.Core;
using Shouldly;
using Xunit;

namespace AutolumoBarcodeScannerTool.Tests;

public class SpaceSuppressorTests
{
    private static readonly DateTime Now = new(2026, 5, 19, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ScannerRecent = Now.AddMilliseconds(-50);
    private static readonly DateTime ScannerOld = Now.AddMilliseconds(-500);
    private static readonly ForegroundWindowInfo MatchingWindow = new("MiAppContable", "Factura nueva");

    [Fact]
    public void Pass_When_VkIsNotSpace()
    {
        var result = SpaceSuppressor.ShouldSuppress(
            vk: 0x41, // 'A'
            now: Now,
            lastScannerKey: ScannerRecent,
            foreground: MatchingWindow,
            targetProcessName: "MiAppContable",
            targetWindowTitleContains: "");

        result.ShouldBeFalse();
    }

    [Fact]
    public void Pass_When_ScannerActivityIsOld()
    {
        var result = SpaceSuppressor.ShouldSuppress(
            vk: 0x20, // SPACE
            now: Now,
            lastScannerKey: ScannerOld,
            foreground: MatchingWindow,
            targetProcessName: "MiAppContable",
            targetWindowTitleContains: "");

        result.ShouldBeFalse();
    }

    [Fact]
    public void Pass_When_ForegroundIsNull()
    {
        var result = SpaceSuppressor.ShouldSuppress(
            vk: 0x20,
            now: Now,
            lastScannerKey: ScannerRecent,
            foreground: null,
            targetProcessName: "MiAppContable",
            targetWindowTitleContains: "");

        result.ShouldBeFalse();
    }

    [Fact]
    public void Pass_When_ProcessNameDoesNotMatch()
    {
        var result = SpaceSuppressor.ShouldSuppress(
            vk: 0x20,
            now: Now,
            lastScannerKey: ScannerRecent,
            foreground: new ForegroundWindowInfo("notepad", "Untitled"),
            targetProcessName: "MiAppContable",
            targetWindowTitleContains: "");

        result.ShouldBeFalse();
    }

    [Fact]
    public void Pass_When_TitleDoesNotContainNeedle()
    {
        var result = SpaceSuppressor.ShouldSuppress(
            vk: 0x20,
            now: Now,
            lastScannerKey: ScannerRecent,
            foreground: new ForegroundWindowInfo("MiAppContable", "Reportes mensuales"),
            targetProcessName: "MiAppContable",
            targetWindowTitleContains: "Factura");

        result.ShouldBeFalse();
    }

    [Fact]
    public void Suppress_When_AllConditionsMet()
    {
        var result = SpaceSuppressor.ShouldSuppress(
            vk: 0x20,
            now: Now,
            lastScannerKey: ScannerRecent,
            foreground: MatchingWindow,
            targetProcessName: "MiAppContable",
            targetWindowTitleContains: "Factura");

        result.ShouldBeTrue();
    }

    [Fact]
    public void Suppress_When_BothFiltersEmpty_AndScannerRecent()
    {
        // Empty filters = "apply in any foreground window". Defensive but useful default.
        var result = SpaceSuppressor.ShouldSuppress(
            vk: 0x20,
            now: Now,
            lastScannerKey: ScannerRecent,
            foreground: new ForegroundWindowInfo("anything", "anywhere"),
            targetProcessName: "",
            targetWindowTitleContains: "");

        result.ShouldBeTrue();
    }

    [Fact]
    public void ProcessMatch_IsCaseInsensitive()
    {
        var result = SpaceSuppressor.ShouldSuppress(
            vk: 0x20,
            now: Now,
            lastScannerKey: ScannerRecent,
            foreground: new ForegroundWindowInfo("MIAPPCONTABLE", "x"),
            targetProcessName: "miappcontable",
            targetWindowTitleContains: "");

        result.ShouldBeTrue();
    }

    [Fact]
    public void TitleContains_IsCaseInsensitive()
    {
        var result = SpaceSuppressor.ShouldSuppress(
            vk: 0x20,
            now: Now,
            lastScannerKey: ScannerRecent,
            foreground: new ForegroundWindowInfo("MiAppContable", "Editar FACTURA 1234"),
            targetProcessName: "MiAppContable",
            targetWindowTitleContains: "factura");

        result.ShouldBeTrue();
    }

    [Fact]
    public void ScannerWindowBoundary_99ms_IsRecent()
    {
        var result = SpaceSuppressor.ShouldSuppress(
            vk: 0x20,
            now: Now,
            lastScannerKey: Now.AddMilliseconds(-99),
            foreground: MatchingWindow,
            targetProcessName: "MiAppContable",
            targetWindowTitleContains: "");

        result.ShouldBeTrue();
    }

    [Fact]
    public void ScannerWindowBoundary_100ms_IsOld()
    {
        var result = SpaceSuppressor.ShouldSuppress(
            vk: 0x20,
            now: Now,
            lastScannerKey: Now.AddMilliseconds(-100),
            foreground: MatchingWindow,
            targetProcessName: "MiAppContable",
            targetWindowTitleContains: "");

        result.ShouldBeFalse();
    }

    [Fact]
    public void Pass_When_OnlyTitleConfigured_AndMatches()
    {
        // ProcessName empty, only title filter active.
        var result = SpaceSuppressor.ShouldSuppress(
            vk: 0x20,
            now: Now,
            lastScannerKey: ScannerRecent,
            foreground: new ForegroundWindowInfo("anything", "Bloc de notas - factura.txt"),
            targetProcessName: "",
            targetWindowTitleContains: "factura");

        result.ShouldBeTrue();
    }
}
```

- [ ] **Step 2: Confirm the Core test project still references Core (no changes needed at this point)**

```bash
/usr/bin/grep "ProjectReference" tests/AutolumoBarcodeScannerTool.Tests/AutolumoBarcodeScannerTool.Tests.csproj
```

Expected: `<ProjectReference Include="..\..\src\AutolumoBarcodeScannerTool.Core\AutolumoBarcodeScannerTool.Core.csproj" />`

- [ ] **Step 3: Run the new tests — they MUST fail (type not defined yet)**

```bash
dotnet test tests/AutolumoBarcodeScannerTool.Tests/AutolumoBarcodeScannerTool.Tests.csproj -c Release --filter "FullyQualifiedName~SpaceSuppressorTests" 2>&1 | tail -10
```

Expected: compile error `The type or namespace name 'SpaceSuppressor' could not be found`. (Or — if the old Core files still reference deleted packages — a broader compile failure. Either way, no green tests.)

- [ ] **Step 4: Create `SpaceSuppressor.cs`**

Create `src/AutolumoBarcodeScannerTool.Core/SpaceSuppressor.cs`:

```csharp
namespace AutolumoBarcodeScannerTool.Core;

public static class SpaceSuppressor
{
    public const int VK_SPACE = 0x20;
    public const int ScannerWindowMs = 100;

    // Returns true ⇔ the keystroke should be suppressed at the LL hook layer.
    // Suppress only when ALL of:
    //   • vk is VK_SPACE
    //   • the configured scanner produced a keystroke within ScannerWindowMs
    //   • the foreground window matches the configured target
    // If both filter strings are empty, the target check passes (suppress applies
    // wherever the focus is — least-surprise default).
    public static bool ShouldSuppress(
        int vk,
        DateTime now,
        DateTime lastScannerKey,
        ForegroundWindowInfo? foreground,
        string targetProcessName,
        string targetWindowTitleContains)
    {
        if (vk != VK_SPACE) return false;
        if ((now - lastScannerKey).TotalMilliseconds >= ScannerWindowMs) return false;
        if (foreground is null) return false;

        if (!string.IsNullOrEmpty(targetProcessName) &&
            !string.Equals(foreground.ProcessName, targetProcessName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrEmpty(targetWindowTitleContains) &&
            foreground.WindowTitle.IndexOf(targetWindowTitleContains, StringComparison.OrdinalIgnoreCase) < 0)
            return false;

        return true;
    }
}
```

- [ ] **Step 5: Run tests — they MUST pass**

The old Core files (Models, Transforms, etc.) still exist and reference packages we removed in Task 1; the Core project itself will fail to compile, which blocks the test project too. Before running tests, we need to TEMPORARILY exclude the obsolete files from the build so we can validate `SpaceSuppressor` in isolation.

Add this `ItemGroup` to `src/AutolumoBarcodeScannerTool.Core/AutolumoBarcodeScannerTool.Core.csproj` (between the `</PropertyGroup>` and `</Project>` lines):

```xml
  <ItemGroup>
    <Compile Remove="Models/**" />
    <Compile Remove="Transforms/**" />
    <Compile Remove="Orchestration/**" />
    <Compile Remove="Sinks/**" />
    <Compile Remove="Sources/**" />
    <Compile Remove="Configuration/**" />
  </ItemGroup>
```

This is scaffolding — Task 12 deletes those folders for real and this `ItemGroup` becomes a no-op (and can be removed in Task 11). For now it lets us TDD without giant rewrites blocking us.

Then run:

```bash
dotnet test tests/AutolumoBarcodeScannerTool.Tests/AutolumoBarcodeScannerTool.Tests.csproj -c Release --filter "FullyQualifiedName~SpaceSuppressorTests" 2>&1 | tail -10
```

Expected: all 12 SpaceSuppressorTests pass. Other tests (Orchestrator, Sink, Transform, IniConfigWriter, Serial) will FAIL to compile because their source files reference Core types that no longer exist — that's expected and resolves in Task 12.

If non-SpaceSuppressor tests block test discovery: temporarily set up the same `Compile Remove` shim in the test project (Task 4 cleans this up properly). Add to `tests/AutolumoBarcodeScannerTool.Tests/AutolumoBarcodeScannerTool.Tests.csproj`:

```xml
  <ItemGroup>
    <Compile Remove="Configuration/**" />
    <Compile Remove="Models/**" />
    <Compile Remove="Orchestration/**" />
    <Compile Remove="Sinks/**" />
    <Compile Remove="Sources/**" />
    <Compile Remove="Transforms/**" />
    <Compile Remove="Fakes/**" />
  </ItemGroup>
```

Re-run the test command. Expected: 12 passed, 0 failed.

- [ ] **Step 6: Commit**

```bash
git add src/AutolumoBarcodeScannerTool.Core/SpaceSuppressor.cs \
        src/AutolumoBarcodeScannerTool.Core/AutolumoBarcodeScannerTool.Core.csproj \
        tests/AutolumoBarcodeScannerTool.Tests/SpaceSuppressorTests.cs \
        tests/AutolumoBarcodeScannerTool.Tests/AutolumoBarcodeScannerTool.Tests.csproj
git commit -m "feat(core): SpaceSuppressor pure-logic decision module + 12 tests"
```

---

## Task 4: TDD `ScannerConfig` + `ConfigIo`

`ScannerConfig` is the persisted state. `ConfigIo` reads/writes a simple `key=value` `.ini` and tolerates unknown keys (lets v0.2.0 read v0.1.5 files without errors).

**Files:**
- Create: `src/AutolumoBarcodeScannerTool.Core/ScannerConfig.cs`
- Create: `src/AutolumoBarcodeScannerTool.Core/ConfigIo.cs`
- Create: `tests/AutolumoBarcodeScannerTool.Tests/ConfigIoTests.cs`

- [ ] **Step 1: Create `ScannerConfig.cs`**

```csharp
namespace AutolumoBarcodeScannerTool.Core;

public sealed record ScannerConfig(
    string VendorId,
    string ProductId,
    string TargetProcessName,
    string TargetWindowTitleContains,
    bool AutostartEnabled)
{
    public static ScannerConfig Default => new(
        VendorId: "0x0000",
        ProductId: "0x0000",
        TargetProcessName: "",
        TargetWindowTitleContains: "",
        AutostartEnabled: true);
}
```

- [ ] **Step 2: Write failing tests for `ConfigIo`**

Create `tests/AutolumoBarcodeScannerTool.Tests/ConfigIoTests.cs`:

```csharp
using AutolumoBarcodeScannerTool.Core;
using Shouldly;
using Xunit;

namespace AutolumoBarcodeScannerTool.Tests;

public class ConfigIoTests
{
    private static string TempPath() => Path.Combine(Path.GetTempPath(),
        $"autolumo-cfgio-{Guid.NewGuid():N}.ini");

    [Fact]
    public void Load_MissingFile_ReturnsDefault()
    {
        var path = TempPath();
        // do not create file

        var cfg = ConfigIo.Load(path);

        cfg.ShouldBe(ScannerConfig.Default);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var path = TempPath();
        try
        {
            var original = new ScannerConfig(
                VendorId: "0x0C2E",
                ProductId: "0x0B61",
                TargetProcessName: "MiAppContable",
                TargetWindowTitleContains: "Factura",
                AutostartEnabled: false);

            ConfigIo.Save(path, original);
            var loaded = ConfigIo.Load(path);

            loaded.ShouldBe(original);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_PartialFile_FillsMissingFieldsWithDefault()
    {
        var path = TempPath();
        File.WriteAllText(path,
            "VendorId=0x1234\n" +
            "ProductId=0xABCD\n");
        try
        {
            var cfg = ConfigIo.Load(path);

            cfg.VendorId.ShouldBe("0x1234");
            cfg.ProductId.ShouldBe("0xABCD");
            cfg.TargetProcessName.ShouldBe(ScannerConfig.Default.TargetProcessName);
            cfg.AutostartEnabled.ShouldBe(ScannerConfig.Default.AutostartEnabled);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_IgnoresUnknownKeys()
    {
        // Compatibility with the v0.1.5 .ini which had a Scanner:Serial:BaudRate etc.
        var path = TempPath();
        File.WriteAllText(path,
            "VendorId=0xAAAA\n" +
            "Scanner:Serial:BaudRate=9600\n" +
            "Scanner:Diagnostics:VerboseLogging=true\n" +
            "ProductId=0xBBBB\n");
        try
        {
            var cfg = ConfigIo.Load(path);

            cfg.VendorId.ShouldBe("0xAAAA");
            cfg.ProductId.ShouldBe("0xBBBB");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_IgnoresCommentsAndBlankLinesAndSectionHeaders()
    {
        var path = TempPath();
        File.WriteAllText(path,
            "; comment line\n" +
            "# hash comment\n" +
            "[Scanner]\n" +
            "\n" +
            "VendorId=0x9999\n" +
            "  ProductId  =  0x8888  \n" +
            "TargetProcessName=AppX\n");
        try
        {
            var cfg = ConfigIo.Load(path);

            cfg.VendorId.ShouldBe("0x9999");
            cfg.ProductId.ShouldBe("0x8888");
            cfg.TargetProcessName.ShouldBe("AppX");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Save_OverwritesExistingFile()
    {
        var path = TempPath();
        try
        {
            ConfigIo.Save(path, new ScannerConfig("0x1111", "0x2222", "AppA", "", true));
            ConfigIo.Save(path, new ScannerConfig("0x3333", "0x4444", "AppB", "filter", false));

            var loaded = ConfigIo.Load(path);

            loaded.VendorId.ShouldBe("0x3333");
            loaded.ProductId.ShouldBe("0x4444");
            loaded.TargetProcessName.ShouldBe("AppB");
            loaded.TargetWindowTitleContains.ShouldBe("filter");
            loaded.AutostartEnabled.ShouldBeFalse();
        }
        finally { File.Delete(path); }
    }
}
```

- [ ] **Step 3: Run tests — they MUST fail**

```bash
dotnet test tests/AutolumoBarcodeScannerTool.Tests/AutolumoBarcodeScannerTool.Tests.csproj -c Release --filter "FullyQualifiedName~ConfigIoTests" 2>&1 | tail -10
```

Expected: compile error `The type or namespace name 'ConfigIo' could not be found`.

- [ ] **Step 4: Create `ConfigIo.cs`**

```csharp
namespace AutolumoBarcodeScannerTool.Core;

public static class ConfigIo
{
    private const string KeyVendor = "VendorId";
    private const string KeyProduct = "ProductId";
    private const string KeyProcess = "TargetProcessName";
    private const string KeyTitle = "TargetWindowTitleContains";
    private const string KeyAutostart = "AutostartEnabled";

    public static ScannerConfig Load(string path)
    {
        if (!File.Exists(path)) return ScannerConfig.Default;

        var d = ScannerConfig.Default;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.TrimStart();
            if (line.Length == 0) continue;
            if (line.StartsWith(';') || line.StartsWith('#')) continue;
            if (line.StartsWith('[') && line.EndsWith(']')) continue; // section header — ignored

            var eq = line.IndexOf('=');
            if (eq < 0) continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            values[key] = value;
        }

        return d with
        {
            VendorId = values.TryGetValue(KeyVendor, out var v) ? v : d.VendorId,
            ProductId = values.TryGetValue(KeyProduct, out var p) ? p : d.ProductId,
            TargetProcessName = values.TryGetValue(KeyProcess, out var pr) ? pr : d.TargetProcessName,
            TargetWindowTitleContains = values.TryGetValue(KeyTitle, out var t) ? t : d.TargetWindowTitleContains,
            AutostartEnabled = values.TryGetValue(KeyAutostart, out var a)
                ? bool.TryParse(a, out var ab) && ab
                : d.AutostartEnabled
        };
    }

    public static void Save(string path, ScannerConfig config)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var lines = new[]
        {
            $"{KeyVendor}={config.VendorId}",
            $"{KeyProduct}={config.ProductId}",
            $"{KeyProcess}={config.TargetProcessName}",
            $"{KeyTitle}={config.TargetWindowTitleContains}",
            $"{KeyAutostart}={(config.AutostartEnabled ? "true" : "false")}"
        };
        File.WriteAllLines(path, lines);
    }
}
```

- [ ] **Step 5: Run tests — they MUST pass**

```bash
dotnet test tests/AutolumoBarcodeScannerTool.Tests/AutolumoBarcodeScannerTool.Tests.csproj -c Release --filter "FullyQualifiedName~ConfigIoTests" 2>&1 | tail -10
```

Expected: 6 ConfigIoTests pass.

- [ ] **Step 6: Run all enabled tests (SpaceSuppressor + ConfigIo = 18 total)**

```bash
dotnet test tests/AutolumoBarcodeScannerTool.Tests/AutolumoBarcodeScannerTool.Tests.csproj -c Release 2>&1 | tail -10
```

Expected: 18 passed, 0 failed.

- [ ] **Step 7: Commit**

```bash
git add src/AutolumoBarcodeScannerTool.Core/ScannerConfig.cs \
        src/AutolumoBarcodeScannerTool.Core/ConfigIo.cs \
        tests/AutolumoBarcodeScannerTool.Tests/ConfigIoTests.cs
git commit -m "feat(core): ScannerConfig record + ConfigIo (load/save .ini) + 6 tests"
```

---

## Task 5: Clean test project of obsolete packages

The test csproj currently references `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options`, etc., used only by tests we're going to delete. Strip these now so the remaining tests are minimal.

**Files:**
- Modify: `tests/AutolumoBarcodeScannerTool.Tests/AutolumoBarcodeScannerTool.Tests.csproj`

- [ ] **Step 1: Replace csproj content**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="Shouldly" Version="4.2.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\AutolumoBarcodeScannerTool.Core\AutolumoBarcodeScannerTool.Core.csproj" />
  </ItemGroup>

  <!-- TEMP: excluded until Task 12 deletes these folders. -->
  <ItemGroup>
    <Compile Remove="Configuration/**" />
    <Compile Remove="Models/**" />
    <Compile Remove="Orchestration/**" />
    <Compile Remove="Sinks/**" />
    <Compile Remove="Sources/**" />
    <Compile Remove="Transforms/**" />
    <Compile Remove="Fakes/**" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Verify tests still run**

```bash
dotnet test tests/AutolumoBarcodeScannerTool.Tests/AutolumoBarcodeScannerTool.Tests.csproj -c Release 2>&1 | tail -10
```

Expected: 18 passed, 0 failed.

- [ ] **Step 3: Commit**

```bash
git add tests/AutolumoBarcodeScannerTool.Tests/AutolumoBarcodeScannerTool.Tests.csproj
git commit -m "refactor(tests): drop Logging/Options packages — only Core left"
```

---

## Task 6: Create `Win32/ForegroundWindow.cs`

Win32 P/Invoke helper. Returns `ForegroundWindowInfo?` — null if no foreground or query fails. No unit tests (would need Win32 mocking).

**Files:**
- Create: `src/AutolumoBarcodeScannerTool/Win32/ForegroundWindow.cs`

- [ ] **Step 1: Create file**

```csharp
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using AutolumoBarcodeScannerTool.Core;

namespace AutolumoBarcodeScannerTool.Win32;

internal static class ForegroundWindow
{
    public static ForegroundWindowInfo? GetCurrent()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;

        _ = GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0) return null;

        string processName;
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            processName = proc.ProcessName;
        }
        catch
        {
            return null;
        }

        var titleLen = GetWindowTextLength(hwnd);
        var title = "";
        if (titleLen > 0)
        {
            var sb = new StringBuilder(titleLen + 1);
            _ = GetWindowText(hwnd, sb, sb.Capacity);
            title = sb.ToString();
        }

        return new ForegroundWindowInfo(processName, title);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr hWnd);
}
```

- [ ] **Step 2: No build verification yet** — the WinForms csproj still references obsolete files. Verification happens in Task 11.

- [ ] **Step 3: Commit**

```bash
git add src/AutolumoBarcodeScannerTool/Win32/ForegroundWindow.cs
git commit -m "feat(win32): ForegroundWindow helper — process name + window title"
```

---

## Task 7: Simplify `LowLevelKeyboardHook` callback signature

The current hook callback is `Func<int, IntPtr, bool>` (vkCode + dwExtraInfo, used by the sentinel for self-suppression). After this refactor we never inject, so we never need `dwExtraInfo`.

**Files:**
- Modify: `src/AutolumoBarcodeScannerTool/Hid/LowLevelKeyboardHook.cs`

- [ ] **Step 1: Replace file content**

```csharp
using System.Runtime.InteropServices;

namespace AutolumoBarcodeScannerTool.Hid;

internal sealed class LowLevelKeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private readonly Func<int, bool> _shouldSuppress;
    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId = IntPtr.Zero;

    public LowLevelKeyboardHook(Func<int, bool> shouldSuppress)
    {
        _shouldSuppress = shouldSuppress;
        _proc = HookCallback;
    }

    public void Install()
    {
        if (_hookId != IntPtr.Zero) return;
        var hMod = GetModuleHandle(null);
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, hMod, 0);
        if (_hookId == IntPtr.Zero)
            throw new InvalidOperationException($"SetWindowsHookEx falló: {Marshal.GetLastWin32Error()}");
    }

    public void Uninstall()
    {
        if (_hookId == IntPtr.Zero) return;
        _ = UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0) return CallNextHookEx(_hookId, nCode, wParam, lParam);

        var msg = wParam.ToInt32();
        if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            if (_shouldSuppress((int)data.vkCode))
                return (IntPtr)1;
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
```

- [ ] **Step 2: Commit**

```bash
git add src/AutolumoBarcodeScannerTool/Hid/LowLevelKeyboardHook.cs
git commit -m "refactor(hid): LowLevelKeyboardHook callback Func<int,bool> — no dwExtraInfo"
```

---

## Task 8: Create `Hid/ScannerKeyTracker.cs` — replaces `HidKeyboardInputSource`

A drastically reduced NativeWindow + Raw Input handler. The only state it exposes is `LastScannerKeyTime`. No payload accumulation, no ToUnicodeEx, no terminator detection, no ScanEvent emission.

**Files:**
- Create: `src/AutolumoBarcodeScannerTool/Hid/ScannerKeyTracker.cs`

- [ ] **Step 1: Create file**

```csharp
using System.Runtime.InteropServices;
using static AutolumoBarcodeScannerTool.Hid.RawInputInterop;

namespace AutolumoBarcodeScannerTool.Hid;

// Registers a Raw Input hidden window for keyboard devices. Every WM_INPUT
// whose source device matches "VID_xxxx&PID_yyyy" stamps the current time
// into LastScannerKeyTime. The LL hook reads that value to decide whether a
// VK_SPACE belongs to a scanner burst or to the human keyboard.
internal sealed class ScannerKeyTracker : NativeWindow, IDisposable
{
    private readonly string _scannerDeviceFragment;
    public DateTime LastScannerKeyTime { get; private set; } = DateTime.MinValue;

    public ScannerKeyTracker(string vendorIdHex, string productIdHex)
    {
        _scannerDeviceFragment = $"VID_{NormalizeHex(vendorIdHex)}&PID_{NormalizeHex(productIdHex)}";
    }

    public void Start()
    {
        var cp = new CreateParams { Caption = "AutolumoScannerKeyTracker", X = 0, Y = 0, Width = 0, Height = 0 };
        CreateHandle(cp);

        var devices = new[]
        {
            new RAWINPUTDEVICE
            {
                UsagePage = HID_USAGE_PAGE_GENERIC,
                Usage = HID_USAGE_GENERIC_KEYBOARD,
                Flags = RIDEV_INPUTSINK,
                WindowHandle = Handle
            }
        };
        if (!RegisterRawInputDevices(devices, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
            throw new InvalidOperationException(
                $"RegisterRawInputDevices falló: {Marshal.GetLastWin32Error()}");
    }

    public void Stop()
    {
        if (Handle != IntPtr.Zero) DestroyHandle();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_INPUT) HandleRawInput(m.LParam);
        base.WndProc(ref m);
    }

    private void HandleRawInput(IntPtr hRawInput)
    {
        uint size = 0;
        _ = GetRawInputData(hRawInput, RID_INPUT, IntPtr.Zero, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
        if (size == 0) return;

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            _ = GetRawInputData(hRawInput, RID_INPUT, buffer, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
            var input = Marshal.PtrToStructure<RAWINPUT>(buffer);
            if (input.Header.Type != 1) return; // RIM_TYPEKEYBOARD == 1

            // Solo keydown — keyup también dispara WM_INPUT pero no nos importa
            // marcar la ventana de actividad para el keyup.
            const ushort RI_KEY_BREAK = 0x01;
            if ((input.Keyboard.Flags & RI_KEY_BREAK) != 0) return;

            var deviceName = GetDeviceName(input.Header.DeviceHandle) ?? "";
            if (!deviceName.Contains(_scannerDeviceFragment, StringComparison.OrdinalIgnoreCase)) return;

            LastScannerKeyTime = DateTime.UtcNow;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string NormalizeHex(string s)
    {
        var t = s.Trim();
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) t = t[2..];
        return t.ToUpperInvariant().PadLeft(4, '0');
    }

    public void Dispose() => Stop();
}
```

- [ ] **Step 2: Commit**

```bash
git add src/AutolumoBarcodeScannerTool/Hid/ScannerKeyTracker.cs
git commit -m "feat(hid): ScannerKeyTracker — Raw Input → LastScannerKeyTime"
```

---

## Task 9: Create `Tray/TrayController.cs` — replaces the v0.1.5 TrayIconController

The new tray has only two menu items: "Configurar…" and "Salir". No enable/disable toggle, no status indicator (the bloqueador is always on).

**Files:**
- Create: `src/AutolumoBarcodeScannerTool/Tray/TrayController.cs`

- [ ] **Step 1: Create file**

```csharp
namespace AutolumoBarcodeScannerTool.Tray;

internal sealed class TrayController : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly Action _openSettings;

    public TrayController(Action openSettings)
    {
        _openSettings = openSettings;
        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "Autolumo — bloqueador de espacios del lector",
            ContextMenuStrip = BuildMenu()
        };
        _icon.DoubleClick += (_, _) => _openSettings();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Configurar…", null, (_, _) => _openSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => Application.Exit());
        return menu;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/AutolumoBarcodeScannerTool/Tray/TrayController.cs
git commit -m "feat(tray): TrayController — minimal NotifyIcon (Configurar/Salir)"
```

---

## Task 10: Rewrite `Tray/SettingsForm.cs` — one tab, three input groups

Replaces the v0.1.5 5-tab form with a single panel.

**Files:**
- Modify (full rewrite): `src/AutolumoBarcodeScannerTool/Tray/SettingsForm.cs`

- [ ] **Step 1: Replace file content**

```csharp
using AutolumoBarcodeScannerTool.Autostart;
using AutolumoBarcodeScannerTool.Core;
using AutolumoBarcodeScannerTool.Hid;

namespace AutolumoBarcodeScannerTool.Tray;

internal sealed class SettingsForm : Form
{
    private readonly ScannerConfig _initial;
    private readonly AutostartManager _autostart;
    private readonly string _configPath;

    private readonly ComboBox _hidDevice = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 480 };
    private readonly TextBox _vid = new() { Width = 100, ReadOnly = true };
    private readonly TextBox _pid = new() { Width = 100, ReadOnly = true };
    private readonly TextBox _processName = new() { Width = 480 };
    private readonly TextBox _windowTitleContains = new() { Width = 480 };
    private readonly CheckBox _autostartChk = new() { Text = "Iniciar con Windows", AutoSize = true };
    private readonly Button _save = new() { Text = "Guardar", Width = 100 };
    private readonly Button _cancel = new() { Text = "Cancelar", Width = 100 };

    public SettingsForm(ScannerConfig initial, AutostartManager autostart, string configPath)
    {
        _initial = initial;
        _autostart = autostart;
        _configPath = configPath;

        Text = "Autolumo — Configuración";
        Width = 560; Height = 480;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        Build();
        Load();
    }

    private void Build()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(16)
        };

        panel.Controls.Add(Header("Dispositivo lector"));
        panel.Controls.Add(Label("Detectados en este equipo:"));
        panel.Controls.Add(_hidDevice);

        var vidPidRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 12)
        };
        vidPidRow.Controls.Add(new Label { Text = "VID:", AutoSize = true, Padding = new Padding(0, 6, 4, 0) });
        vidPidRow.Controls.Add(_vid);
        vidPidRow.Controls.Add(new Label { Text = "  PID:", AutoSize = true, Padding = new Padding(8, 6, 4, 0) });
        vidPidRow.Controls.Add(_pid);
        panel.Controls.Add(vidPidRow);

        panel.Controls.Add(Header("Ventana objetivo"));
        panel.Controls.Add(Label("Nombre de proceso (sin .exe):"));
        panel.Controls.Add(_processName);
        panel.Controls.Add(Label("Título contiene (opcional):"));
        panel.Controls.Add(_windowTitleContains);
        panel.Controls.Add(new Label
        {
            Text = "Si ambos quedan vacíos, los espacios del lector se suprimen en cualquier app.",
            ForeColor = Color.DimGray,
            AutoSize = false,
            Width = 480,
            Height = 32,
            Margin = new Padding(0, 4, 0, 12)
        });

        panel.Controls.Add(Header("Arranque"));
        panel.Controls.Add(_autostartChk);

        Controls.Add(panel);

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 48,
            Padding = new Padding(16, 8, 16, 8)
        };
        _save.Click += (_, _) => Save();
        _cancel.Click += (_, _) => Close();
        bar.Controls.Add(_save);
        bar.Controls.Add(_cancel);
        Controls.Add(bar);

        _hidDevice.SelectedIndexChanged += (_, _) =>
        {
            if (_hidDevice.SelectedItem is HidDeviceDescriptor d)
            {
                _vid.Text = d.VendorId;
                _pid.Text = d.ProductId;
            }
        };
    }

    private void Load()
    {
        foreach (var d in HidDeviceEnumerator.Enumerate())
            _hidDevice.Items.Add(d);

        // Pre-select the device matching the saved VID/PID, if any.
        var savedVid = NormalizeHex(_initial.VendorId);
        var savedPid = NormalizeHex(_initial.ProductId);
        for (var i = 0; i < _hidDevice.Items.Count; i++)
        {
            if (_hidDevice.Items[i] is HidDeviceDescriptor d &&
                string.Equals(NormalizeHex(d.VendorId), savedVid, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(NormalizeHex(d.ProductId), savedPid, StringComparison.OrdinalIgnoreCase))
            {
                _hidDevice.SelectedIndex = i;
                break;
            }
        }

        _vid.Text = savedVid;
        _pid.Text = savedPid;
        _processName.Text = _initial.TargetProcessName;
        _windowTitleContains.Text = _initial.TargetWindowTitleContains;
        _autostartChk.Checked = _autostart.IsEnabled();
    }

    private void Save()
    {
        try
        {
            var config = new ScannerConfig(
                VendorId: _vid.Text.Trim(),
                ProductId: _pid.Text.Trim(),
                TargetProcessName: _processName.Text.Trim(),
                TargetWindowTitleContains: _windowTitleContains.Text.Trim(),
                AutostartEnabled: _autostartChk.Checked);

            ConfigIo.Save(_configPath, config);

            var exePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("No se pudo determinar la ruta del ejecutable.");
            if (config.AutostartEnabled) _autostart.Enable(exePath);
            else _autostart.Disable();

            MessageBox.Show(
                "Configuración guardada.\n\nReinicia la aplicación para aplicar los cambios " +
                "del dispositivo (VID/PID). El nombre de proceso y el filtro de título se aplican " +
                "tras reiniciar también.",
                "Autolumo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al guardar: {ex.Message}", "Autolumo",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static Label Header(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font(Control.DefaultFont, FontStyle.Bold),
        Margin = new Padding(0, 8, 0, 6)
    };

    private static Label Label(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(0, 0, 0, 2)
    };

    private static string NormalizeHex(string s)
    {
        var t = s.Trim();
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) t = t[2..];
        return t.ToUpperInvariant().PadLeft(4, '0');
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/AutolumoBarcodeScannerTool/Tray/SettingsForm.cs
git commit -m "refactor(tray): SettingsForm rewritten — one panel, four inputs"
```

---

## Task 11: Rewrite `Program.cs` + simplify WinForms csproj + appsettings.default.ini

Wires together the tracker + hook + tray + settings. No `IHostBuilder`, no DI, no Serilog.

**Files:**
- Modify (full rewrite): `src/AutolumoBarcodeScannerTool/Program.cs`
- Modify: `src/AutolumoBarcodeScannerTool/AutolumoBarcodeScannerTool.csproj`
- Modify: `src/AutolumoBarcodeScannerTool/appsettings.default.ini`

- [ ] **Step 1: Replace `Program.cs`**

```csharp
using System.IO;
using System.Threading;
using AutolumoBarcodeScannerTool.Autostart;
using AutolumoBarcodeScannerTool.Core;
using AutolumoBarcodeScannerTool.Hid;
using AutolumoBarcodeScannerTool.Tray;
using AutolumoBarcodeScannerTool.Win32;

namespace AutolumoBarcodeScannerTool;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Single-instance guard scoped to the current user session.
        using var mutex = new Mutex(initiallyOwned: true,
            name: @"Local\AutolumoBarcodeScannerTool_v1", out var firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show(
                "Autolumo ya está en ejecución. Revisa la bandeja del sistema.",
                "Autolumo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Config lives in %LOCALAPPDATA%\AutolumoBarcodeScannerTool\appsettings.ini.
        // On first run we seed it from appsettings.default.ini next to the exe.
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutolumoBarcodeScannerTool");
        Directory.CreateDirectory(configDir);
        var configPath = Path.Combine(configDir, "appsettings.ini");
        if (!File.Exists(configPath))
        {
            var seedPath = Path.Combine(AppContext.BaseDirectory, "appsettings.default.ini");
            if (File.Exists(seedPath)) File.Copy(seedPath, configPath);
        }

        var config = ConfigIo.Load(configPath);
        var autostart = new AutostartManager();

        // Tracker registers a hidden Raw Input window for the configured VID/PID.
        // The window MUST be created on the STA Main thread that owns Application.Run().
        using var tracker = new ScannerKeyTracker(config.VendorId, config.ProductId);
        try
        {
            tracker.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"No se pudo registrar el lector HID (VID={config.VendorId}, PID={config.ProductId}):\n\n{ex.Message}\n\n" +
                "Abre Configurar… desde la bandeja para seleccionar otro dispositivo.",
                "Autolumo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            // We continue running — user can fix config from the tray.
        }

        // LL hook callback: combine tracker timestamp + foreground window check.
        using var hook = new LowLevelKeyboardHook(vk => SpaceSuppressor.ShouldSuppress(
            vk,
            DateTime.UtcNow,
            tracker.LastScannerKeyTime,
            ForegroundWindow.GetCurrent(),
            config.TargetProcessName,
            config.TargetWindowTitleContains));
        hook.Install();

        using var tray = new TrayController(() =>
        {
            using var form = new SettingsForm(config, autostart, configPath);
            form.ShowDialog();
        });

        Application.Run(new ApplicationContext());
    }
}
```

- [ ] **Step 2: Replace `AutolumoBarcodeScannerTool.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <AssemblyName>AutolumoBarcodeScannerTool</AssemblyName>
    <RootNamespace>AutolumoBarcodeScannerTool</RootNamespace>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\AutolumoBarcodeScannerTool.Core\AutolumoBarcodeScannerTool.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="System.Management" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <None Update="appsettings.default.ini">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Replace `appsettings.default.ini`**

```ini
; Autolumo Barcode Scanner Tool — configuración por defecto.
; En tiempo de ejecución se copia a %LOCALAPPDATA%\AutolumoBarcodeScannerTool\appsettings.ini
; donde el usuario la edita desde la pestaña "Configurar…".

VendorId=0x0000
ProductId=0x0000
TargetProcessName=
TargetWindowTitleContains=
AutostartEnabled=true
```

- [ ] **Step 4: Commit**

```bash
git add src/AutolumoBarcodeScannerTool/Program.cs \
        src/AutolumoBarcodeScannerTool/AutolumoBarcodeScannerTool.csproj \
        src/AutolumoBarcodeScannerTool/appsettings.default.ini
git commit -m "refactor: Program.cs no-DI + csproj only System.Management + 5-line ini"
```

---

## Task 12: Delete obsolete source files (requires user authorization for each `git rm` batch)

The classifier blocks mass `git rm -r` of pre-existing source folders. We do this in named batches; the user approves each batch before it runs.

**Files to delete (all paths relative to repo root):**

Batch A — Core obsolete folders (replaced by SpaceSuppressor/ConfigIo/etc):
```
src/AutolumoBarcodeScannerTool.Core/Configuration/
src/AutolumoBarcodeScannerTool.Core/Models/
src/AutolumoBarcodeScannerTool.Core/Orchestration/
src/AutolumoBarcodeScannerTool.Core/Sinks/
src/AutolumoBarcodeScannerTool.Core/Sources/
src/AutolumoBarcodeScannerTool.Core/Transforms/
```

Batch B — WinForms obsolete (replaced by ScannerKeyTracker/ForegroundWindow):
```
src/AutolumoBarcodeScannerTool/Hid/HidKeyboardInputSource.cs
src/AutolumoBarcodeScannerTool/Hid/InjectionMarker.cs
src/AutolumoBarcodeScannerTool/Win32/SendInputInjector.cs
src/AutolumoBarcodeScannerTool/Win32/Win32ForegroundWindowProvider.cs
```

Batch C — Obsolete tests:
```
tests/AutolumoBarcodeScannerTool.Tests/Configuration/
tests/AutolumoBarcodeScannerTool.Tests/Models/
tests/AutolumoBarcodeScannerTool.Tests/Orchestration/
tests/AutolumoBarcodeScannerTool.Tests/Sinks/
tests/AutolumoBarcodeScannerTool.Tests/Sources/
tests/AutolumoBarcodeScannerTool.Tests/Transforms/
tests/AutolumoBarcodeScannerTool.Tests/Fakes/
```

- [ ] **Step 1: Ask user for explicit OK on Batch A**

Show the list above to the user, get confirmation.

- [ ] **Step 2: Delete Batch A**

```bash
git rm -r src/AutolumoBarcodeScannerTool.Core/Configuration \
         src/AutolumoBarcodeScannerTool.Core/Models \
         src/AutolumoBarcodeScannerTool.Core/Orchestration \
         src/AutolumoBarcodeScannerTool.Core/Sinks \
         src/AutolumoBarcodeScannerTool.Core/Sources \
         src/AutolumoBarcodeScannerTool.Core/Transforms
```

- [ ] **Step 3: Ask user for explicit OK on Batch B**

- [ ] **Step 4: Delete Batch B**

```bash
git rm src/AutolumoBarcodeScannerTool/Hid/HidKeyboardInputSource.cs \
       src/AutolumoBarcodeScannerTool/Hid/InjectionMarker.cs \
       src/AutolumoBarcodeScannerTool/Win32/SendInputInjector.cs \
       src/AutolumoBarcodeScannerTool/Win32/Win32ForegroundWindowProvider.cs
```

- [ ] **Step 5: Ask user for explicit OK on Batch C**

- [ ] **Step 6: Delete Batch C**

```bash
git rm -r tests/AutolumoBarcodeScannerTool.Tests/Configuration \
         tests/AutolumoBarcodeScannerTool.Tests/Models \
         tests/AutolumoBarcodeScannerTool.Tests/Orchestration \
         tests/AutolumoBarcodeScannerTool.Tests/Sinks \
         tests/AutolumoBarcodeScannerTool.Tests/Sources \
         tests/AutolumoBarcodeScannerTool.Tests/Transforms \
         tests/AutolumoBarcodeScannerTool.Tests/Fakes
```

- [ ] **Step 7: Remove the `Compile Remove` shims from Core and Test csprojs**

In `src/AutolumoBarcodeScannerTool.Core/AutolumoBarcodeScannerTool.Core.csproj`, remove the `<ItemGroup>` block with `Compile Remove="Models/**"` etc. (the folders no longer exist).

In `tests/AutolumoBarcodeScannerTool.Tests/AutolumoBarcodeScannerTool.Tests.csproj`, remove the second `<ItemGroup>` (the TEMP block).

- [ ] **Step 8: Verify clean compile of Core**

```bash
dotnet build src/AutolumoBarcodeScannerTool.Core/AutolumoBarcodeScannerTool.Core.csproj -c Release 2>&1 | tail -5
```

Expected: `1 projects, 0 errors, 0 warnings`.

- [ ] **Step 9: Run all tests**

```bash
dotnet test tests/AutolumoBarcodeScannerTool.Tests/AutolumoBarcodeScannerTool.Tests.csproj -c Release 2>&1 | tail -10
```

Expected: 18 passed, 0 failed.

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "chore: delete obsolete source files + tests (Core/Sinks/Transform/...)"
```

---

## Task 13: Update `AutolumoBarcodeScannerTool.sln` if needed

The .sln currently lists the Core project. The Core project STILL EXISTS (with only 4 files left in it) so the .sln stays valid as-is. No changes expected. Verify:

- [ ] **Step 1: Restore the full solution**

```bash
dotnet restore AutolumoBarcodeScannerTool.sln 2>&1 | tail -10
```

Expected: restore succeeds.

- [ ] **Step 2: If restore fails, inspect with the user**

(No commit if no change.)

---

## Task 14: Update CI workflows — one framework-dependent artifact

The current `.github/workflows/release.yml` builds TWO artifacts (self-contained + framework-dependent). For a single-equipment solution where the target PC has the .NET 10 Desktop Runtime installed, we ship ONE framework-dependent zip (~2 MB instead of ~50 MB).

The csproj has `<SelfContained>true</SelfContained>` hardcoded. The CLI flag `--self-contained false` does NOT override this — we MUST use `-p:SelfContained=false` (MSBuild property override) instead.

**Files:**
- Modify: `.github/workflows/release.yml`
- Modify: `.github/workflows/build.yml`

- [ ] **Step 1: Inspect current release workflow**

```bash
cat .github/workflows/release.yml
```

- [ ] **Step 2: Rewrite `release.yml`**

```yaml
name: release

on:
  push:
    tags:
      - 'v*'

jobs:
  release:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore
        run: dotnet restore AutolumoBarcodeScannerTool.sln

      - name: Run tests
        run: dotnet test tests/AutolumoBarcodeScannerTool.Tests/AutolumoBarcodeScannerTool.Tests.csproj -c Release --no-restore

      - name: Publish single-file framework-dependent
        # -p:SelfContained=false is the MSBuild property override; the CLI flag
        # --self-contained false does NOT override the csproj <SelfContained>true</>.
        run: |
          dotnet publish src/AutolumoBarcodeScannerTool/AutolumoBarcodeScannerTool.csproj `
            -c Release `
            -r win-x64 `
            -p:SelfContained=false `
            -o publish/fxdep

      - name: Package zip
        run: |
          $version = "${{ github.ref_name }}".TrimStart("v")
          $zip = "AutolumoBarcodeScannerTool-$version-win-x64-fxdep.zip"
          Compress-Archive -Path publish/fxdep/* -DestinationPath $zip
          echo "ZIP=$zip" >> $env:GITHUB_ENV

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          files: ${{ env.ZIP }}
          tag_name: ${{ github.ref_name }}
          generate_release_notes: true
          body: |
            ## Instalación

            Requiere **.NET 10 Desktop Runtime (x64)** instalado en el equipo destino.
            Si no lo tienes: https://dotnet.microsoft.com/download/dotnet/10.0/runtime
            (elige "Desktop Runtime").

            1. Descarga `AutolumoBarcodeScannerTool-<version>-win-x64-fxdep.zip`
            2. Extrae en una carpeta persistente
            3. Ejecuta `AutolumoBarcodeScannerTool.exe`
            4. Tray → Configurar… para seleccionar el lector
```

- [ ] **Step 3: Rewrite `build.yml`**

```yaml
name: build

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore
        run: dotnet restore AutolumoBarcodeScannerTool.sln

      - name: Build
        run: dotnet build AutolumoBarcodeScannerTool.sln -c Release --no-restore

      - name: Test
        run: dotnet test tests/AutolumoBarcodeScannerTool.Tests/AutolumoBarcodeScannerTool.Tests.csproj -c Release --no-restore
```

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/release.yml .github/workflows/build.yml
git commit -m "ci: simplify to one artifact (self-contained only)"
```

---

## Task 15: Update README

The README still describes the v0.1.5 pipeline (Serial + HID + transform + sink + autostart + diagnostics tab). Rewrite to match the new minimal behavior.

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Inspect current README**

```bash
cat README.md | head -50
```

- [ ] **Step 2: Replace contents (preserve repo-specific badges if any at top)**

Replace the file body (everything after any badge block) with:

```markdown
# Autolumo Barcode Scanner Tool

App de bandeja para un único equipo Windows que **suprime los espacios** generados por un lector de códigos de barra HID-keyboard, dejando intacto el resto de las teclas (las del lector y las del teclado humano).

## Cómo funciona

El lector dispara teclas a >100 ch/s; el teclado humano teclea a 5-10 ch/s. La app combina dos señales para decidir qué espacios suprimir:

1. **Origen del dispositivo:** un `WM_INPUT` Raw Input registrado para el VID/PID del lector marca cada keystroke con un timestamp.
2. **Ventana activa:** la app sólo actúa si el foreground coincide con el proceso/título configurado.

El callback de `WH_KEYBOARD_LL` suprime un `VK_SPACE` ⇔ los dos chequeos pasan dentro de los últimos 100 ms.

## Configuración (`Configurar…` desde la bandeja)

- **Dispositivo lector:** ComboBox con todos los HID-keyboards detectados.
- **Ventana objetivo:** nombre de proceso (sin `.exe`) y/o substring del título. Si ambos vacíos, suprime espacios del lector en cualquier app.
- **Iniciar con Windows:** registra/desregistra en `HKCU\…\Run`.

El config persiste en `%LOCALAPPDATA%\AutolumoBarcodeScannerTool\appsettings.ini` (texto plano, `clave=valor`, 5 líneas). Para aplicar cambios reiniciar la app desde la bandeja.

## Instalación

**Requisito previo:** .NET 10 Desktop Runtime (x64). Descárgalo de https://dotnet.microsoft.com/download/dotnet/10.0/runtime (elige "Desktop Runtime"). El zip de release es framework-dependent (~2 MB) — necesita el runtime en el equipo destino.

1. Descarga `AutolumoBarcodeScannerTool-<version>-win-x64-fxdep.zip` del último release.
2. Extrae a una carpeta persistente (ej. `C:\Tools\Autolumo`).
3. Ejecuta `AutolumoBarcodeScannerTool.exe`. Aparece un ícono en la bandeja.
4. Click derecho → **Configurar…** → elige tu lector → guardar → reinicia la app.

No requiere instalador, no escribe en `Program Files`, no requiere admin (excepto la primera vez que registre autostart si lo activas).

## Limitaciones conocidas

- Si tu lector arranca el código con un espacio Y el `WH_KEYBOARD_LL` se invoca antes que el `WM_INPUT` correspondiente, ese primer espacio escapa al destino (~5% de los casos según la implementación de Windows). Si lo ves frecuentemente, edita `SpaceSuppressor.ScannerWindowMs` (de 100 a 200).
- Si el operador teclea un espacio JUSTO después de escanear (<100 ms en la misma ventana objetivo), ese espacio se suprime falsamente. Físicamente improbable.
- No hay logging. Si necesitas diagnosticar, revisa el código de `SpaceSuppressor` y haz un build local con `Debug.WriteLine`.

## Desarrollo

- .NET 10. Tests cross-platform en `AutolumoBarcodeScannerTool.Core` (pure logic). Build del exe requiere Windows.
- `dotnet test tests/AutolumoBarcodeScannerTool.Tests/AutolumoBarcodeScannerTool.Tests.csproj` corre los tests.
- Para release: `git tag vX.Y.Z && git push --tags` dispara CI.
```

- [ ] **Step 3: Delete obsolete docs (if any)**

```bash
/usr/bin/find docs -name "installation-and-validation.md" -o -name "manual-test-checklist.md" 2>/dev/null
```

If any of those exist, prompt user before deleting:

```bash
git rm docs/installation-and-validation.md docs/manual-test-checklist.md  # only if they exist
```

- [ ] **Step 4: Commit**

```bash
git add README.md docs/
git commit -m "docs: README reflects v0.2.0 minimal space-blocker behavior"
```

---

## Task 16: Full build + manual smoke test instructions

- [ ] **Step 1: Clean build**

```bash
dotnet clean AutolumoBarcodeScannerTool.sln
dotnet restore AutolumoBarcodeScannerTool.sln
dotnet build src/AutolumoBarcodeScannerTool.Core/AutolumoBarcodeScannerTool.Core.csproj -c Release 2>&1 | tail -5
```

Expected: `0 errors, 0 warnings` on Core.

- [ ] **Step 2: Run all tests**

```bash
dotnet test tests/AutolumoBarcodeScannerTool.Tests/AutolumoBarcodeScannerTool.Tests.csproj -c Release 2>&1 | tail -10
```

Expected: 18 passed, 0 failed.

- [ ] **Step 3: Build the WinForms exe (Windows-only — skip on macOS, runs on CI)**

If you have a Windows machine available:

```bash
dotnet publish src/AutolumoBarcodeScannerTool/AutolumoBarcodeScannerTool.csproj -c Release -r win-x64 -o publish/sc
```

Expected: produces a single `.exe` in `publish/sc/` ~50 MB.

- [ ] **Step 4: Manual smoke test (Windows only)**

1. Launch `publish/sc/AutolumoBarcodeScannerTool.exe`.
2. Tray icon should appear.
3. Right-click → "Configurar…". The form opens with the device ComboBox populated.
4. Pick your scanner. Save.
5. Close & relaunch the app.
6. Open Notepad. Type `hello world` manually — both words appear with the space between them (the space is NOT suppressed because no scanner activity).
7. Scan a barcode that contains a space, e.g. "PREFIX MIDDLE". Notepad shows `PREFIXMIDDLE` (space suppressed) followed by ENTER if the scanner sends CR/LF.
8. Open another app (e.g. Calculator). Scan again — the space IS preserved this time (target window check fails) — IF you set a TargetProcessName.

- [ ] **Step 5: No commit (build artifacts only)**

---

## Task 17: Tag and release v0.2.0

- [ ] **Step 1: Verify the commit log looks clean**

```bash
git log --oneline -20
```

Expected: a series of small commits from this plan (one per task).

- [ ] **Step 2: Create annotated tag**

```bash
git tag -a v0.2.0 -m "v0.2.0 — minimal space-blocker

Breaking change vs v0.1.x: no more pipeline (transform/sink/SendInput).
The LL hook directly suppresses VK_SPACE when the configured scanner
just fired AND the configured target window is in foreground.

Removes: Core/Models, Core/Transforms, Core/Orchestration, Core/Sinks,
Core/Sources/Serial, SendInputInjector, ForegroundProcessSink,
InjectionMarker, HidKeyboardInputSource, IniConfigWriter, IOptionsMonitor,
Hosting, Serilog, 30 tests."
```

- [ ] **Step 3: Push to remote (ask user for explicit confirmation)**

Show the user:
- `git log` of new commits
- `git tag v0.2.0` exists locally

Then ask: "Push commits + tag to origin? (This triggers the release workflow and publishes a GitHub release.)"

On OK:

```bash
git push origin main
git push origin v0.2.0
```

- [ ] **Step 4: Wait for CI**

Check `https://github.com/<user>/<repo>/actions`. The release workflow should succeed and create a release with one zip.

---

## Behavior Summary (post-refactor)

| Scenario | Result |
|---|---|
| Scanner sends `"PREFIX MIDDLE\r"` in target window | Notepad receives `PREFIXMIDDLE\r` (space suppressed) |
| Scanner sends a code starting with space `" CODE\r"` in target window | First space MAY escape (~5%, LL-before-WM_INPUT race); rest works correctly |
| Human types `"hello world"` in target window | Notepad receives `"hello world"` (no scanner activity → no suppression) |
| Scanner fires while target window NOT in foreground | All keys pass through (target check fails) |
| Human types in non-target window | All keys pass through (vk != SPACE OR target fails) |
| Both filters empty (no target configured) | Suppression applies in any foreground window — useful for single-purpose machines |

## Spec Coverage Check

- [x] User typing spaces in human keyboard preserved ⇒ Task 3 (SpaceSuppressorTests `Pass_When_ScannerActivityIsOld`)
- [x] Scanner spaces suppressed in any position of barcode ⇒ Task 8 (`ScannerKeyTracker` stamps every scanner keydown)
- [x] Window-name filter required ⇒ Task 3 (`Pass_When_ProcessNameDoesNotMatch`, `Pass_When_TitleDoesNotContainNeedle`)
- [x] Visual device selection ⇒ Task 10 (`SettingsForm` ComboBox populated from `HidDeviceEnumerator`)
- [x] Autostart configurable ⇒ Task 10 (checkbox bound to `AutostartManager`)
- [x] Single-instance guard ⇒ Task 11 (`Mutex Local\AutolumoBarcodeScannerTool_v1`)
- [x] Removal of v0.1.5 features (transform/sink/Serial/diagnostics) ⇒ Task 12 (`git rm`)
- [x] Single self-contained artifact via CI ⇒ Task 14
- [x] Tests survive (cross-platform) ⇒ Task 3+4 (SpaceSuppressor + ConfigIo, 18 tests, net10.0)
