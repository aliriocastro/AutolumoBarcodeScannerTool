# AutolumoBarcodeScannerTool Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Construir una app Windows en .NET 8 que intercepte el input de un lector de código de barras (modo Serial o HID-keyboard, configurable), reemplace el terminador `CRLF` por `TAB` (configurable), e inyecte el resultado solo en una app destino filtrada por proceso. App de bandeja con autoarranque al login.

**Architecture:** Solución de 3 proyectos. `Core` (net8.0, cross-platform) contiene modelos, transforms, interfaces, SerialInputSource e IniConfigWriter — todo testeable sin Windows. `App` (net8.0-windows, WinExe) contiene WinForms tray UI, Win32 P/Invokes (RawInput, LL Hook, SendInput), registro HKCU\\Run, y Program.cs. `Tests` (net8.0) referencia solo Core y usa fakes (FakeSerialPort, FakeForegroundWindowProvider, FakeInputInjector) para cubrir lógica.

**Tech Stack:** .NET 8 LTS · WinForms (NotifyIcon) · Microsoft.Extensions.Hosting · Microsoft.Extensions.Configuration.Ini · Serilog · System.IO.Ports · xUnit + Shouldly · Inno Setup 6.

**Plataforma de desarrollo:** El usuario desarrolla en macOS. Las tareas marcadas **🪟 Solo Windows** requieren build/test en una máquina Windows real (VM o física). Las tareas sin marcador corren en macOS via `dotnet`.

**Spec de referencia:** [docs/superpowers/specs/2026-05-18-autolumo-barcode-scanner-tool-design.md](../specs/2026-05-18-autolumo-barcode-scanner-tool-design.md)

**Working directory:** `/Users/aliriocastro/Labotech/AutobioKeylogger`

---

## Estructura final de archivos

```
src/
  AutolumoBarcodeScannerTool.Core/        (net8.0, cross-platform)
    AutolumoBarcodeScannerTool.Core.csproj
    Models/
      ScanEvent.cs
      Enums.cs                            (SourceType, ScanTerminator, OutputMode, SourceConnectionState)
      ScannerOptions.cs
    Transforms/
      ITerminatorTransform.cs
      ReplaceTerminatorTransform.cs
    Sources/
      IInputSource.cs
      ISerialPortAdapter.cs
      Serial/
        SerialInputSource.cs
        SystemSerialPortAdapter.cs        (wrapper sobre System.IO.Ports.SerialPort)
    Sinks/
      IInputSink.cs
      IForegroundWindowProvider.cs
      IInputInjector.cs
      ForegroundProcessSink.cs
    Configuration/
      IniConfigWriter.cs
    Orchestration/
      ScannerOrchestrator.cs
  AutolumoBarcodeScannerTool/             (net8.0-windows, WinExe)
    AutolumoBarcodeScannerTool.csproj
    Program.cs
    appsettings.default.ini
    Hid/
      HidKeyboardInputSource.cs
      RawInputInterop.cs
      LowLevelKeyboardHook.cs
      HidDeviceEnumerator.cs
    Win32/
      Win32ForegroundWindowProvider.cs
      SendInputInjector.cs
    Autostart/
      AutostartManager.cs
    Tray/
      TrayIconController.cs
      SettingsForm.cs
      SettingsForm.Designer.cs
      Resources/
        tray-active.ico
        tray-paused.ico
        tray-error.ico
tests/
  AutolumoBarcodeScannerTool.Tests/       (net8.0)
    AutolumoBarcodeScannerTool.Tests.csproj
    Models/
      ScanEventTests.cs
    Transforms/
      ReplaceTerminatorTransformTests.cs
    Sources/
      Fakes/FakeSerialPortAdapter.cs
      SerialInputSourceTests.cs
    Sinks/
      Fakes/FakeForegroundWindowProvider.cs
      Fakes/FakeInputInjector.cs
      ForegroundProcessSinkTests.cs
    Configuration/
      IniConfigWriterTests.cs
    Orchestration/
      Fakes/FakeInputSource.cs
      ScannerOrchestratorTests.cs
installer/
  AutolumoBarcodeScannerTool.iss          (Inno Setup script)
docs/
  manual-test-checklist.md
AutolumoBarcodeScannerTool.sln
README.md
.gitignore
Directory.Build.props
```

---

## Fase 0 — Scaffolding del proyecto

### Task 1: Crear `.gitignore` y `Directory.Build.props`

**Files:**
- Create: `.gitignore`
- Create: `Directory.Build.props`

- [ ] **Step 1: Crear `.gitignore`**

Crear archivo `.gitignore` con:

```gitignore
bin/
obj/
.vs/
.idea/
*.user
*.suo
TestResults/
publish/
artifacts/
.DS_Store
Thumbs.db
```

- [ ] **Step 2: Crear `Directory.Build.props` con metadata compartida**

Crear archivo `Directory.Build.props` en la raíz:

```xml
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsNotAsErrors>CS1591</WarningsNotAsErrors>
    <Company>Labotech</Company>
    <Product>Autolumo Barcode Scanner Tool</Product>
    <Copyright>Copyright © Labotech 2026</Copyright>
    <Version>0.1.0</Version>
    <Authors>Labotech</Authors>
    <Description>Bridge para integración de lectores de código de barras con software contable Labotech.</Description>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Commit**

```bash
git add .gitignore Directory.Build.props
git commit -m "chore: scaffolding inicial (.gitignore + Directory.Build.props)"
```

---

### Task 2: Crear solución y proyecto Core

**Files:**
- Create: `AutolumoBarcodeScannerTool.sln`
- Create: `src/AutolumoBarcodeScannerTool.Core/AutolumoBarcodeScannerTool.Core.csproj`

- [ ] **Step 1: Crear solución vacía**

Run:
```bash
dotnet new sln -n AutolumoBarcodeScannerTool
```

Expected: `The template "Solution File" was created successfully.`

- [ ] **Step 2: Crear proyecto Core (classlib net8.0)**

Run:
```bash
dotnet new classlib -n AutolumoBarcodeScannerTool.Core -o src/AutolumoBarcodeScannerTool.Core -f net8.0
rm src/AutolumoBarcodeScannerTool.Core/Class1.cs
```

Expected: directorio `src/AutolumoBarcodeScannerTool.Core/` con `.csproj` y sin `Class1.cs`.

- [ ] **Step 3: Añadir el proyecto a la solución**

Run:
```bash
dotnet sln add src/AutolumoBarcodeScannerTool.Core/AutolumoBarcodeScannerTool.Core.csproj
```

Expected: `Project ... added to the solution.`

- [ ] **Step 4: Verificar que compila**

Run:
```bash
dotnet build src/AutolumoBarcodeScannerTool.Core/AutolumoBarcodeScannerTool.Core.csproj
```

Expected: `Build succeeded.` con 0 warnings y 0 errors.

- [ ] **Step 5: Commit**

```bash
git add AutolumoBarcodeScannerTool.sln src/AutolumoBarcodeScannerTool.Core
git commit -m "chore(core): scaffolding del proyecto Core (net8.0)"
```

---

### Task 3: Crear proyecto de tests

**Files:**
- Create: `tests/AutolumoBarcodeScannerTool.Tests/AutolumoBarcodeScannerTool.Tests.csproj`

- [ ] **Step 1: Crear proyecto xUnit**

Run:
```bash
dotnet new xunit -n AutolumoBarcodeScannerTool.Tests -o tests/AutolumoBarcodeScannerTool.Tests -f net8.0
rm tests/AutolumoBarcodeScannerTool.Tests/UnitTest1.cs
```

Expected: directorio creado con `.csproj` y sin UnitTest1.cs.

- [ ] **Step 2: Añadir referencia a Core**

Run:
```bash
dotnet add tests/AutolumoBarcodeScannerTool.Tests/AutolumoBarcodeScannerTool.Tests.csproj reference src/AutolumoBarcodeScannerTool.Core/AutolumoBarcodeScannerTool.Core.csproj
```

Expected: `Reference ... added.`

- [ ] **Step 3: Añadir Shouldly (asserts más legibles, MIT)**

Run:
```bash
dotnet add tests/AutolumoBarcodeScannerTool.Tests/AutolumoBarcodeScannerTool.Tests.csproj package Shouldly --version 4.2.1
```

Expected: package added.

- [ ] **Step 4: Añadir proyecto a la solución**

Run:
```bash
dotnet sln add tests/AutolumoBarcodeScannerTool.Tests/AutolumoBarcodeScannerTool.Tests.csproj
```

- [ ] **Step 5: Verificar que test runner funciona**

Run:
```bash
dotnet test tests/AutolumoBarcodeScannerTool.Tests/AutolumoBarcodeScannerTool.Tests.csproj
```

Expected: `Passed!  - Failed: 0, Passed: 0, Skipped: 0, Total: 0` (sin tests aún, pero el runner corre).

- [ ] **Step 6: Commit**

```bash
git add tests/ AutolumoBarcodeScannerTool.sln
git commit -m "chore(tests): scaffolding del proyecto de tests (xUnit + Shouldly)"
```

---

### Task 4: Crear proyecto principal (App) — 🪟 Solo Windows para build, pero csproj se crea aquí

**Files:**
- Create: `src/AutolumoBarcodeScannerTool/AutolumoBarcodeScannerTool.csproj`
- Create: `src/AutolumoBarcodeScannerTool/Program.cs` (placeholder)

- [ ] **Step 1: Crear estructura de carpeta y .csproj manualmente**

Crear archivo `src/AutolumoBarcodeScannerTool/AutolumoBarcodeScannerTool.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <AssemblyName>AutolumoBarcodeScannerTool</AssemblyName>
    <RootNamespace>AutolumoBarcodeScannerTool</RootNamespace>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\AutolumoBarcodeScannerTool.Core\AutolumoBarcodeScannerTool.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Update="appsettings.default.ini">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Crear `app.manifest` mínimo con requestedExecutionLevel asInvoker**

Crear `src/AutolumoBarcodeScannerTool/app.manifest`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="0.1.0.0" name="AutolumoBarcodeScannerTool"/>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
    <security>
      <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
        <requestedExecutionLevel level="asInvoker" uiAccess="false" />
      </requestedPrivileges>
    </security>
  </trustInfo>
  <compatibility xmlns="urn:schemas-microsoft-com:compat.v1">
    <application>
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"/>
    </application>
  </compatibility>
</assembly>
```

- [ ] **Step 3: Crear `Program.cs` placeholder**

Crear `src/AutolumoBarcodeScannerTool/Program.cs`:

```csharp
namespace AutolumoBarcodeScannerTool;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Console.WriteLine("AutolumoBarcodeScannerTool placeholder");
    }
}
```

- [ ] **Step 4: Añadir a la solución**

Run:
```bash
dotnet sln add src/AutolumoBarcodeScannerTool/AutolumoBarcodeScannerTool.csproj
```

- [ ] **Step 5: Commit (sin build — requiere Windows)**

```bash
git add src/AutolumoBarcodeScannerTool AutolumoBarcodeScannerTool.sln
git commit -m "chore(app): scaffolding del proyecto principal WinForms (net8.0-windows)"
```

> **Nota:** este proyecto solo compila en Windows. En macOS verás warning `NETSDK1100` al hacer `dotnet build` de la solución completa. Ignóralo en este entorno; valida builds en una máquina Windows al final.

---

## Fase 1 — Modelos y enums (Core, TDD)

### Task 5: Definir enums

**Files:**
- Create: `src/AutolumoBarcodeScannerTool.Core/Models/Enums.cs`

- [ ] **Step 1: Crear archivo de enums**

```csharp
namespace AutolumoBarcodeScannerTool.Core.Models;

public enum SourceType
{
    Serial,
    HidKeyboard
}

public enum ScanTerminator
{
    Cr,
    Lf,
    CrLf,
    Any
}

public enum OutputMode
{
    Tab,
    TabEnter,
    TabOnly,
    Custom
}

public enum SourceConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

public enum SerialEncoding
{
    Ascii,
    Utf8,
    Latin1
}
```

- [ ] **Step 2: Verificar build**

Run:
```bash
dotnet build src/AutolumoBarcodeScannerTool.Core
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/AutolumoBarcodeScannerTool.Core/Models/Enums.cs
git commit -m "feat(core): enums (SourceType, ScanTerminator, OutputMode, SourceConnectionState, SerialEncoding)"
```

---

### Task 6: Definir `ScanEvent` record

**Files:**
- Create: `src/AutolumoBarcodeScannerTool.Core/Models/ScanEvent.cs`
- Create: `tests/AutolumoBarcodeScannerTool.Tests/Models/ScanEventTests.cs`

- [ ] **Step 1: Escribir test fallido**

Crear `tests/AutolumoBarcodeScannerTool.Tests/Models/ScanEventTests.cs`:

```csharp
using AutolumoBarcodeScannerTool.Core.Models;
using Shouldly;
using Xunit;

namespace AutolumoBarcodeScannerTool.Tests.Models;

public class ScanEventTests
{
    [Fact]
    public void Construct_PreservesAllFields()
    {
        var ts = new DateTimeOffset(2026, 5, 18, 10, 30, 0, TimeSpan.Zero);
        var ev = new ScanEvent("ABC123", ScanTerminator.CrLf, ts);

        ev.Payload.ShouldBe("ABC123");
        ev.DetectedTerminator.ShouldBe(ScanTerminator.CrLf);
        ev.Timestamp.ShouldBe(ts);
    }

    [Fact]
    public void Records_WithPayload_ReplacesPayloadKeepsRest()
    {
        var ts = DateTimeOffset.UtcNow;
        var original = new ScanEvent("ABC", ScanTerminator.CrLf, ts);

        var modified = original with { Payload = "ABC\t" };

        modified.Payload.ShouldBe("ABC\t");
        modified.DetectedTerminator.ShouldBe(ScanTerminator.CrLf);
        modified.Timestamp.ShouldBe(ts);
    }
}
```

- [ ] **Step 2: Verificar que falla**

Run:
```bash
dotnet test --filter "FullyQualifiedName~ScanEventTests"
```

Expected: FAIL — `The type or namespace name 'ScanEvent' could not be found`.

- [ ] **Step 3: Implementar `ScanEvent`**

Crear `src/AutolumoBarcodeScannerTool.Core/Models/ScanEvent.cs`:

```csharp
namespace AutolumoBarcodeScannerTool.Core.Models;

public sealed record ScanEvent(
    string Payload,
    ScanTerminator DetectedTerminator,
    DateTimeOffset Timestamp);
```

- [ ] **Step 4: Verificar que pasa**

Run:
```bash
dotnet test --filter "FullyQualifiedName~ScanEventTests"
```

Expected: `Passed: 2, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add src/AutolumoBarcodeScannerTool.Core/Models/ScanEvent.cs tests/AutolumoBarcodeScannerTool.Tests/Models/ScanEventTests.cs
git commit -m "feat(core): ScanEvent record"
```

---

### Task 7: Definir `ScannerOptions` (jerarquía completa)

**Files:**
- Create: `src/AutolumoBarcodeScannerTool.Core/Models/ScannerOptions.cs`

- [ ] **Step 1: Crear `ScannerOptions.cs` con todas las clases anidadas**

```csharp
namespace AutolumoBarcodeScannerTool.Core.Models;

public sealed class ScannerOptions
{
    public const string SectionName = "Scanner";

    public bool Enabled { get; set; } = true;
    public SourceType SourceType { get; set; } = SourceType.Serial;
    public ScanTerminator Terminator { get; set; } = ScanTerminator.CrLf;
    public SerialOptions Serial { get; set; } = new();
    public HidKeyboardOptions HidKeyboard { get; set; } = new();
    public TargetOptions Target { get; set; } = new();
    public OutputOptions Output { get; set; } = new();
}

public sealed class SerialOptions
{
    public string PortName { get; set; } = "COM3";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public SerialEncoding Encoding { get; set; } = SerialEncoding.Ascii;
}

public sealed class HidKeyboardOptions
{
    public string VendorId { get; set; } = "0x0000";
    public string ProductId { get; set; } = "0x0000";
}

public sealed class TargetOptions
{
    public string ProcessName { get; set; } = "";
    public string? WindowTitleContains { get; set; }
}

public sealed class OutputOptions
{
    public OutputMode OnTerminator { get; set; } = OutputMode.Tab;
    public string OutputSuffix { get; set; } = "{TAB}";
}

public sealed class AutostartOptions
{
    public const string SectionName = "Autostart";
    public bool Enabled { get; set; } = true;
}

public sealed class LoggingOptions
{
    public const string SectionName = "Logging";
    public string MinimumLevel { get; set; } = "Information";
    public bool LogPayload { get; set; } = true;
}
```

- [ ] **Step 2: Verificar build**

Run:
```bash
dotnet build src/AutolumoBarcodeScannerTool.Core
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/AutolumoBarcodeScannerTool.Core/Models/ScannerOptions.cs
git commit -m "feat(core): ScannerOptions y POCOs anidadas"
```

---

## Fase 2 — Transform pipeline (TDD)

### Task 8: `ITerminatorTransform` interface

**Files:**
- Create: `src/AutolumoBarcodeScannerTool.Core/Transforms/ITerminatorTransform.cs`

- [ ] **Step 1: Crear interface**

```csharp
using AutolumoBarcodeScannerTool.Core.Models;

namespace AutolumoBarcodeScannerTool.Core.Transforms;

public interface ITerminatorTransform
{
    ScanEvent Apply(ScanEvent input);
}
```

- [ ] **Step 2: Commit**

```bash
git add src/AutolumoBarcodeScannerTool.Core/Transforms/ITerminatorTransform.cs
git commit -m "feat(core): ITerminatorTransform interface"
```

---

### Task 9: `ReplaceTerminatorTransform` — modo Tab (TDD)

**Files:**
- Create: `tests/AutolumoBarcodeScannerTool.Tests/Transforms/ReplaceTerminatorTransformTests.cs`
- Create: `src/AutolumoBarcodeScannerTool.Core/Transforms/ReplaceTerminatorTransform.cs`

- [ ] **Step 1: Escribir test del modo Tab**

```csharp
using AutolumoBarcodeScannerTool.Core.Models;
using AutolumoBarcodeScannerTool.Core.Transforms;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace AutolumoBarcodeScannerTool.Tests.Transforms;

public class ReplaceTerminatorTransformTests
{
    private static ReplaceTerminatorTransform CreateSut(OutputOptions opts) =>
        new(Options.Create(opts));

    private static ScanEvent SampleEvent(string payload = "ABC123") =>
        new(payload, ScanTerminator.CrLf, DateTimeOffset.UnixEpoch);

    [Fact]
    public void Apply_TabMode_AppendsTabCharacter()
    {
        var sut = CreateSut(new OutputOptions { OnTerminator = OutputMode.Tab });
        var result = sut.Apply(SampleEvent());

        result.Payload.ShouldBe("ABC123\t");
    }
}
```

- [ ] **Step 2: Añadir referencia a Microsoft.Extensions.Options.ConfigurationExtensions en tests**

Run:
```bash
dotnet add tests/AutolumoBarcodeScannerTool.Tests package Microsoft.Extensions.Options --version 8.0.2
```

- [ ] **Step 3: Verificar que falla por tipo no existente**

Run:
```bash
dotnet test --filter "FullyQualifiedName~ReplaceTerminatorTransformTests"
```

Expected: FAIL — `ReplaceTerminatorTransform could not be found`.

- [ ] **Step 4: Añadir `Microsoft.Extensions.Options` y DI.Abstractions al Core**

Run:
```bash
dotnet add src/AutolumoBarcodeScannerTool.Core package Microsoft.Extensions.Options --version 8.0.2
dotnet add src/AutolumoBarcodeScannerTool.Core package Microsoft.Extensions.DependencyInjection.Abstractions --version 8.0.2
```

> El segundo paquete trae `[ActivatorUtilitiesConstructor]` que nos permite indicarle a MS DI cuál constructor preferir cuando hay varios.

- [ ] **Step 5: Implementar `ReplaceTerminatorTransform` (mínimo para que pase el test Tab)**

Crear `src/AutolumoBarcodeScannerTool.Core/Transforms/ReplaceTerminatorTransform.cs`:

```csharp
using AutolumoBarcodeScannerTool.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AutolumoBarcodeScannerTool.Core.Transforms;

public sealed class ReplaceTerminatorTransform : ITerminatorTransform
{
    private readonly IOptionsMonitor<OutputOptions> _opts;

    // Test-friendly constructor (IOptions). Marcado como NO el preferido por DI.
    public ReplaceTerminatorTransform(IOptions<OutputOptions> opts)
        : this(new StaticOptionsMonitor<OutputOptions>(opts.Value)) { }

    // Constructor preferido por DI en producción (hot-reload).
    [ActivatorUtilitiesConstructor]
    public ReplaceTerminatorTransform(IOptionsMonitor<OutputOptions> opts)
    {
        _opts = opts;
    }

    public ScanEvent Apply(ScanEvent input) =>
        input with { Payload = input.Payload + ResolveSuffix(_opts.CurrentValue) };

    private static string ResolveSuffix(OutputOptions opts) => opts.OnTerminator switch
    {
        OutputMode.Tab => "\t",
        OutputMode.TabEnter => "\t\n",
        OutputMode.TabOnly => string.Empty,
        OutputMode.Custom => (opts.OutputSuffix ?? string.Empty)
            .Replace("{TAB}", "\t", StringComparison.Ordinal)
            .Replace("{ENTER}", "\n", StringComparison.Ordinal),
        _ => "\t"
    };

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public StaticOptionsMonitor(T value) { CurrentValue = value; }
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
```

- [ ] **Step 6: Verificar que pasa**

Run:
```bash
dotnet test --filter "FullyQualifiedName~ReplaceTerminatorTransformTests"
```

Expected: `Passed: 1, Failed: 0`.

- [ ] **Step 7: Commit**

```bash
git add src/AutolumoBarcodeScannerTool.Core/Transforms/ReplaceTerminatorTransform.cs tests/AutolumoBarcodeScannerTool.Tests/Transforms/
git commit -m "feat(transforms): ReplaceTerminatorTransform modo Tab"
```

---

### Task 10: Cubrir modos TabEnter, TabOnly y Custom (TDD)

**Files:**
- Modify: `tests/AutolumoBarcodeScannerTool.Tests/Transforms/ReplaceTerminatorTransformTests.cs`

- [ ] **Step 1: Añadir tests para los 3 modos restantes**

Añadir al final de la clase `ReplaceTerminatorTransformTests`:

```csharp
    [Fact]
    public void Apply_TabEnterMode_AppendsTabAndNewline()
    {
        var sut = CreateSut(new OutputOptions { OnTerminator = OutputMode.TabEnter });
        var result = sut.Apply(SampleEvent());

        result.Payload.ShouldBe("ABC123\t\n");
    }

    [Fact]
    public void Apply_TabOnlyMode_AppendsNothing()
    {
        var sut = CreateSut(new OutputOptions { OnTerminator = OutputMode.TabOnly });
        var result = sut.Apply(SampleEvent());

        result.Payload.ShouldBe("ABC123");
    }

    [Fact]
    public void Apply_CustomMode_ResolvesTabAndEnterTokens()
    {
        var sut = CreateSut(new OutputOptions
        {
            OnTerminator = OutputMode.Custom,
            OutputSuffix = "{TAB}suffix{ENTER}"
        });
        var result = sut.Apply(SampleEvent());

        result.Payload.ShouldBe("ABC123\tsuffix\n");
    }

    [Fact]
    public void Apply_CustomMode_LiteralStringWithoutTokens()
    {
        var sut = CreateSut(new OutputOptions
        {
            OnTerminator = OutputMode.Custom,
            OutputSuffix = "|END"
        });
        var result = sut.Apply(SampleEvent());

        result.Payload.ShouldBe("ABC123|END");
    }

    [Fact]
    public void Apply_PreservesTerminatorAndTimestamp()
    {
        var ts = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var input = new ScanEvent("X", ScanTerminator.Lf, ts);
        var sut = CreateSut(new OutputOptions { OnTerminator = OutputMode.Tab });

        var result = sut.Apply(input);

        result.DetectedTerminator.ShouldBe(ScanTerminator.Lf);
        result.Timestamp.ShouldBe(ts);
    }
```

- [ ] **Step 2: Verificar que todos pasan**

Run:
```bash
dotnet test --filter "FullyQualifiedName~ReplaceTerminatorTransformTests"
```

Expected: `Passed: 5, Failed: 0`.

- [ ] **Step 3: Commit**

```bash
git add tests/AutolumoBarcodeScannerTool.Tests/Transforms/
git commit -m "test(transforms): cubre modos TabEnter, TabOnly, Custom y preservación de campos"
```

---

## Fase 3 — Configuración

### Task 11: Plantilla `appsettings.default.ini`

**Files:**
- Create: `src/AutolumoBarcodeScannerTool/appsettings.default.ini`

- [ ] **Step 1: Crear archivo con valores por defecto**

```ini
; AutolumoBarcodeScannerTool - configuración por defecto
; Ubicación en runtime: %LOCALAPPDATA%\AutolumoBarcodeScannerTool\appsettings.ini

[Scanner]
Enabled=true
SourceType=Serial
Terminator=CrLf

[Scanner:Serial]
PortName=COM3
BaudRate=9600
DataBits=8
Parity=None
StopBits=One
Encoding=Ascii

[Scanner:HidKeyboard]
VendorId=0x0000
ProductId=0x0000

[Scanner:Target]
ProcessName=
WindowTitleContains=

[Scanner:Output]
OnTerminator=Tab
OutputSuffix={TAB}

[Autostart]
Enabled=true

[Logging]
MinimumLevel=Information
LogPayload=true
```

- [ ] **Step 2: Commit**

```bash
git add src/AutolumoBarcodeScannerTool/appsettings.default.ini
git commit -m "chore(config): plantilla appsettings.default.ini"
```

---

### Task 12: `IniConfigWriter` — parseo (TDD)

**Files:**
- Create: `tests/AutolumoBarcodeScannerTool.Tests/Configuration/IniConfigWriterTests.cs`
- Create: `src/AutolumoBarcodeScannerTool.Core/Configuration/IniConfigWriter.cs`

- [ ] **Step 1: Escribir tests de parseo y escritura preservando comentarios**

```csharp
using AutolumoBarcodeScannerTool.Core.Configuration;
using Shouldly;
using Xunit;

namespace AutolumoBarcodeScannerTool.Tests.Configuration;

public class IniConfigWriterTests
{
    [Fact]
    public void Set_ExistingKey_ReplacesValueInSamePosition()
    {
        var original = """
            ; comment
            [Section]
            Key=oldValue
            Other=foo
            """;

        var result = IniConfigWriter.Update(original, "Section:Key", "newValue");

        result.ShouldBe("""
            ; comment
            [Section]
            Key=newValue
            Other=foo
            """);
    }

    [Fact]
    public void Set_NewKeyInExistingSection_AppendsToSection()
    {
        var original = """
            [Section]
            Existing=1
            """;

        var result = IniConfigWriter.Update(original, "Section:NewKey", "value");

        result.ShouldBe("""
            [Section]
            Existing=1
            NewKey=value
            """);
    }

    [Fact]
    public void Set_NewSection_AppendsAtEnd()
    {
        var original = """
            [Existing]
            A=1
            """;

        var result = IniConfigWriter.Update(original, "NewSection:Key", "value");

        result.ShouldBe("""
            [Existing]
            A=1

            [NewSection]
            Key=value
            """);
    }

    [Fact]
    public void Set_PreservesCommentsAboveTargetLine()
    {
        var original = """
            [Section]
            ; documenta Key
            Key=old
            """;

        var result = IniConfigWriter.Update(original, "Section:Key", "new");

        result.ShouldBe("""
            [Section]
            ; documenta Key
            Key=new
            """);
    }

    [Fact]
    public void Set_HierarchicalSectionWithColon_Works()
    {
        var original = """
            [Scanner:Serial]
            PortName=COM3
            """;

        var result = IniConfigWriter.Update(original, "Scanner:Serial:PortName", "COM5");

        result.ShouldBe("""
            [Scanner:Serial]
            PortName=COM5
            """);
    }

    [Fact]
    public void Set_EmptyValue_WritesEmptyValue()
    {
        var original = """
            [Section]
            Key=oldValue
            """;

        var result = IniConfigWriter.Update(original, "Section:Key", "");

        result.ShouldBe("""
            [Section]
            Key=
            """);
    }
}
```

- [ ] **Step 2: Verificar fallo**

Run:
```bash
dotnet test --filter "FullyQualifiedName~IniConfigWriterTests"
```

Expected: FAIL — `IniConfigWriter could not be found`.

- [ ] **Step 3: Implementar `IniConfigWriter.Update`**

```csharp
namespace AutolumoBarcodeScannerTool.Core.Configuration;

public static class IniConfigWriter
{
    public static string Update(string original, string fullKey, string value)
    {
        var (sectionPath, keyName) = SplitKey(fullKey);
        var lines = original.ReplaceLineEndings("\n").Split('\n').ToList();

        var sectionRange = FindSectionRange(lines, sectionPath);

        if (sectionRange is null)
        {
            // Section doesn't exist: append a blank line, the header, and the key.
            if (lines.Count > 0 && !string.IsNullOrEmpty(lines[^1]))
                lines.Add(string.Empty);
            lines.Add($"[{sectionPath}]");
            lines.Add($"{keyName}={value}");
            return string.Join("\n", lines);
        }

        var (start, end) = sectionRange.Value;
        for (var i = start + 1; i <= end; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith(';') || trimmed.StartsWith('#') || string.IsNullOrWhiteSpace(trimmed))
                continue;

            var eq = lines[i].IndexOf('=');
            if (eq < 0) continue;

            var currentKey = lines[i][..eq].Trim();
            if (string.Equals(currentKey, keyName, StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = $"{keyName}={value}";
                return string.Join("\n", lines);
            }
        }

        // Key not found inside the section: append at end of section.
        lines.Insert(end + 1, $"{keyName}={value}");
        return string.Join("\n", lines);
    }

    private static (string SectionPath, string KeyName) SplitKey(string fullKey)
    {
        var lastColon = fullKey.LastIndexOf(':');
        if (lastColon < 0)
            throw new ArgumentException($"Key must include section: '{fullKey}'", nameof(fullKey));
        return (fullKey[..lastColon], fullKey[(lastColon + 1)..]);
    }

    private static (int Start, int End)? FindSectionRange(List<string> lines, string sectionPath)
    {
        var header = $"[{sectionPath}]";
        var start = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (string.Equals(lines[i].Trim(), header, StringComparison.OrdinalIgnoreCase))
            {
                start = i;
                break;
            }
        }
        if (start < 0) return null;

        var end = lines.Count - 1;
        for (var j = start + 1; j < lines.Count; j++)
        {
            var t = lines[j].TrimStart();
            if (t.StartsWith('[') && t.EndsWith(']'))
            {
                end = j - 1;
                break;
            }
        }
        return (start, end);
    }
}
```

- [ ] **Step 4: Verificar que todos los tests pasan**

Run:
```bash
dotnet test --filter "FullyQualifiedName~IniConfigWriterTests"
```

Expected: `Passed: 6, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add src/AutolumoBarcodeScannerTool.Core/Configuration/ tests/AutolumoBarcodeScannerTool.Tests/Configuration/
git commit -m "feat(config): IniConfigWriter (update + preserve comments)"
```

---

## Fase 4 — Sink con fakes (TDD)

### Task 13: Interfaces `IInputSink`, `IForegroundWindowProvider`, `IInputInjector`

**Files:**
- Create: `src/AutolumoBarcodeScannerTool.Core/Sinks/IInputSink.cs`
- Create: `src/AutolumoBarcodeScannerTool.Core/Sinks/IForegroundWindowProvider.cs`
- Create: `src/AutolumoBarcodeScannerTool.Core/Sinks/IInputInjector.cs`

- [ ] **Step 1: Crear interfaces**

`IInputSink.cs`:

```csharp
using AutolumoBarcodeScannerTool.Core.Models;

namespace AutolumoBarcodeScannerTool.Core.Sinks;

public interface IInputSink
{
    Task SendAsync(ScanEvent ev, CancellationToken ct);
}
```

`IForegroundWindowProvider.cs`:

```csharp
namespace AutolumoBarcodeScannerTool.Core.Sinks;

public interface IForegroundWindowProvider
{
    ForegroundWindowInfo? GetCurrent();
}

public sealed record ForegroundWindowInfo(string ProcessName, string WindowTitle);
```

`IInputInjector.cs`:

```csharp
namespace AutolumoBarcodeScannerTool.Core.Sinks;

public interface IInputInjector
{
    void SendText(string text);
}
```

- [ ] **Step 2: Commit**

```bash
git add src/AutolumoBarcodeScannerTool.Core/Sinks/
git commit -m "feat(sinks): interfaces IInputSink, IForegroundWindowProvider, IInputInjector"
```

---

### Task 14: Fakes para tests del sink

**Files:**
- Create: `tests/AutolumoBarcodeScannerTool.Tests/Sinks/Fakes/FakeForegroundWindowProvider.cs`
- Create: `tests/AutolumoBarcodeScannerTool.Tests/Sinks/Fakes/FakeInputInjector.cs`

- [ ] **Step 1: Crear fake del foreground window provider**

```csharp
using AutolumoBarcodeScannerTool.Core.Sinks;

namespace AutolumoBarcodeScannerTool.Tests.Sinks.Fakes;

public sealed class FakeForegroundWindowProvider : IForegroundWindowProvider
{
    public ForegroundWindowInfo? Current { get; set; }
    public ForegroundWindowInfo? GetCurrent() => Current;
}
```

- [ ] **Step 2: Crear fake del injector**

```csharp
using AutolumoBarcodeScannerTool.Core.Sinks;

namespace AutolumoBarcodeScannerTool.Tests.Sinks.Fakes;

public sealed class FakeInputInjector : IInputInjector
{
    public List<string> SentTexts { get; } = new();
    public TimeSpan SimulatedDelay { get; set; } = TimeSpan.Zero;

    public void SendText(string text)
    {
        if (SimulatedDelay > TimeSpan.Zero) Thread.Sleep(SimulatedDelay);
        SentTexts.Add(text);
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add tests/AutolumoBarcodeScannerTool.Tests/Sinks/Fakes/
git commit -m "test(sinks): fakes para foreground window provider e input injector"
```

---

### Task 15: `ForegroundProcessSink` — coincide por proceso (TDD)

**Files:**
- Create: `tests/AutolumoBarcodeScannerTool.Tests/Sinks/ForegroundProcessSinkTests.cs`
- Create: `src/AutolumoBarcodeScannerTool.Core/Sinks/ForegroundProcessSink.cs`

- [ ] **Step 1: Escribir tests de matching y descarte**

```csharp
using AutolumoBarcodeScannerTool.Core.Models;
using AutolumoBarcodeScannerTool.Core.Sinks;
using AutolumoBarcodeScannerTool.Tests.Sinks.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace AutolumoBarcodeScannerTool.Tests.Sinks;

public class ForegroundProcessSinkTests
{
    private static (ForegroundProcessSink Sut, FakeForegroundWindowProvider Window, FakeInputInjector Injector)
        Build(string targetProcess, string? titleContains = null)
    {
        var opts = Options.Create(new TargetOptions
        {
            ProcessName = targetProcess,
            WindowTitleContains = titleContains
        });
        var window = new FakeForegroundWindowProvider();
        var injector = new FakeInputInjector();
        var sut = new ForegroundProcessSink(opts, window, injector, NullLogger<ForegroundProcessSink>.Instance);
        return (sut, window, injector);
    }

    private static ScanEvent Event(string payload = "ABC\t") =>
        new(payload, ScanTerminator.CrLf, DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task Sends_WhenForegroundMatchesTargetProcess()
    {
        var (sut, window, injector) = Build("MiAppContable");
        window.Current = new ForegroundWindowInfo("MiAppContable", "Factura nueva");

        await sut.SendAsync(Event(), CancellationToken.None);

        injector.SentTexts.ShouldBe(new[] { "ABC\t" });
    }

    [Fact]
    public async Task Discards_WhenForegroundIsDifferentProcess()
    {
        var (sut, window, injector) = Build("MiAppContable");
        window.Current = new ForegroundWindowInfo("notepad", "Untitled");

        await sut.SendAsync(Event(), CancellationToken.None);

        injector.SentTexts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Discards_WhenNoForegroundWindow()
    {
        var (sut, _, injector) = Build("MiAppContable");

        await sut.SendAsync(Event(), CancellationToken.None);

        injector.SentTexts.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessNameMatch_IsCaseInsensitive()
    {
        var (sut, window, injector) = Build("MiAppContable");
        window.Current = new ForegroundWindowInfo("MIAPPCONTABLE", "x");

        await sut.SendAsync(Event(), CancellationToken.None);

        injector.SentTexts.ShouldBe(new[] { "ABC\t" });
    }

    [Fact]
    public async Task TitleFilter_RequiresContains()
    {
        var (sut, window, injector) = Build("MiAppContable", titleContains: "Factura");
        window.Current = new ForegroundWindowInfo("MiAppContable", "Reportes mensuales");

        await sut.SendAsync(Event(), CancellationToken.None);

        injector.SentTexts.ShouldBeEmpty();
    }

    [Fact]
    public async Task TitleFilter_MatchesSubstringCaseInsensitive()
    {
        var (sut, window, injector) = Build("MiAppContable", titleContains: "factura");
        window.Current = new ForegroundWindowInfo("MiAppContable", "Editar Factura 1234");

        await sut.SendAsync(Event(), CancellationToken.None);

        injector.SentTexts.ShouldBe(new[] { "ABC\t" });
    }

    [Fact]
    public async Task ConcurrentSend_SecondCallIsDiscarded()
    {
        var (sut, window, injector) = Build("MiAppContable");
        window.Current = new ForegroundWindowInfo("MiAppContable", "x");
        injector.SimulatedDelay = TimeSpan.FromMilliseconds(200);

        var first = sut.SendAsync(Event("FIRST\t"), CancellationToken.None);
        // Asegurar que el primero haya entrado al semaforo
        await Task.Delay(50);
        var second = sut.SendAsync(Event("SECOND\t"), CancellationToken.None);

        await Task.WhenAll(first, second);

        injector.SentTexts.ShouldBe(new[] { "FIRST\t" });
    }
}
```

- [ ] **Step 2: Verificar fallo**

Run:
```bash
dotnet test --filter "FullyQualifiedName~ForegroundProcessSinkTests"
```

Expected: FAIL — `ForegroundProcessSink could not be found`.

- [ ] **Step 3: Añadir Microsoft.Extensions.Logging.Abstractions al Core**

Run:
```bash
dotnet add src/AutolumoBarcodeScannerTool.Core package Microsoft.Extensions.Logging.Abstractions --version 8.0.2
dotnet add tests/AutolumoBarcodeScannerTool.Tests package Microsoft.Extensions.Logging.Abstractions --version 8.0.2
```

- [ ] **Step 4: Implementar `ForegroundProcessSink`**

```csharp
using AutolumoBarcodeScannerTool.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutolumoBarcodeScannerTool.Core.Sinks;

public sealed class ForegroundProcessSink : IInputSink
{
    private readonly IOptions<TargetOptions> _opts;
    private readonly IForegroundWindowProvider _window;
    private readonly IInputInjector _injector;
    private readonly ILogger<ForegroundProcessSink> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ForegroundProcessSink(
        IOptions<TargetOptions> opts,
        IForegroundWindowProvider window,
        IInputInjector injector,
        ILogger<ForegroundProcessSink> logger)
    {
        _opts = opts;
        _window = window;
        _injector = injector;
        _logger = logger;
    }

    public async Task SendAsync(ScanEvent ev, CancellationToken ct)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, ct).ConfigureAwait(false))
        {
            _logger.LogWarning("Escaneo descartado: sink ocupado con escaneo previo (payload len={Len})", ev.Payload.Length);
            return;
        }

        try
        {
            var target = _opts.Value;
            var current = _window.GetCurrent();

            if (current is null)
            {
                _logger.LogInformation("Escaneo descartado: no hay ventana en foreground");
                return;
            }

            if (!ProcessMatches(current.ProcessName, target.ProcessName))
            {
                _logger.LogInformation(
                    "Escaneo descartado: ventana activa '{Current}' no coincide con target '{Target}'",
                    current.ProcessName, target.ProcessName);
                return;
            }

            if (!string.IsNullOrEmpty(target.WindowTitleContains) &&
                current.WindowTitle.IndexOf(target.WindowTitleContains, StringComparison.OrdinalIgnoreCase) < 0)
            {
                _logger.LogInformation(
                    "Escaneo descartado: título '{Title}' no contiene '{Needle}'",
                    current.WindowTitle, target.WindowTitleContains);
                return;
            }

            _injector.SendText(ev.Payload);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static bool ProcessMatches(string current, string target)
    {
        if (string.IsNullOrEmpty(target)) return false;
        return string.Equals(current, target, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 5: Verificar tests pasan**

Run:
```bash
dotnet test --filter "FullyQualifiedName~ForegroundProcessSinkTests"
```

Expected: `Passed: 7, Failed: 0`.

- [ ] **Step 6: Commit**

```bash
git add src/AutolumoBarcodeScannerTool.Core/Sinks/ForegroundProcessSink.cs tests/AutolumoBarcodeScannerTool.Tests/Sinks/
git commit -m "feat(sinks): ForegroundProcessSink con matching de proceso, título y semáforo"
```

---

## Fase 5 — Source serial (TDD)

### Task 16: Interfaces `IInputSource` y `ISerialPortAdapter`

**Files:**
- Create: `src/AutolumoBarcodeScannerTool.Core/Sources/IInputSource.cs`
- Create: `src/AutolumoBarcodeScannerTool.Core/Sources/ISerialPortAdapter.cs`

- [ ] **Step 1: Crear `IInputSource`**

```csharp
using AutolumoBarcodeScannerTool.Core.Models;

namespace AutolumoBarcodeScannerTool.Core.Sources;

public interface IInputSource : IAsyncDisposable
{
    event Func<ScanEvent, Task>? OnScan;
    event EventHandler<SourceConnectionState>? StateChanged;
    SourceConnectionState State { get; }
    Task StartAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);
}
```

- [ ] **Step 2: Crear `ISerialPortAdapter`**

```csharp
using System.Text;

namespace AutolumoBarcodeScannerTool.Core.Sources;

public interface ISerialPortAdapter : IDisposable
{
    event EventHandler<ReadOnlyMemory<byte>>? DataReceived;
    bool IsOpen { get; }
    void Open();
    void Close();
}

public sealed record SerialPortConfig(
    string PortName,
    int BaudRate,
    int DataBits,
    string Parity,
    string StopBits,
    Encoding Encoding);
```

- [ ] **Step 3: Commit**

```bash
git add src/AutolumoBarcodeScannerTool.Core/Sources/IInputSource.cs src/AutolumoBarcodeScannerTool.Core/Sources/ISerialPortAdapter.cs
git commit -m "feat(sources): interfaces IInputSource e ISerialPortAdapter"
```

---

### Task 17: `FakeSerialPortAdapter` para tests

**Files:**
- Create: `tests/AutolumoBarcodeScannerTool.Tests/Sources/Fakes/FakeSerialPortAdapter.cs`

- [ ] **Step 1: Crear fake**

```csharp
using AutolumoBarcodeScannerTool.Core.Sources;

namespace AutolumoBarcodeScannerTool.Tests.Sources.Fakes;

public sealed class FakeSerialPortAdapter : ISerialPortAdapter
{
    public event EventHandler<ReadOnlyMemory<byte>>? DataReceived;
    public bool IsOpen { get; private set; }
    public bool OpenCalled { get; private set; }
    public bool CloseCalled { get; private set; }

    public void Open() { OpenCalled = true; IsOpen = true; }
    public void Close() { CloseCalled = true; IsOpen = false; }

    public void EmitBytes(byte[] data) => DataReceived?.Invoke(this, data);
    public void EmitText(string text, System.Text.Encoding encoding)
        => EmitBytes(encoding.GetBytes(text));

    public void Dispose() => Close();
}
```

- [ ] **Step 2: Commit**

```bash
git add tests/AutolumoBarcodeScannerTool.Tests/Sources/Fakes/
git commit -m "test(sources): FakeSerialPortAdapter"
```

---

### Task 18: `SerialInputSource` — detección de terminador CRLF (TDD)

**Files:**
- Create: `tests/AutolumoBarcodeScannerTool.Tests/Sources/SerialInputSourceTests.cs`
- Create: `src/AutolumoBarcodeScannerTool.Core/Sources/Serial/SerialInputSource.cs`

- [ ] **Step 1: Escribir test base de CRLF**

```csharp
using System.Text;
using AutolumoBarcodeScannerTool.Core.Models;
using AutolumoBarcodeScannerTool.Core.Sources;
using AutolumoBarcodeScannerTool.Core.Sources.Serial;
using AutolumoBarcodeScannerTool.Tests.Sources.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace AutolumoBarcodeScannerTool.Tests.Sources;

public class SerialInputSourceTests
{
    private static (SerialInputSource Sut, FakeSerialPortAdapter Port, List<ScanEvent> Events)
        Build(ScanTerminator terminator, Encoding? encoding = null)
    {
        var port = new FakeSerialPortAdapter();
        var sut = new SerialInputSource(port, terminator, encoding ?? Encoding.ASCII,
            NullLogger<SerialInputSource>.Instance);
        var events = new List<ScanEvent>();
        sut.OnScan += ev => { events.Add(ev); return Task.CompletedTask; };
        sut.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        return (sut, port, events);
    }

    [Fact]
    public void Emits_ScanEvent_OnCrLfTerminator()
    {
        var (_, port, events) = Build(ScanTerminator.CrLf);

        port.EmitText("ABC123\r\n", Encoding.ASCII);

        events.Count.ShouldBe(1);
        events[0].Payload.ShouldBe("ABC123");
        events[0].DetectedTerminator.ShouldBe(ScanTerminator.CrLf);
    }
}
```

- [ ] **Step 2: Verificar fallo**

Run:
```bash
dotnet test --filter "FullyQualifiedName~SerialInputSourceTests.Emits_ScanEvent_OnCrLfTerminator"
```

Expected: FAIL — `SerialInputSource could not be found`.

- [ ] **Step 3: Implementar mínimo para que pase**

Crear `src/AutolumoBarcodeScannerTool.Core/Sources/Serial/SerialInputSource.cs`:

```csharp
using System.Text;
using AutolumoBarcodeScannerTool.Core.Models;
using Microsoft.Extensions.Logging;

namespace AutolumoBarcodeScannerTool.Core.Sources.Serial;

public sealed class SerialInputSource : IInputSource
{
    private readonly ISerialPortAdapter _port;
    private readonly ScanTerminator _terminator;
    private readonly Encoding _encoding;
    private readonly ILogger<SerialInputSource> _logger;
    private readonly StringBuilder _buffer = new();
    private readonly object _bufferLock = new();
    private SourceConnectionState _state = SourceConnectionState.Disconnected;

    public event Func<ScanEvent, Task>? OnScan;
    public event EventHandler<SourceConnectionState>? StateChanged;

    public SerialInputSource(
        ISerialPortAdapter port,
        ScanTerminator terminator,
        Encoding encoding,
        ILogger<SerialInputSource> logger)
    {
        _port = port;
        _terminator = terminator;
        _encoding = encoding;
        _logger = logger;
        _port.DataReceived += OnDataReceived;
    }

    public SourceConnectionState State => _state;

    public Task StartAsync(CancellationToken ct)
    {
        try
        {
            _port.Open();
            SetState(SourceConnectionState.Connected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error abriendo puerto serial");
            SetState(SourceConnectionState.Error);
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _port.Close();
        SetState(SourceConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    private void OnDataReceived(object? sender, ReadOnlyMemory<byte> bytes)
    {
        var text = _encoding.GetString(bytes.Span);

        List<ScanEvent>? events = null;
        lock (_bufferLock)
        {
            _buffer.Append(text);
            events = TryExtractEvents();
        }

        if (events is null) return;
        foreach (var ev in events)
            OnScan?.Invoke(ev);
    }

    private List<ScanEvent>? TryExtractEvents()
    {
        List<ScanEvent>? results = null;
        while (true)
        {
            var current = _buffer.ToString();
            var (terminatorIndex, terminatorLength, detected) = FindTerminator(current);
            if (terminatorIndex < 0) break;

            var payload = current[..terminatorIndex];
            _buffer.Clear();
            _buffer.Append(current.AsSpan(terminatorIndex + terminatorLength));

            results ??= new List<ScanEvent>();
            results.Add(new ScanEvent(payload, detected, DateTimeOffset.UtcNow));
        }
        return results;
    }

    private (int Index, int Length, ScanTerminator Detected) FindTerminator(string s)
    {
        switch (_terminator)
        {
            case ScanTerminator.CrLf:
                {
                    var i = s.IndexOf("\r\n", StringComparison.Ordinal);
                    return i >= 0 ? (i, 2, ScanTerminator.CrLf) : (-1, 0, default);
                }
            case ScanTerminator.Cr:
                {
                    var i = s.IndexOf('\r');
                    return i >= 0 ? (i, 1, ScanTerminator.Cr) : (-1, 0, default);
                }
            case ScanTerminator.Lf:
                {
                    var i = s.IndexOf('\n');
                    return i >= 0 ? (i, 1, ScanTerminator.Lf) : (-1, 0, default);
                }
            case ScanTerminator.Any:
                {
                    var crLf = s.IndexOf("\r\n", StringComparison.Ordinal);
                    if (crLf >= 0) return (crLf, 2, ScanTerminator.CrLf);
                    var cr = s.IndexOf('\r');
                    var lf = s.IndexOf('\n');
                    if (cr >= 0 && (lf < 0 || cr < lf)) return (cr, 1, ScanTerminator.Cr);
                    if (lf >= 0) return (lf, 1, ScanTerminator.Lf);
                    return (-1, 0, default);
                }
            default:
                return (-1, 0, default);
        }
    }

    private void SetState(SourceConnectionState s)
    {
        if (_state == s) return;
        _state = s;
        StateChanged?.Invoke(this, s);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _port.DataReceived -= OnDataReceived;
        _port.Dispose();
    }
}
```

- [ ] **Step 4: Verificar test pasa**

Run:
```bash
dotnet test --filter "FullyQualifiedName~SerialInputSourceTests.Emits_ScanEvent_OnCrLfTerminator"
```

Expected: `Passed: 1, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add src/AutolumoBarcodeScannerTool.Core/Sources/Serial/ tests/AutolumoBarcodeScannerTool.Tests/Sources/
git commit -m "feat(sources): SerialInputSource con terminador CRLF"
```

---

### Task 19: Cubrir terminadores Cr, Lf, Any y casos borde (TDD)

**Files:**
- Modify: `tests/AutolumoBarcodeScannerTool.Tests/Sources/SerialInputSourceTests.cs`

- [ ] **Step 1: Añadir tests**

Añadir a la clase `SerialInputSourceTests`:

```csharp
    [Fact]
    public void Emits_OnCrOnly_WhenTerminatorCr()
    {
        var (_, port, events) = Build(ScanTerminator.Cr);
        port.EmitText("HELLO\r", Encoding.ASCII);

        events.Count.ShouldBe(1);
        events[0].Payload.ShouldBe("HELLO");
        events[0].DetectedTerminator.ShouldBe(ScanTerminator.Cr);
    }

    [Fact]
    public void Emits_OnLfOnly_WhenTerminatorLf()
    {
        var (_, port, events) = Build(ScanTerminator.Lf);
        port.EmitText("HELLO\n", Encoding.ASCII);

        events.Count.ShouldBe(1);
        events[0].Payload.ShouldBe("HELLO");
        events[0].DetectedTerminator.ShouldBe(ScanTerminator.Lf);
    }

    [Fact]
    public void AnyTerminator_PrefersCrLfOverIndividualCrLf()
    {
        var (_, port, events) = Build(ScanTerminator.Any);
        port.EmitText("A\r\nB\rC\n", Encoding.ASCII);

        events.Count.ShouldBe(3);
        events[0].Payload.ShouldBe("A");
        events[0].DetectedTerminator.ShouldBe(ScanTerminator.CrLf);
        events[1].Payload.ShouldBe("B");
        events[1].DetectedTerminator.ShouldBe(ScanTerminator.Cr);
        events[2].Payload.ShouldBe("C");
        events[2].DetectedTerminator.ShouldBe(ScanTerminator.Lf);
    }

    [Fact]
    public void Buffers_PartialPayload_AcrossMultipleEmissions()
    {
        var (_, port, events) = Build(ScanTerminator.CrLf);

        port.EmitText("ABC", Encoding.ASCII);
        events.ShouldBeEmpty();

        port.EmitText("123\r\n", Encoding.ASCII);
        events.Count.ShouldBe(1);
        events[0].Payload.ShouldBe("ABC123");
    }

    [Fact]
    public void EmitsMultipleEvents_InSingleBurst()
    {
        var (_, port, events) = Build(ScanTerminator.CrLf);
        port.EmitText("ONE\r\nTWO\r\n", Encoding.ASCII);

        events.Count.ShouldBe(2);
        events[0].Payload.ShouldBe("ONE");
        events[1].Payload.ShouldBe("TWO");
    }

    [Fact]
    public void Decodes_Latin1_Correctly()
    {
        var (_, port, events) = Build(ScanTerminator.CrLf, Encoding.Latin1);
        var bytes = Encoding.Latin1.GetBytes("ñandú\r\n");
        port.EmitBytes(bytes);

        events.Count.ShouldBe(1);
        events[0].Payload.ShouldBe("ñandú");
    }

    [Fact]
    public void CrLfTerminator_DoesNotEmit_OnSoloCrWithoutLf()
    {
        var (_, port, events) = Build(ScanTerminator.CrLf);
        port.EmitText("ABC\r", Encoding.ASCII);
        events.ShouldBeEmpty();

        port.EmitText("\n", Encoding.ASCII);
        events.Count.ShouldBe(1);
        events[0].Payload.ShouldBe("ABC");
    }
```

- [ ] **Step 2: Verificar que todos pasan**

Run:
```bash
dotnet test --filter "FullyQualifiedName~SerialInputSourceTests"
```

Expected: `Passed: 8, Failed: 0`.

- [ ] **Step 3: Commit**

```bash
git add tests/AutolumoBarcodeScannerTool.Tests/Sources/
git commit -m "test(sources): cubre terminadores Cr, Lf, Any; multi-evento; encoding Latin1; partial buffering"
```

---

### Task 20: `SystemSerialPortAdapter` — wrap real de `System.IO.Ports.SerialPort`

**Files:**
- Create: `src/AutolumoBarcodeScannerTool.Core/Sources/Serial/SystemSerialPortAdapter.cs`

- [ ] **Step 1: Añadir paquete `System.IO.Ports`**

Run:
```bash
dotnet add src/AutolumoBarcodeScannerTool.Core package System.IO.Ports --version 8.0.0
```

- [ ] **Step 2: Implementar wrapper**

```csharp
using System.IO.Ports;

namespace AutolumoBarcodeScannerTool.Core.Sources.Serial;

public sealed class SystemSerialPortAdapter : ISerialPortAdapter
{
    private readonly SerialPortConfig _config;
    private SerialPort? _port;

    public event EventHandler<ReadOnlyMemory<byte>>? DataReceived;

    public SystemSerialPortAdapter(SerialPortConfig config)
    {
        _config = config;
    }

    public bool IsOpen => _port?.IsOpen ?? false;

    public void Open()
    {
        _port = new SerialPort(_config.PortName, _config.BaudRate)
        {
            DataBits = _config.DataBits,
            Parity = Enum.Parse<Parity>(_config.Parity, ignoreCase: true),
            StopBits = Enum.Parse<StopBits>(_config.StopBits, ignoreCase: true),
            ReadTimeout = 500,
            WriteTimeout = 500
        };
        _port.DataReceived += OnPortDataReceived;
        _port.Open();
    }

    public void Close()
    {
        if (_port is null) return;
        _port.DataReceived -= OnPortDataReceived;
        if (_port.IsOpen) _port.Close();
        _port.Dispose();
        _port = null;
    }

    private void OnPortDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_port is null || !_port.IsOpen) return;
        try
        {
            var available = _port.BytesToRead;
            if (available <= 0) return;
            var buffer = new byte[available];
            var read = _port.Read(buffer, 0, available);
            if (read <= 0) return;
            var data = read == available ? buffer : buffer.AsMemory(0, read).ToArray();
            DataReceived?.Invoke(this, data);
        }
        catch (Exception)
        {
            // intencionalmente silencioso aquí; el source loguea state changes
        }
    }

    public void Dispose() => Close();
}
```

- [ ] **Step 3: Verificar build**

Run:
```bash
dotnet build src/AutolumoBarcodeScannerTool.Core
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/AutolumoBarcodeScannerTool.Core/Sources/Serial/SystemSerialPortAdapter.cs src/AutolumoBarcodeScannerTool.Core/AutolumoBarcodeScannerTool.Core.csproj
git commit -m "feat(sources): SystemSerialPortAdapter wrap real de System.IO.Ports.SerialPort"
```

---

## Fase 6 — Orquestación (TDD)

### Task 21: `ScannerOrchestrator` con fakes (TDD)

**Files:**
- Create: `tests/AutolumoBarcodeScannerTool.Tests/Orchestration/Fakes/FakeInputSource.cs`
- Create: `tests/AutolumoBarcodeScannerTool.Tests/Orchestration/ScannerOrchestratorTests.cs`
- Create: `src/AutolumoBarcodeScannerTool.Core/Orchestration/ScannerOrchestrator.cs`

- [ ] **Step 1: Crear `FakeInputSource`**

```csharp
using AutolumoBarcodeScannerTool.Core.Models;
using AutolumoBarcodeScannerTool.Core.Sources;

namespace AutolumoBarcodeScannerTool.Tests.Orchestration.Fakes;

public sealed class FakeInputSource : IInputSource
{
    public event Func<ScanEvent, Task>? OnScan;
    public event EventHandler<SourceConnectionState>? StateChanged;

    public SourceConnectionState State { get; private set; } = SourceConnectionState.Disconnected;
    public bool StartCalled { get; private set; }
    public bool StopCalled { get; private set; }

    public Task StartAsync(CancellationToken ct) { StartCalled = true; State = SourceConnectionState.Connected; return Task.CompletedTask; }
    public Task StopAsync(CancellationToken ct) { StopCalled = true; State = SourceConnectionState.Disconnected; return Task.CompletedTask; }

    public Task EmitAsync(ScanEvent ev) => OnScan?.Invoke(ev) ?? Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

- [ ] **Step 2: Crear fake sink genérico**

Crear `tests/AutolumoBarcodeScannerTool.Tests/Orchestration/Fakes/FakeSink.cs`:

```csharp
using AutolumoBarcodeScannerTool.Core.Models;
using AutolumoBarcodeScannerTool.Core.Sinks;

namespace AutolumoBarcodeScannerTool.Tests.Orchestration.Fakes;

public sealed class FakeSink : IInputSink
{
    public List<ScanEvent> Received { get; } = new();

    public Task SendAsync(ScanEvent ev, CancellationToken ct)
    {
        Received.Add(ev);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 3: Escribir tests del orchestrator**

```csharp
using AutolumoBarcodeScannerTool.Core.Models;
using AutolumoBarcodeScannerTool.Core.Orchestration;
using AutolumoBarcodeScannerTool.Core.Transforms;
using AutolumoBarcodeScannerTool.Tests.Orchestration.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace AutolumoBarcodeScannerTool.Tests.Orchestration;

public class ScannerOrchestratorTests
{
    private sealed class IdentityTransform : ITerminatorTransform
    {
        public ScanEvent Apply(ScanEvent input) => input with { Payload = input.Payload + "[X]" };
    }

    [Fact]
    public async Task StartAsync_StartsSource()
    {
        var source = new FakeInputSource();
        var sink = new FakeSink();
        var sut = new ScannerOrchestrator(source, new IdentityTransform(), sink,
            NullLogger<ScannerOrchestrator>.Instance);

        await sut.StartAsync(CancellationToken.None);

        source.StartCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task ScanEvent_FromSource_FlowsThroughTransformToSink()
    {
        var source = new FakeInputSource();
        var sink = new FakeSink();
        var sut = new ScannerOrchestrator(source, new IdentityTransform(), sink,
            NullLogger<ScannerOrchestrator>.Instance);
        await sut.StartAsync(CancellationToken.None);

        var ev = new ScanEvent("ABC", ScanTerminator.CrLf, DateTimeOffset.UnixEpoch);
        await source.EmitAsync(ev);

        sink.Received.Count.ShouldBe(1);
        sink.Received[0].Payload.ShouldBe("ABC[X]");
    }

    [Fact]
    public async Task StopAsync_StopsSource()
    {
        var source = new FakeInputSource();
        var sink = new FakeSink();
        var sut = new ScannerOrchestrator(source, new IdentityTransform(), sink,
            NullLogger<ScannerOrchestrator>.Instance);
        await sut.StartAsync(CancellationToken.None);

        await sut.StopAsync(CancellationToken.None);

        source.StopCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task SinkException_IsCaught_AndOrchestratorContinues()
    {
        var source = new FakeInputSource();
        var throwingSink = new ThrowingSink();
        var sut = new ScannerOrchestrator(source, new IdentityTransform(), throwingSink,
            NullLogger<ScannerOrchestrator>.Instance);
        await sut.StartAsync(CancellationToken.None);

        await Should.NotThrowAsync(() => source.EmitAsync(
            new ScanEvent("X", ScanTerminator.CrLf, DateTimeOffset.UnixEpoch)));
    }

    private sealed class ThrowingSink : Core.Sinks.IInputSink
    {
        public Task SendAsync(ScanEvent ev, CancellationToken ct) =>
            throw new InvalidOperationException("boom");
    }
}
```

- [ ] **Step 4: Verificar fallo**

Run:
```bash
dotnet test --filter "FullyQualifiedName~ScannerOrchestratorTests"
```

Expected: FAIL — `ScannerOrchestrator could not be found`.

- [ ] **Step 5: Implementar orchestrator**

```csharp
using AutolumoBarcodeScannerTool.Core.Models;
using AutolumoBarcodeScannerTool.Core.Sinks;
using AutolumoBarcodeScannerTool.Core.Sources;
using AutolumoBarcodeScannerTool.Core.Transforms;
using Microsoft.Extensions.Logging;

namespace AutolumoBarcodeScannerTool.Core.Orchestration;

public sealed class ScannerOrchestrator : IAsyncDisposable
{
    private readonly IInputSource _source;
    private readonly ITerminatorTransform _transform;
    private readonly IInputSink _sink;
    private readonly ILogger<ScannerOrchestrator> _logger;
    private bool _started;

    public ScannerOrchestrator(
        IInputSource source,
        ITerminatorTransform transform,
        IInputSink sink,
        ILogger<ScannerOrchestrator> logger)
    {
        _source = source;
        _transform = transform;
        _sink = sink;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        if (_started) return;
        _source.OnScan += HandleScanAsync;
        await _source.StartAsync(ct).ConfigureAwait(false);
        _started = true;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (!_started) return;
        _source.OnScan -= HandleScanAsync;
        await _source.StopAsync(ct).ConfigureAwait(false);
        _started = false;
    }

    private async Task HandleScanAsync(ScanEvent raw)
    {
        try
        {
            var transformed = _transform.Apply(raw);
            await _sink.SendAsync(transformed, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando escaneo");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        await _source.DisposeAsync().ConfigureAwait(false);
    }
}
```

- [ ] **Step 6: Verificar tests pasan**

Run:
```bash
dotnet test --filter "FullyQualifiedName~ScannerOrchestratorTests"
```

Expected: `Passed: 4, Failed: 0`.

- [ ] **Step 7: Verificar TODOS los tests pasan**

Run:
```bash
dotnet test
```

Expected: `Passed: 28, Failed: 0` (suma de todas las fases anteriores).

- [ ] **Step 8: Commit**

```bash
git add src/AutolumoBarcodeScannerTool.Core/Orchestration/ tests/AutolumoBarcodeScannerTool.Tests/Orchestration/
git commit -m "feat(orchestration): ScannerOrchestrator cableando source→transform→sink"
```

---

## Fase 7 — Implementaciones Win32 (App project, 🪟 Solo Windows para build)

> Las tareas de esta fase tocan código `net8.0-windows` que **no compila en macOS**. Después de cada Edit, no se ejecuta `dotnet build` localmente; el commit refleja la implementación. La validación se hace en la primera build de Windows (al final).

### Task 22: `Win32ForegroundWindowProvider` 🪟

**Files:**
- Create: `src/AutolumoBarcodeScannerTool/Win32/Win32ForegroundWindowProvider.cs`

- [ ] **Step 1: Implementar el provider**

```csharp
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using AutolumoBarcodeScannerTool.Core.Sinks;

namespace AutolumoBarcodeScannerTool.Win32;

internal sealed class Win32ForegroundWindowProvider : IForegroundWindowProvider
{
    public ForegroundWindowInfo? GetCurrent()
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
        catch (ArgumentException)
        {
            return null;
        }

        var title = new StringBuilder(512);
        _ = GetWindowText(hwnd, title, title.Capacity);
        return new ForegroundWindowInfo(processName, title.ToString());
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
}
```

- [ ] **Step 2: Commit**

```bash
git add src/AutolumoBarcodeScannerTool/Win32/Win32ForegroundWindowProvider.cs
git commit -m "feat(win32): Win32ForegroundWindowProvider con GetForegroundWindow + GetWindowThreadProcessId"
```

---

### Task 23: `SendInputInjector` 🪟

**Files:**
- Create: `src/AutolumoBarcodeScannerTool/Win32/SendInputInjector.cs`

- [ ] **Step 1: Implementar injector con manejo especial de `\t` y `\n`**

```csharp
using System.Runtime.InteropServices;
using AutolumoBarcodeScannerTool.Core.Sinks;

namespace AutolumoBarcodeScannerTool.Win32;

internal sealed class SendInputInjector : IInputInjector
{
    private const ushort VK_TAB = 0x09;
    private const ushort VK_RETURN = 0x0D;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    public void SendText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        var inputs = new List<INPUT>(capacity: text.Length * 2);
        foreach (var ch in text)
        {
            if (ch == '\t') AppendVirtualKey(inputs, VK_TAB);
            else if (ch == '\n') AppendVirtualKey(inputs, VK_RETURN);
            else if (ch == '\r') continue; // ya consumido por terminador, ignorar restantes
            else AppendUnicode(inputs, ch);
        }

        if (inputs.Count == 0) return;
        var arr = inputs.ToArray();
        _ = SendInput((uint)arr.Length, arr, Marshal.SizeOf<INPUT>());
    }

    private static void AppendVirtualKey(List<INPUT> list, ushort vk)
    {
        list.Add(MakeKey(vk, 0, 0));
        list.Add(MakeKey(vk, 0, KEYEVENTF_KEYUP));
    }

    private static void AppendUnicode(List<INPUT> list, char ch)
    {
        list.Add(MakeKey(0, ch, KEYEVENTF_UNICODE));
        list.Add(MakeKey(0, ch, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP));
    }

    private static INPUT MakeKey(ushort vk, char scan, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = vk,
                wScan = (ushort)scan,
                dwFlags = flags,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            }
        }
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/AutolumoBarcodeScannerTool/Win32/SendInputInjector.cs
git commit -m "feat(win32): SendInputInjector con manejo de VK_TAB/VK_RETURN y Unicode"
```

---

### Task 24: `AutostartManager` (HKCU\\Run) 🪟

**Files:**
- Create: `src/AutolumoBarcodeScannerTool/Autostart/AutostartManager.cs`

- [ ] **Step 1: Implementar manager**

```csharp
using Microsoft.Win32;

namespace AutolumoBarcodeScannerTool.Autostart;

internal sealed class AutostartManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AutolumoBarcodeScannerTool";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        return key?.GetValue(ValueName) is not null;
    }

    public void Enable(string executablePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException("No se pudo abrir HKCU\\" + RunKey);
        key.SetValue(ValueName, $"\"{executablePath}\" --minimized", RegistryValueKind.String);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/AutolumoBarcodeScannerTool/Autostart/AutostartManager.cs
git commit -m "feat(autostart): AutostartManager para HKCU\\Run"
```

---

### Task 25: `HidDeviceEnumerator` — listar HID con VID/PID 🪟

**Files:**
- Create: `src/AutolumoBarcodeScannerTool/Hid/HidDeviceEnumerator.cs`

- [ ] **Step 1: Implementar enumerador usando WMI (más simple que SetupAPI)**

> Usamos WMI vía `System.Management` para evitar marshallear SetupAPI/HID structs.

Run:
```bash
dotnet add src/AutolumoBarcodeScannerTool package System.Management --version 8.0.0
```

Crear archivo:

```csharp
using System.Management;
using System.Text.RegularExpressions;

namespace AutolumoBarcodeScannerTool.Hid;

internal sealed record HidDeviceDescriptor(
    string Description,
    string DeviceId,
    string VendorId,
    string ProductId);

internal static class HidDeviceEnumerator
{
    private static readonly Regex VidPidRegex =
        new(@"VID_(?<vid>[0-9A-F]{4})&PID_(?<pid>[0-9A-F]{4})", RegexOptions.IgnoreCase);

    public static IReadOnlyList<HidDeviceDescriptor> Enumerate()
    {
        var list = new List<HidDeviceDescriptor>();
        using var searcher = new ManagementObjectSearcher(
            "SELECT Description, DeviceID FROM Win32_PnPEntity WHERE DeviceID LIKE 'HID%'");

        foreach (var obj in searcher.Get().Cast<ManagementObject>())
        {
            var description = obj["Description"]?.ToString() ?? "";
            var deviceId = obj["DeviceID"]?.ToString() ?? "";
            var match = VidPidRegex.Match(deviceId);
            if (!match.Success) continue;
            var vid = "0x" + match.Groups["vid"].Value.ToUpperInvariant();
            var pid = "0x" + match.Groups["pid"].Value.ToUpperInvariant();
            list.Add(new HidDeviceDescriptor(description, deviceId, vid, pid));
        }
        return list;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/AutolumoBarcodeScannerTool/Hid/HidDeviceEnumerator.cs src/AutolumoBarcodeScannerTool/AutolumoBarcodeScannerTool.csproj
git commit -m "feat(hid): HidDeviceEnumerator vía WMI (Win32_PnPEntity)"
```

---

### Task 26: `LowLevelKeyboardHook` 🪟

**Files:**
- Create: `src/AutolumoBarcodeScannerTool/Hid/LowLevelKeyboardHook.cs`

- [ ] **Step 1: Implementar hook reutilizable**

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
git commit -m "feat(hid): LowLevelKeyboardHook (WH_KEYBOARD_LL) con callback shouldSuppress"
```

---

### Task 27: `RawInputInterop` — registro y parsing de WM_INPUT 🪟

**Files:**
- Create: `src/AutolumoBarcodeScannerTool/Hid/RawInputInterop.cs`

- [ ] **Step 1: Implementar wrappers Raw Input**

```csharp
using System.Runtime.InteropServices;

namespace AutolumoBarcodeScannerTool.Hid;

internal static class RawInputInterop
{
    public const int WM_INPUT = 0x00FF;
    public const ushort HID_USAGE_PAGE_GENERIC = 0x01;
    public const ushort HID_USAGE_GENERIC_KEYBOARD = 0x06;
    public const uint RIDEV_INPUTSINK = 0x00000100;
    public const uint RID_INPUT = 0x10000003;
    public const uint RIDI_DEVICENAME = 0x20000007;

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWINPUTDEVICE
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr WindowHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWINPUTHEADER
    {
        public uint Type;
        public uint Size;
        public IntPtr DeviceHandle;
        public IntPtr WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWKEYBOARD
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWINPUT
    {
        public RAWINPUTHEADER Header;
        public RAWKEYBOARD Keyboard;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterRawInputDevices(
        [In] RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetRawInputData(
        IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint GetRawInputDeviceInfo(
        IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);

    public static string? GetDeviceName(IntPtr hDevice)
    {
        uint size = 0;
        _ = GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref size);
        if (size == 0) return null;

        var bytes = size * 2; // unicode chars
        var ptr = Marshal.AllocHGlobal((int)bytes);
        try
        {
            _ = GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, ptr, ref size);
            return Marshal.PtrToStringUni(ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/AutolumoBarcodeScannerTool/Hid/RawInputInterop.cs
git commit -m "feat(hid): RawInputInterop con P/Invokes y helpers de device name"
```

---

### Task 28: `HidKeyboardInputSource` — integración Raw Input + LL Hook 🪟

**Files:**
- Create: `src/AutolumoBarcodeScannerTool/Hid/HidKeyboardInputSource.cs`

- [ ] **Step 1: Implementar source con ventana oculta para WM_INPUT**

```csharp
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using AutolumoBarcodeScannerTool.Core.Models;
using AutolumoBarcodeScannerTool.Core.Sources;
using Microsoft.Extensions.Logging;
using static AutolumoBarcodeScannerTool.Hid.RawInputInterop;

namespace AutolumoBarcodeScannerTool.Hid;

internal sealed class HidKeyboardInputSource : NativeWindow, IInputSource
{
    private readonly string _vendorIdHex;
    private readonly string _productIdHex;
    private readonly ScanTerminator _terminator;
    private readonly ILogger<HidKeyboardInputSource> _logger;
    private readonly ConcurrentQueue<(uint VKey, DateTime When, IntPtr Device)> _recentScannerKeys = new();
    private readonly System.Text.StringBuilder _payload = new();
    private LowLevelKeyboardHook? _hook;
    private SourceConnectionState _state = SourceConnectionState.Disconnected;
    private string? _scannerDeviceNameFragment;

    public event Func<ScanEvent, Task>? OnScan;
    public event EventHandler<SourceConnectionState>? StateChanged;

    public HidKeyboardInputSource(
        string vendorIdHex,
        string productIdHex,
        ScanTerminator terminator,
        ILogger<HidKeyboardInputSource> logger)
    {
        _vendorIdHex = NormalizeHex(vendorIdHex);
        _productIdHex = NormalizeHex(productIdHex);
        _terminator = terminator;
        _logger = logger;
    }

    public SourceConnectionState State => _state;

    public Task StartAsync(CancellationToken ct)
    {
        var cp = new CreateParams { Caption = "AutolumoHiddenInputWindow", X = 0, Y = 0, Width = 0, Height = 0 };
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
        {
            _logger.LogError("RegisterRawInputDevices falló: {Err}", Marshal.GetLastWin32Error());
            SetState(SourceConnectionState.Error);
            return Task.CompletedTask;
        }

        _scannerDeviceNameFragment = $"VID_{_vendorIdHex}&PID_{_productIdHex}";
        _hook = new LowLevelKeyboardHook(ShouldSuppress);
        _hook.Install();
        SetState(SourceConnectionState.Connected);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _hook?.Uninstall();
        _hook = null;
        if (Handle != IntPtr.Zero) DestroyHandle();
        SetState(SourceConnectionState.Disconnected);
        return Task.CompletedTask;
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

            var deviceName = GetDeviceName(input.Header.DeviceHandle) ?? "";
            if (!IsScannerDevice(deviceName)) return;

            // Solo nos interesa keydown
            const ushort RI_KEY_BREAK = 0x01;
            if ((input.Keyboard.Flags & RI_KEY_BREAK) != 0) return;

            _recentScannerKeys.Enqueue((input.Keyboard.VKey, DateTime.UtcNow, input.Header.DeviceHandle));
            TrimRecent();

            // Acumular char en payload
            var ch = MapVKeyToChar(input.Keyboard.VKey);
            if (ch is null) return;

            lock (_payload)
            {
                if (ch == '\r' || ch == '\n')
                {
                    var detected = ResolveTerminator(ch.Value);
                    if (detected is null) return;
                    var text = _payload.ToString();
                    _payload.Clear();
                    OnScan?.Invoke(new ScanEvent(text, detected.Value, DateTimeOffset.UtcNow));
                }
                else
                {
                    _payload.Append(ch.Value);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private ScanTerminator? ResolveTerminator(char ch) => _terminator switch
    {
        ScanTerminator.CrLf when ch == '\n' => ScanTerminator.CrLf,
        ScanTerminator.CrLf => null, // ignorar CR sin LF en este modo
        ScanTerminator.Cr when ch == '\r' => ScanTerminator.Cr,
        ScanTerminator.Lf when ch == '\n' => ScanTerminator.Lf,
        ScanTerminator.Any when ch == '\r' => ScanTerminator.Cr,
        ScanTerminator.Any when ch == '\n' => ScanTerminator.Lf,
        _ => null
    };

    private bool ShouldSuppress(int vkCode)
    {
        var cutoff = DateTime.UtcNow.AddMilliseconds(-50);
        foreach (var (vk, when, _) in _recentScannerKeys)
        {
            if (when >= cutoff && vk == vkCode) return true;
        }
        return false;
    }

    private void TrimRecent()
    {
        var cutoff = DateTime.UtcNow.AddMilliseconds(-200);
        while (_recentScannerKeys.TryPeek(out var first) && first.When < cutoff)
            _recentScannerKeys.TryDequeue(out _);
    }

    private bool IsScannerDevice(string deviceName) =>
        _scannerDeviceNameFragment is not null &&
        deviceName.Contains(_scannerDeviceNameFragment, StringComparison.OrdinalIgnoreCase);

    private static char? MapVKeyToChar(ushort vKey)
    {
        // Mapeo minimal para barcode payloads (alfanumérico + comunes)
        // Para producción se usaría ToUnicode con keyboard layout; aquí simplificado.
        if (vKey >= 0x30 && vKey <= 0x39) return (char)vKey; // 0-9
        if (vKey >= 0x41 && vKey <= 0x5A) return (char)vKey; // A-Z
        if (vKey == 0x0D) return '\r';
        if (vKey == 0x09) return '\t';
        return null;
    }

    private static string NormalizeHex(string s)
    {
        var t = s.Trim();
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) t = t[2..];
        return t.ToUpperInvariant().PadLeft(4, '0');
    }

    private void SetState(SourceConnectionState s)
    {
        if (_state == s) return;
        _state = s;
        StateChanged?.Invoke(this, s);
    }

    public ValueTask DisposeAsync()
    {
        return new ValueTask(StopAsync(CancellationToken.None));
    }
}
```

> **Nota de simplificación:** el mapeo VKey→char es minimalista (solo `0-9`, `A-Z`, `\r`, `\t`) — suficiente para códigos de barras estándar (EAN, Code 128, QR alfanuméricos). Para soporte de símbolos completos se debe usar `ToUnicodeEx` con el layout del teclado. Documentado como limitación conocida.

- [ ] **Step 2: Commit**

```bash
git add src/AutolumoBarcodeScannerTool/Hid/HidKeyboardInputSource.cs
git commit -m "feat(hid): HidKeyboardInputSource integrando Raw Input + LL Hook"
```

---

## Fase 8 — Logging, DI y Program.cs 🪟

### Task 29: Añadir Serilog y paquetes de hosting

**Files:**
- Modify: `src/AutolumoBarcodeScannerTool/AutolumoBarcodeScannerTool.csproj`

- [ ] **Step 1: Añadir paquetes**

Run:
```bash
dotnet add src/AutolumoBarcodeScannerTool package Microsoft.Extensions.Hosting --version 8.0.1
dotnet add src/AutolumoBarcodeScannerTool package Microsoft.Extensions.Configuration.Ini --version 8.0.0
dotnet add src/AutolumoBarcodeScannerTool package Serilog --version 4.0.2
dotnet add src/AutolumoBarcodeScannerTool package Serilog.Extensions.Hosting --version 8.0.0
dotnet add src/AutolumoBarcodeScannerTool package Serilog.Sinks.File --version 6.0.0
```

- [ ] **Step 2: Commit**

```bash
git add src/AutolumoBarcodeScannerTool/AutolumoBarcodeScannerTool.csproj
git commit -m "chore(app): añade Hosting, Configuration.Ini y Serilog"
```

---

### Task 30: `Program.cs` con Generic Host y DI 🪟

**Files:**
- Modify: `src/AutolumoBarcodeScannerTool/Program.cs`

- [ ] **Step 1: Reemplazar Program.cs con bootstrap completo**

```csharp
using System.IO;
using System.Text;
using AutolumoBarcodeScannerTool.Autostart;
using AutolumoBarcodeScannerTool.Core.Models;
using AutolumoBarcodeScannerTool.Core.Orchestration;
using AutolumoBarcodeScannerTool.Core.Sinks;
using AutolumoBarcodeScannerTool.Core.Sources;
using AutolumoBarcodeScannerTool.Core.Sources.Serial;
using AutolumoBarcodeScannerTool.Core.Transforms;
using AutolumoBarcodeScannerTool.Hid;
using AutolumoBarcodeScannerTool.Tray;
using AutolumoBarcodeScannerTool.Win32;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;

namespace AutolumoBarcodeScannerTool;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutolumoBarcodeScannerTool");
        Directory.CreateDirectory(configDir);

        var configPath = Path.Combine(configDir, "appsettings.ini");
        if (!File.Exists(configPath))
        {
            var defaultIni = Path.Combine(AppContext.BaseDirectory, "appsettings.default.ini");
            if (File.Exists(defaultIni)) File.Copy(defaultIni, configPath);
        }

        var logsDir = Path.Combine(configDir, "logs");
        Directory.CreateDirectory(logsDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                path: Path.Combine(logsDir, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            using var host = BuildHost(configPath, args);
            host.Start();

            var tray = host.Services.GetRequiredService<TrayIconController>();
            tray.Show();

            Application.Run();

            host.StopAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fallo fatal en arranque");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static IHost BuildHost(string configPath, string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureAppConfiguration((_, builder) =>
            {
                builder.Sources.Clear();
                builder.AddIniFile(configPath, optional: false, reloadOnChange: true);
            })
            .ConfigureServices((ctx, services) =>
            {
                services.Configure<ScannerOptions>(ctx.Configuration.GetSection(ScannerOptions.SectionName));
                services.Configure<TargetOptions>(ctx.Configuration.GetSection($"{ScannerOptions.SectionName}:Target"));
                services.Configure<OutputOptions>(ctx.Configuration.GetSection($"{ScannerOptions.SectionName}:Output"));
                services.Configure<AutostartOptions>(ctx.Configuration.GetSection(AutostartOptions.SectionName));
                services.Configure<LoggingOptions>(ctx.Configuration.GetSection(LoggingOptions.SectionName));

                services.AddSingleton(_ => configPath);
                services.AddSingleton<AutostartManager>();
                services.AddSingleton<IForegroundWindowProvider, Win32ForegroundWindowProvider>();
                services.AddSingleton<IInputInjector, SendInputInjector>();
                services.AddSingleton<ITerminatorTransform, ReplaceTerminatorTransform>();
                services.AddSingleton<IInputSink, ForegroundProcessSink>();

                services.AddSingleton<IInputSource>(sp =>
                {
                    var opts = sp.GetRequiredService<IOptionsMonitor<ScannerOptions>>().CurrentValue;
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                    return opts.SourceType switch
                    {
                        SourceType.Serial => CreateSerial(opts, loggerFactory),
                        SourceType.HidKeyboard => CreateHid(opts, loggerFactory),
                        _ => throw new InvalidOperationException("SourceType desconocido")
                    };
                });

                services.AddSingleton<ScannerOrchestrator>();
                services.AddSingleton<TrayIconController>();
                services.AddHostedService<OrchestratorHostedService>();
            })
            .Build();

    private static IInputSource CreateSerial(ScannerOptions opts, ILoggerFactory lf)
    {
        var encoding = opts.Serial.Encoding switch
        {
            SerialEncoding.Utf8 => Encoding.UTF8,
            SerialEncoding.Latin1 => Encoding.Latin1,
            _ => Encoding.ASCII
        };
        var adapter = new SystemSerialPortAdapter(new SerialPortConfig(
            opts.Serial.PortName, opts.Serial.BaudRate, opts.Serial.DataBits,
            opts.Serial.Parity, opts.Serial.StopBits, encoding));
        return new SerialInputSource(adapter, opts.Terminator, encoding,
            lf.CreateLogger<SerialInputSource>());
    }

    private static IInputSource CreateHid(ScannerOptions opts, ILoggerFactory lf) =>
        new HidKeyboardInputSource(
            opts.HidKeyboard.VendorId, opts.HidKeyboard.ProductId,
            opts.Terminator, lf.CreateLogger<HidKeyboardInputSource>());
}

internal sealed class OrchestratorHostedService : IHostedService
{
    private readonly ScannerOrchestrator _orchestrator;
    public OrchestratorHostedService(ScannerOrchestrator orchestrator) { _orchestrator = orchestrator; }
    public Task StartAsync(CancellationToken ct) => _orchestrator.StartAsync(ct);
    public Task StopAsync(CancellationToken ct) => _orchestrator.StopAsync(ct);
}
```

- [ ] **Step 2: Commit**

```bash
git add src/AutolumoBarcodeScannerTool/Program.cs
git commit -m "feat(app): Program.cs con Generic Host, DI, Serilog, ini config y orchestrator hosted service"
```

---

## Fase 9 — Tray UI 🪟

### Task 31: ~~Recursos de íconos~~ — diferido

**Estado:** Diferido. El `TrayIconController` (Task 32) usa `SystemIcons.Application` como fallback cuando no encuentra los recursos embebidos, así que la app funciona sin íconos custom. Crear `.ico` reales (verde activo / amarillo pausado / rojo error) es trabajo de polish post-MVP y se hace en Windows con cualquier editor de íconos. Cuando se añadan, embeber así:

```xml
<ItemGroup>
  <EmbeddedResource Include="Tray\Resources\tray-active.ico" />
  <EmbeddedResource Include="Tray\Resources\tray-paused.ico" />
  <EmbeddedResource Include="Tray\Resources\tray-error.ico" />
</ItemGroup>
```

No requiere commit. Continuar con Task 32.

---

### Task 32: `TrayIconController` 🪟

**Files:**
- Create: `src/AutolumoBarcodeScannerTool/Tray/TrayIconController.cs`

- [ ] **Step 1: Implementar controlador del tray**

```csharp
using System.Diagnostics;
using System.IO;
using System.Reflection;
using AutolumoBarcodeScannerTool.Autostart;
using AutolumoBarcodeScannerTool.Core.Models;
using AutolumoBarcodeScannerTool.Core.Orchestration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutolumoBarcodeScannerTool.Tray;

internal sealed class TrayIconController : IDisposable
{
    private readonly ScannerOrchestrator _orchestrator;
    private readonly AutostartManager _autostart;
    private readonly string _configPath;
    private readonly ILogger<TrayIconController> _logger;
    private readonly IOptionsMonitor<ScannerOptions> _opts;
    private readonly NotifyIcon _icon = new();
    private readonly ContextMenuStrip _menu = new();
    private bool _enabled = true;
    private SettingsForm? _settings;

    public TrayIconController(
        ScannerOrchestrator orchestrator,
        AutostartManager autostart,
        IOptionsMonitor<ScannerOptions> opts,
        string configPath,
        ILogger<TrayIconController> logger)
    {
        _orchestrator = orchestrator;
        _autostart = autostart;
        _opts = opts;
        _configPath = configPath;
        _logger = logger;
    }

    public void Show()
    {
        _icon.Text = "Autolumo Barcode Scanner Tool";
        _icon.Icon = LoadIcon("tray-active.ico");
        BuildMenu();
        _icon.ContextMenuStrip = _menu;
        _icon.Visible = true;
    }

    private void BuildMenu()
    {
        _menu.Items.Clear();
        var toggle = new ToolStripMenuItem(_enabled ? "Pausar" : "Reanudar");
        toggle.Click += async (_, _) => await ToggleAsync();
        _menu.Items.Add(toggle);

        var settings = new ToolStripMenuItem("Configuración...");
        settings.Click += (_, _) => OpenSettings();
        _menu.Items.Add(settings);

        var logs = new ToolStripMenuItem("Ver logs");
        logs.Click += (_, _) => OpenLogsFolder();
        _menu.Items.Add(logs);

        _menu.Items.Add(new ToolStripSeparator());

        var quit = new ToolStripMenuItem("Salir");
        quit.Click += (_, _) => Application.Exit();
        _menu.Items.Add(quit);
    }

    private async Task ToggleAsync()
    {
        if (_enabled)
        {
            await _orchestrator.StopAsync(CancellationToken.None);
            _icon.Icon = LoadIcon("tray-paused.ico");
            _enabled = false;
        }
        else
        {
            await _orchestrator.StartAsync(CancellationToken.None);
            _icon.Icon = LoadIcon("tray-active.ico");
            _enabled = true;
        }
        BuildMenu();
    }

    private void OpenSettings()
    {
        if (_settings is { IsDisposed: false })
        {
            _settings.BringToFront();
            return;
        }
        _settings = new SettingsForm(_opts.CurrentValue, _autostart, _configPath);
        _settings.Show();
    }

    private void OpenLogsFolder()
    {
        var logsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutolumoBarcodeScannerTool", "logs");
        if (Directory.Exists(logsDir))
            Process.Start(new ProcessStartInfo("explorer.exe", logsDir) { UseShellExecute = true });
    }

    private static Icon LoadIcon(string name)
    {
        var asm = Assembly.GetExecutingAssembly();
        var resName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase));
        if (resName is null) return SystemIcons.Application;
        using var stream = asm.GetManifestResourceStream(resName);
        return stream is null ? SystemIcons.Application : new Icon(stream);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        _settings?.Dispose();
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/AutolumoBarcodeScannerTool/Tray/TrayIconController.cs
git commit -m "feat(tray): TrayIconController con menú Pausar/Configuración/Ver logs/Salir"
```

---

### Task 33: `SettingsForm` con pestañas 🪟

**Files:**
- Create: `src/AutolumoBarcodeScannerTool/Tray/SettingsForm.cs`

- [ ] **Step 1: Implementar SettingsForm (sin Designer para mantener simple)**

```csharp
using System.IO.Ports;
using AutolumoBarcodeScannerTool.Autostart;
using AutolumoBarcodeScannerTool.Core.Configuration;
using AutolumoBarcodeScannerTool.Core.Models;
using AutolumoBarcodeScannerTool.Hid;

namespace AutolumoBarcodeScannerTool.Tray;

internal sealed class SettingsForm : Form
{
    private readonly ScannerOptions _opts;
    private readonly AutostartManager _autostart;
    private readonly string _configPath;

    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };

    private readonly ComboBox _sourceType = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _terminator = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _enabled = new() { Text = "Servicio habilitado", AutoSize = true };
    private readonly CheckBox _autostartChk = new() { Text = "Autoarranque al iniciar sesión", AutoSize = true };

    private readonly ComboBox _portName = new() { DropDownStyle = ComboBoxStyle.DropDown };
    private readonly TextBox _baudRate = new();
    private readonly ComboBox _parity = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _stopBits = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _encoding = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    private readonly ComboBox _hidDevice = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _vid = new();
    private readonly TextBox _pid = new();

    private readonly TextBox _processName = new();
    private readonly TextBox _windowTitleContains = new();

    private readonly ComboBox _outputMode = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _outputSuffix = new();

    private readonly Button _save = new() { Text = "Guardar", Width = 100 };
    private readonly Button _cancel = new() { Text = "Cancelar", Width = 100 };

    public SettingsForm(ScannerOptions opts, AutostartManager autostart, string configPath)
    {
        _opts = opts;
        _autostart = autostart;
        _configPath = configPath;

        Text = "Autolumo Barcode Scanner Tool — Configuración";
        Width = 560; Height = 480;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        BuildTabs();
        BuildButtons();
        LoadValues();
    }

    private void BuildTabs()
    {
        var general = new TabPage("General");
        general.Controls.Add(Stack(8,
            _enabled,
            _autostartChk,
            Label("Tipo de fuente:"), _sourceType,
            Label("Terminador:"), _terminator
        ));
        _sourceType.Items.AddRange(new object[] { "Serial", "HidKeyboard" });
        _terminator.Items.AddRange(new object[] { "Cr", "Lf", "CrLf", "Any" });

        var serial = new TabPage("Serial");
        _portName.Items.AddRange(SerialPort.GetPortNames());
        _parity.Items.AddRange(Enum.GetNames<Parity>());
        _stopBits.Items.AddRange(Enum.GetNames<StopBits>());
        _encoding.Items.AddRange(new object[] { "Ascii", "Utf8", "Latin1" });
        serial.Controls.Add(Stack(8,
            Label("Puerto:"), _portName,
            Label("Baud rate:"), _baudRate,
            Label("Parity:"), _parity,
            Label("Stop bits:"), _stopBits,
            Label("Encoding:"), _encoding
        ));

        var hid = new TabPage("HID");
        hid.Controls.Add(Stack(8,
            Label("Dispositivo detectado:"), _hidDevice,
            Label("VID (hex):"), _vid,
            Label("PID (hex):"), _pid
        ));
        _hidDevice.SelectedIndexChanged += (_, _) =>
        {
            if (_hidDevice.SelectedItem is HidDeviceDescriptor d)
            {
                _vid.Text = d.VendorId; _pid.Text = d.ProductId;
            }
        };
        foreach (var d in HidDeviceEnumerator.Enumerate())
            _hidDevice.Items.Add(d);

        var target = new TabPage("Destino");
        target.Controls.Add(Stack(8,
            Label("Nombre de proceso (sin .exe):"), _processName,
            Label("Contiene en título (opcional):"), _windowTitleContains
        ));

        var output = new TabPage("Salida");
        _outputMode.Items.AddRange(new object[] { "Tab", "TabEnter", "TabOnly", "Custom" });
        output.Controls.Add(Stack(8,
            Label("Modo al terminador:"), _outputMode,
            Label("Sufijo (Custom; tokens {TAB} {ENTER}):"), _outputSuffix
        ));

        _tabs.TabPages.AddRange(new[] { general, serial, hid, target, output });
        Controls.Add(_tabs);
    }

    private void BuildButtons()
    {
        _save.Click += (_, _) => Save();
        _cancel.Click += (_, _) => Close();
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 40
        };
        bar.Controls.Add(_save);
        bar.Controls.Add(_cancel);
        Controls.Add(bar);
    }

    private void LoadValues()
    {
        _enabled.Checked = _opts.Enabled;
        _autostartChk.Checked = _autostart.IsEnabled();
        _sourceType.SelectedItem = _opts.SourceType.ToString();
        _terminator.SelectedItem = _opts.Terminator.ToString();

        _portName.Text = _opts.Serial.PortName;
        _baudRate.Text = _opts.Serial.BaudRate.ToString();
        _parity.SelectedItem = _opts.Serial.Parity;
        _stopBits.SelectedItem = _opts.Serial.StopBits;
        _encoding.SelectedItem = _opts.Serial.Encoding.ToString();

        _vid.Text = _opts.HidKeyboard.VendorId;
        _pid.Text = _opts.HidKeyboard.ProductId;

        _processName.Text = _opts.Target.ProcessName;
        _windowTitleContains.Text = _opts.Target.WindowTitleContains ?? "";

        _outputMode.SelectedItem = _opts.Output.OnTerminator.ToString();
        _outputSuffix.Text = _opts.Output.OutputSuffix;
    }

    private void Save()
    {
        try
        {
            var contents = File.ReadAllText(_configPath);
            contents = IniConfigWriter.Update(contents, "Scanner:Enabled", _enabled.Checked ? "true" : "false");
            contents = IniConfigWriter.Update(contents, "Scanner:SourceType", _sourceType.SelectedItem?.ToString() ?? "Serial");
            contents = IniConfigWriter.Update(contents, "Scanner:Terminator", _terminator.SelectedItem?.ToString() ?? "CrLf");
            contents = IniConfigWriter.Update(contents, "Scanner:Serial:PortName", _portName.Text);
            contents = IniConfigWriter.Update(contents, "Scanner:Serial:BaudRate", _baudRate.Text);
            contents = IniConfigWriter.Update(contents, "Scanner:Serial:Parity", _parity.SelectedItem?.ToString() ?? "None");
            contents = IniConfigWriter.Update(contents, "Scanner:Serial:StopBits", _stopBits.SelectedItem?.ToString() ?? "One");
            contents = IniConfigWriter.Update(contents, "Scanner:Serial:Encoding", _encoding.SelectedItem?.ToString() ?? "Ascii");
            contents = IniConfigWriter.Update(contents, "Scanner:HidKeyboard:VendorId", _vid.Text);
            contents = IniConfigWriter.Update(contents, "Scanner:HidKeyboard:ProductId", _pid.Text);
            contents = IniConfigWriter.Update(contents, "Scanner:Target:ProcessName", _processName.Text);
            contents = IniConfigWriter.Update(contents, "Scanner:Target:WindowTitleContains", _windowTitleContains.Text);
            contents = IniConfigWriter.Update(contents, "Scanner:Output:OnTerminator", _outputMode.SelectedItem?.ToString() ?? "Tab");
            contents = IniConfigWriter.Update(contents, "Scanner:Output:OutputSuffix", _outputSuffix.Text);
            File.WriteAllText(_configPath, contents);

            if (_autostartChk.Checked) _autostart.Enable(Application.ExecutablePath);
            else _autostart.Disable();

            MessageBox.Show("Configuración guardada. Los cambios se aplican automáticamente.",
                "Autolumo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al guardar: {ex.Message}", "Autolumo", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static Label Label(string text) => new() { Text = text, AutoSize = true };
    private static FlowLayoutPanel Stack(int gap, params Control[] children)
    {
        var p = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(12)
        };
        foreach (var c in children)
        {
            c.Margin = new Padding(0, 0, 0, gap);
            if (c is TextBox or ComboBox) c.Width = 480;
            p.Controls.Add(c);
        }
        return p;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/AutolumoBarcodeScannerTool/Tray/SettingsForm.cs
git commit -m "feat(tray): SettingsForm con pestañas General/Serial/HID/Destino/Salida"
```

---

## Fase 10 — Instalador y documentación

### Task 34: Inno Setup script 🪟

**Files:**
- Create: `installer/AutolumoBarcodeScannerTool.iss`

- [ ] **Step 1: Crear script Inno Setup**

```iss
; AutolumoBarcodeScannerTool — Inno Setup Script
; Compilar con: ISCC.exe installer\AutolumoBarcodeScannerTool.iss

#define MyAppName "Autolumo Barcode Scanner Tool"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "Labotech"
#define MyAppExeName "AutolumoBarcodeScannerTool.exe"

[Setup]
AppId={{B7C49E3A-7C2F-4A6D-9F11-0AE3D6E5E0F1}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Autolumo\BarcodeScannerTool
DefaultGroupName=Autolumo
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=AutolumoBarcodeScannerTool-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0.17763
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "autostart"; Description: "Iniciar al iniciar sesión de Windows"; GroupDescription: "Opciones:"
Name: "launch"; Description: "Ejecutar al finalizar la instalación"; GroupDescription: "Opciones:"

[Files]
Source: "..\src\AutolumoBarcodeScannerTool\bin\Release\net8.0-windows\win-x64\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\AutolumoBarcodeScannerTool\bin\Release\net8.0-windows\win-x64\publish\appsettings.default.ini"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
    ValueName: "AutolumoBarcodeScannerTool"; \
    ValueData: """{app}\{#MyAppExeName}"" --minimized"; \
    Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Ejecutar"; Flags: postinstall nowait skipifsilent; Tasks: launch
```

- [ ] **Step 2: Commit**

```bash
git add installer/
git commit -m "chore(installer): script Inno Setup con autostart opcional"
```

---

### Task 35: README y checklist de pruebas manuales

**Files:**
- Create: `README.md`
- Create: `docs/manual-test-checklist.md`

- [ ] **Step 1: Crear README**

```markdown
# Autolumo Barcode Scanner Tool

App Windows que intercepta el input de un lector de código de barras (Serial COM o HID-keyboard) y reemplaza el terminador `CRLF` por `TAB` (configurable) antes de inyectarlo en una app destino filtrada por proceso.

## Requisitos

- Windows 10 (build 17763) o superior, x64
- Permisos de usuario estándar (no requiere admin)

## Build (en Windows)

```cmd
dotnet publish src\AutolumoBarcodeScannerTool -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

El binario queda en `src\AutolumoBarcodeScannerTool\bin\Release\net8.0-windows\win-x64\publish\AutolumoBarcodeScannerTool.exe`.

## Build del instalador (en Windows)

Requiere [Inno Setup 6](https://jrsoftware.org/isdl.php).

```cmd
ISCC.exe installer\AutolumoBarcodeScannerTool.iss
```

El instalador queda en `artifacts\installer\`.

## Tests (cualquier plataforma)

```bash
dotnet test
```

## Configuración

Tras instalar, abrir `Configuración` desde el ícono de bandeja. La configuración se guarda en:

```
%LOCALAPPDATA%\AutolumoBarcodeScannerTool\appsettings.ini
```

Los logs en:

```
%LOCALAPPDATA%\AutolumoBarcodeScannerTool\logs\app-YYYYMMDD.log
```

## Estructura

- `src/AutolumoBarcodeScannerTool.Core` — lógica cross-platform (modelos, transforms, source serial, sink, config writer)
- `src/AutolumoBarcodeScannerTool` — WinForms tray, Win32 P/Invokes, HID, autostart
- `tests/AutolumoBarcodeScannerTool.Tests` — xUnit + Shouldly
- `installer/` — script Inno Setup
- `docs/` — specs, plans, checklist manual

## Licencia

Propietaria — Labotech.
```

- [ ] **Step 2: Crear checklist de pruebas manuales**

Crear `docs/manual-test-checklist.md`:

```markdown
# Checklist de pruebas manuales

Pruebas que requieren hardware real (lector de código de barras) o validación en Windows.
Marcar cada caso con la fecha y el responsable.

## Modo Serial

- [ ] Lector configurado en modo Serial (USB-CDC) reconocido como COM auto-asignado
- [ ] Escanear un código alfanumérico → llega al campo destino con `TAB` al final, no `CRLF`
- [ ] Desconectar el lector → ícono de bandeja cambia a rojo, log registra `Disconnected`
- [ ] Reconectar el lector → ícono vuelve a verde, escaneo siguiente funciona
- [ ] Cambiar `Encoding` a `Latin1` en config → escanear código con `ñ` se transfiere correctamente

## Modo HID-keyboard

- [ ] Lector configurado en modo HID-keyboard, identificado por VID/PID en Settings
- [ ] Escanear → solo el resultado transformado llega a la app destino (las teclas crudas son suprimidas)
- [ ] Tipear manualmente en el campo destino → el tecleo manual NO es suprimido
- [ ] Desenchufar y reenchufar el lector → reconexión automática

## Foreground

- [ ] App destino al frente → escaneo entrega input
- [ ] App destino minimizada / otra app al frente → escaneo descartado + log info
- [ ] Cambiar `WindowTitleContains` y abrir ventana con título distinto → escaneo descartado
- [ ] Cambiar ventana al título correcto → escaneo entrega input

## Concurrencia

- [ ] Disparar dos escaneos en menos de 200ms → el segundo se descarta y se loguea warning

## Autostart

- [ ] Activar autoarranque en Settings → reiniciar Windows → app aparece en bandeja al login
- [ ] Desactivar autoarranque → reiniciar → app NO aparece

## Hot-reload

- [ ] Editar `appsettings.ini` y cambiar `Scanner:Output:OnTerminator` de `Tab` a `TabEnter` → próximo escaneo agrega `TAB+ENTER` sin reiniciar app

## Antivirus

- [ ] Windows Defender no marca el binario tras 24h en uso normal
- [ ] (Si aplica) AV corporativo permite el binario (whitelist o no levanta alerta)
```

- [ ] **Step 3: Commit**

```bash
git add README.md docs/manual-test-checklist.md
git commit -m "docs: README y checklist de pruebas manuales"
```

---

## Fase 11 — Validación en Windows 🪟

### Task 36: Build y publish del solution completo en Windows

> Ejecutar en una máquina/VM Windows con .NET 8 SDK instalado.

- [ ] **Step 1: Restore y build de toda la solución**

```cmd
dotnet restore
dotnet build -c Release
```

Expected: `Build succeeded.` con 0 errores. Warnings esperados solo de paquetes con vulnerabilidades conocidas si las hay, pero TreatWarningsAsErrors está activo — si hay warnings de código, hay que arreglarlos.

- [ ] **Step 2: Ejecutar tests**

```cmd
dotnet test -c Release
```

Expected: `Passed!` con 28+ tests, 0 fallos.

- [ ] **Step 3: Publish single-file**

```cmd
dotnet publish src\AutolumoBarcodeScannerTool -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Expected: `AutolumoBarcodeScannerTool.exe` (~70-90 MB) y `appsettings.default.ini` en la carpeta publish.

- [ ] **Step 4: Arrancar el binario manualmente y verificar tray**

Doble-click al `.exe`. Debe aparecer ícono en la bandeja. Click derecho debe abrir menú con `Pausar / Configuración / Ver logs / Salir`.

- [ ] **Step 5: Verificar creación de config y log**

Tras primer arranque debe existir:

- `%LOCALAPPDATA%\AutolumoBarcodeScannerTool\appsettings.ini`
- `%LOCALAPPDATA%\AutolumoBarcodeScannerTool\logs\app-<fecha>.log`

- [ ] **Step 6: Ejecutar el checklist manual**

Seguir `docs/manual-test-checklist.md` y marcar cada item.

- [ ] **Step 7: Commit (si hubo fixes)**

```bash
git add .
git commit -m "fix: ajustes tras validación en Windows"
```

---

### Task 37: Build del instalador y smoke test 🪟

- [ ] **Step 1: Compilar el instalador**

```cmd
ISCC.exe installer\AutolumoBarcodeScannerTool.iss
```

Expected: `AutolumoBarcodeScannerTool-Setup-0.1.0.exe` en `artifacts\installer\`.

- [ ] **Step 2: Instalar en máquina limpia (o VM)**

Ejecutar el setup, elegir autostart, finalizar con "Ejecutar".

Expected: ícono en bandeja, `%PROGRAMFILES%\Autolumo\BarcodeScannerTool\AutolumoBarcodeScannerTool.exe` presente, entrada en `HKCU\...\Run`.

- [ ] **Step 3: Reiniciar y verificar autoarranque**

Cerrar sesión, volver a iniciar → ícono debe aparecer en la bandeja.

- [ ] **Step 4: Desinstalar**

`Configuración → Apps → Autolumo Barcode Scanner Tool → Desinstalar`. La entrada de `HKCU\Run` debe eliminarse.

---

## Brechas conocidas respecto al spec

Tres comportamientos descritos en el spec **no están cubiertos en este plan** porque añadirían 5-8 tareas más. Son trabajos de refinamiento que se hacen tras tener el flujo end-to-end funcionando. El plan actual deja al sistema en estado "funcional pero reactivo" — el usuario tiene que cerrar y reabrir la app si el lector se desconecta.

| Comportamiento del spec | Estado actual | Para implementarlo |
|---|---|---|
| Reconexión Serial con backoff exponencial 5s/10s/30s | `SerialInputSource` entra en estado `Error` y se queda allí; el usuario debe usar `Pausar`/`Reanudar` en el tray para reintentar | Añadir `Timer` en `SerialInputSource` que reintente `Open()` con backoff cuando state == Error |
| Reconexión HID vía `WM_DEVICECHANGE` | `HidKeyboardInputSource` no escucha cambios de dispositivo; si el lector se desenchufa hay que reiniciar la app | Suscribir `WM_DEVICECHANGE` en la ventana oculta de Raw Input y re-enumerar al recibir `DBT_DEVNODES_CHANGED` |
| Toggle `LogPayload=false` para no loguear el contenido escaneado | La opción está en `LoggingOptions` pero ningún componente la lee; siempre se loguea el payload | Inyectar `IOptions<LoggingOptions>` en `ForegroundProcessSink` y gatearlo en las llamadas a `_logger.LogInformation(...payload...)` |

Recomendación: ejecutar este plan completo primero, validar el flujo en Windows con el checklist manual, y después abrir un segundo plan corto que cubra estas tres brechas.

---

## Resumen de cobertura tests

- Modelos: `ScanEventTests` (2)
- Transforms: `ReplaceTerminatorTransformTests` (5)
- Configuration: `IniConfigWriterTests` (6)
- Sinks: `ForegroundProcessSinkTests` (7)
- Sources: `SerialInputSourceTests` (8)
- Orchestration: `ScannerOrchestratorTests` (4)

Total esperado: **32 tests** que corren en macOS sin requerir Windows.

Lo que NO está cubierto por tests automatizados (validar manualmente en Windows):
- `Win32ForegroundWindowProvider`
- `SendInputInjector`
- `AutostartManager`
- `HidDeviceEnumerator`
- `LowLevelKeyboardHook`
- `RawInputInterop` / `HidKeyboardInputSource`
- `TrayIconController` / `SettingsForm`
- Hot-reload de `IOptionsMonitor` end-to-end
- Boot Generic Host completo

Estos componentes son delgados wrappers de Win32 o WinForms y se validan vía checklist manual.
