# WinUI 3 UI Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Azookey's candidate window, ruby reading window, input mode indicator, and settings UI with C#/.NET WinUI 3 while preserving the current IME, server, launcher, installer, config file, and IPC contracts.

**Architecture:** Add new C# projects under `C:/Users/Takahiro/Documents/Azuki-Win/apps` and keep the existing Rust/Swift IME and server projects intact. `Azookey.UI` produces `ui.exe` and hosts the existing `window.WindowService` on the `azookey_ui` named pipe; `Azookey.Settings` produces `frontend.exe`, reads and writes `%APPDATA%/Azookey/settings.json`, and notifies the existing `azookey_server` pipe through `azookey.AzookeyService.UpdateConfig`.

**Tech Stack:** C# 13, .NET 10 SDK, Visual Studio Build Tools 2026 MSBuild, WinUI 3, Windows App SDK 2.2.0, Grpc.AspNetCore, protobuf-net.Grpc.Tools/Grpc.Tools, xUnit, Inno Setup.

## Global Constraints

- Repository root is `C:/Users/Takahiro/Documents/Azuki-Win`.
- Use Visual Studio Build Tools 2026 MSBuild at `C:/Program Files (x86)/Microsoft Visual Studio/18/BuildTools/MSBuild/Current/Bin/amd64/MSBuild.exe`.
- Install .NET SDK 10 if `dotnet --list-sdks` prints no `10.` SDK; the machine currently has only .NET 10 runtimes.
- Pin `Microsoft.WindowsAppSDK` to `2.2.0`, the stable release listed by Microsoft Learn on 2026-06-09.
- WinUI apps are unpackaged and set `<WindowsPackageType>None</WindowsPackageType>`.
- Publish `ui.exe` and `frontend.exe` as .NET self-contained `win-x64` apps.
- Windows App SDK Runtime remains a runtime prerequisite and is installed by the Inno installer.
- Preserve executable file names `ui.exe` and `frontend.exe`.
- Preserve named pipes `azookey_ui`, `azookey_server`, and `azookey_launcher`.
- Do not change `crates/shared/window.proto` or `crates/shared/service.proto` field names, field numbers, service names, or package names.
- Preserve `%APPDATA%/Azookey/settings.json` schema and `CONFIG_VERSION = "0.1.2"`.
- The final installer must not require WebView2 for the new WinUI settings UI or candidate UI.
- All production C# behavior starts with a failing xUnit test before implementation.

---

## File Structure

- Create `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.WinUI.sln`: C# solution for all new WinUI work.
- Create `C:/Users/Takahiro/Documents/Azuki-Win/apps/Directory.Build.props`: shared C# build defaults.
- Create `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Core`: config, filesystem, IPC helpers, update helpers, process helpers, and Win32 helpers shared by both apps.
- Create `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Core.Tests`: unit tests for config, update, capability, and server restart logic.
- Create `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Protos`: generated C# gRPC types from the existing Rust proto files.
- Create `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.UI`: WinUI `ui.exe` replacement for `crates/ui`.
- Create `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.UI.Tests`: unit tests for candidate/ruby positioning, state reducer, and gRPC action mapping.
- Create `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Settings`: WinUI `frontend.exe` replacement for `frontend`.
- Create `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Settings.Tests`: view model and service tests for settings workflows.
- Modify `C:/Users/Takahiro/Documents/Azuki-Win/Makefile.toml`: replace Tauri build with C# publish and copy both WinUI apps into `build`.
- Modify `C:/Users/Takahiro/Documents/Azuki-Win/installer/Installer.iss`: keep `frontend.exe` as `MainBinaryName`, remove WebView2 dependency calls, add Windows App SDK Runtime dependency, remove WebView2 cleanup.
- Modify `C:/Users/Takahiro/Documents/Azuki-Win/installer/CodeDependencies.iss`: add a Windows App SDK Runtime dependency helper.

---

### Task 1: Toolchain And Solution Skeleton

**Files:**
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.WinUI.sln`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Directory.Build.props`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Core/Azookey.Core.csproj`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Core.Tests/Azookey.Core.Tests.csproj`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Protos/Azookey.Protos.csproj`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.UI/Azookey.UI.csproj`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.UI.Tests/Azookey.UI.Tests.csproj`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Settings/Azookey.Settings.csproj`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Settings.Tests/Azookey.Settings.Tests.csproj`

**Interfaces:**
- Produces: `Azookey.Core`, `Azookey.Protos`, `Azookey.UI`, `Azookey.Settings` projects buildable by MSBuild.
- Produces: `ui.exe` from `Azookey.UI` and `frontend.exe` from `Azookey.Settings`.

- [ ] **Step 1: Verify the current toolchain gap**

Run:

```powershell
dotnet --list-sdks
dotnet --list-runtimes
& 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe' -latest -products * -property displayName
```

Expected before installation on this machine:

```text
Microsoft.NETCore.App 10.0.9 [...]
Microsoft.WindowsDesktop.App 10.0.9 [...]
Visual Studio Build Tools 2026
```

The SDK list may be empty. Continue to Step 2 if no `10.` SDK is printed.

- [ ] **Step 2: Install .NET SDK 10 when it is missing**

Run:

```powershell
winget install --id Microsoft.DotNet.SDK.10 --source winget --accept-source-agreements --accept-package-agreements
```

Expected:

```text
Successfully installed
```

Then verify:

```powershell
dotnet --list-sdks
```

Expected:

```text
10.x.x [C:\Program Files\dotnet\sdk]
```

- [ ] **Step 3: Create the shared build props**

Create `apps/Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <LangVersion>preview</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <PlatformTarget>x64</PlatformTarget>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  </PropertyGroup>
</Project>
```

- [ ] **Step 4: Create the project files**

Create `apps/Azookey.Core/Azookey.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Google.Protobuf" Version="3.33.2" />
    <PackageReference Include="Grpc.Net.Client" Version="2.71.0" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.0" />
    <PackageReference Include="System.IO.Pipelines" Version="10.0.0" />
    <PackageReference Include="System.Management" Version="10.0.0" />
  </ItemGroup>
</Project>
```

Create `apps/Azookey.Protos/Azookey.Protos.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Google.Protobuf" Version="3.33.2" />
    <PackageReference Include="Grpc.Tools" Version="2.76.0" PrivateAssets="all" />
    <PackageReference Include="Grpc.Net.Client" Version="2.71.0" />
    <PackageReference Include="Grpc.AspNetCore" Version="2.71.0" />
    <Protobuf Include="../../crates/shared/window.proto" Link="Protos/window.proto" GrpcServices="Both" />
    <Protobuf Include="../../crates/shared/service.proto" Link="Protos/service.proto" GrpcServices="Client" />
  </ItemGroup>
</Project>
```

Create `apps/Azookey.UI/Azookey.UI.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.19041.0</TargetPlatformMinVersion>
    <RootNamespace>Azookey.UI</RootNamespace>
    <AssemblyName>ui</AssemblyName>
    <UseWinUI>true</UseWinUI>
    <WindowsPackageType>None</WindowsPackageType>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>false</PublishSingleFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="2.2.0" />
    <PackageReference Include="Grpc.AspNetCore" Version="2.71.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.0" />
    <ProjectReference Include="../Azookey.Core/Azookey.Core.csproj" />
    <ProjectReference Include="../Azookey.Protos/Azookey.Protos.csproj" />
  </ItemGroup>
</Project>
```

Create `apps/Azookey.Settings/Azookey.Settings.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.19041.0</TargetPlatformMinVersion>
    <RootNamespace>Azookey.Settings</RootNamespace>
    <AssemblyName>frontend</AssemblyName>
    <UseWinUI>true</UseWinUI>
    <WindowsPackageType>None</WindowsPackageType>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>false</PublishSingleFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="2.2.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.0" />
    <ProjectReference Include="../Azookey.Core/Azookey.Core.csproj" />
    <ProjectReference Include="../Azookey.Protos/Azookey.Protos.csproj" />
  </ItemGroup>
</Project>
```

Create each test project with this shape, replacing the production reference:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" PrivateAssets="all" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../Azookey.Core/Azookey.Core.csproj" />
  </ItemGroup>
</Project>
```

For `Azookey.UI.Tests`, reference `../Azookey.UI/Azookey.UI.csproj`. For `Azookey.Settings.Tests`, reference `../Azookey.Settings/Azookey.Settings.csproj`.

- [ ] **Step 5: Create the solution and add projects**

Run:

```powershell
cd C:\Users\Takahiro\Documents\Azuki-Win
dotnet new sln -n Azookey.WinUI -o apps
dotnet sln apps\Azookey.WinUI.sln add apps\Azookey.Core\Azookey.Core.csproj
dotnet sln apps\Azookey.WinUI.sln add apps\Azookey.Core.Tests\Azookey.Core.Tests.csproj
dotnet sln apps\Azookey.WinUI.sln add apps\Azookey.Protos\Azookey.Protos.csproj
dotnet sln apps\Azookey.WinUI.sln add apps\Azookey.UI\Azookey.UI.csproj
dotnet sln apps\Azookey.WinUI.sln add apps\Azookey.UI.Tests\Azookey.UI.Tests.csproj
dotnet sln apps\Azookey.WinUI.sln add apps\Azookey.Settings\Azookey.Settings.csproj
dotnet sln apps\Azookey.WinUI.sln add apps\Azookey.Settings.Tests\Azookey.Settings.Tests.csproj
```

Expected:

```text
Project ... added to the solution.
```

- [ ] **Step 6: Build with Visual Studio Build Tools 2026 MSBuild**

Run:

```powershell
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe'
& $msbuild C:\Users\Takahiro\Documents\Azuki-Win\apps\Azookey.WinUI.sln /restore /p:Configuration=Debug /p:Platform=x64
```

Expected after adding minimal source files in later tasks:

```text
Build succeeded.
```

- [ ] **Step 7: Commit**

```powershell
git add apps
git commit -m "build: add WinUI solution skeleton"
```

---

### Task 2: Core Config Models And JSON Compatibility

**Files:**
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Core/Config/AppConfig.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Core/Config/JsonOptions.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Core/Config/DefaultRomajiTable.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Core.Tests/Config/AppConfigTests.cs`
- Add as linked content: `C:/Users/Takahiro/Documents/Azuki-Win/crates/shared/src/default_romaji_table.txt`

**Interfaces:**
- Produces: `Azookey.Core.Config.AppConfig.CreateDefault()`.
- Produces: `Azookey.Core.Config.AzookeyJson.Options`.
- Produces: `Azookey.Core.Config.DefaultRomajiTable.Load()`.

- [ ] **Step 1: Write failing default compatibility tests**

Create `apps/Azookey.Core.Tests/Config/AppConfigTests.cs`:

```csharp
using System.Text.Json;
using Azookey.Core.Config;

namespace Azookey.Core.Tests.Config;

public sealed class AppConfigTests
{
    [Fact]
    public void DefaultConfigMatchesRustDefaults()
    {
        AppConfig config = AppConfig.CreateDefault();

        Assert.Equal("0.1.2", config.Version);
        Assert.False(config.General.PunctuationCommit);
        Assert.True(config.General.PunctuationCommitPunctuation);
        Assert.True(config.General.PunctuationCommitExclamation);
        Assert.True(config.General.PunctuationCommitQuestion);
        Assert.False(config.General.ShowCandidateWindowAfterSpace);
        Assert.True(config.General.ShowLiveConversionReading);
        Assert.Equal(4, config.General.LiveConversionReadingVerticalAdjustment);
        Assert.Equal(NumpadInputMode.DirectInput, config.General.NumpadInput);
        Assert.Equal(SpaceInputMode.AlwaysHalf, config.General.SpaceInput);
        Assert.True(config.Shortcuts.CtrlSpaceToggle);
        Assert.True(config.Shortcuts.AltBackquoteToggle);
        Assert.False(config.Shortcuts.EisuToggle);
        Assert.False(config.Zenzai.Enable);
        Assert.Equal("", config.Zenzai.Profile);
        Assert.Equal("cpu", config.Zenzai.Backend);
        Assert.False(config.Debug.ServerLogEnabled);
        Assert.Equal("warn", config.Debug.ServerLogLevel);
        Assert.True(config.Debug.ServerCrashTraceEnabled);
        Assert.Empty(config.UserDictionary.Entries);
        Assert.True(config.RomajiTable.Rows.Count > 100);
    }

    [Fact]
    public void JsonUsesSnakeCaseEnumValuesAndPropertyNames()
    {
        AppConfig config = AppConfig.CreateDefault();
        string json = JsonSerializer.Serialize(config, AzookeyJson.Options);

        Assert.Contains("\"punctuation_style\"", json);
        Assert.Contains("\"numpad_input\": \"direct_input\"", json);
        Assert.Contains("\"space_input\": \"always_half\"", json);
        Assert.DoesNotContain("PunctuationStyle", json);
    }

    [Fact]
    public void SpaceInputAcceptsLegacyAlwaysFullAlias()
    {
        const string json = """
        {
          "version": "0.1.2",
          "zenzai": { "enable": false, "profile": "", "backend": "cpu" },
          "general": { "space_input": "always_full" }
        }
        """;

        AppConfig config = JsonSerializer.Deserialize<AppConfig>(json, AzookeyJson.Options)!;

        Assert.Equal(SpaceInputMode.FollowInputMode, config.General.SpaceInput);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test C:\Users\Takahiro\Documents\Azuki-Win\apps\Azookey.Core.Tests\Azookey.Core.Tests.csproj --filter AppConfigTests
```

Expected:

```text
The type or namespace name 'Config' does not exist
```

- [ ] **Step 3: Implement JSON options and config models**

Create `apps/Azookey.Core/Config/JsonOptions.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azookey.Core.Config;

public static class AzookeyJson
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
        options.Converters.Add(new JsonStringEnumMemberConverter());
        return options;
    }
}

public sealed class JsonStringEnumMemberConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        Type converterType = typeof(JsonStringEnumMemberConverterInner<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}
```

Create enum converter inner class in the same file:

```csharp
using System.Reflection;
using System.Runtime.Serialization;

namespace Azookey.Core.Config;

internal sealed class JsonStringEnumMemberConverterInner<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private static readonly Dictionary<string, TEnum> FromJson = BuildFromJson();
    private static readonly Dictionary<TEnum, string> ToJson = BuildToJson();

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? value = reader.GetString();
        if (value is not null && FromJson.TryGetValue(value, out TEnum result))
        {
            return result;
        }
        throw new JsonException($"Unknown {typeof(TEnum).Name} value '{value}'.");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(ToJson[value]);
    }

    private static Dictionary<string, TEnum> BuildFromJson()
    {
        var map = new Dictionary<string, TEnum>(StringComparer.Ordinal);
        foreach (TEnum value in Enum.GetValues<TEnum>())
        {
            string name = ToSnakeCase(value.ToString());
            map[name] = value;
            EnumMemberAttribute? attribute = typeof(TEnum).GetMember(value.ToString())[0].GetCustomAttribute<EnumMemberAttribute>();
            if (!string.IsNullOrWhiteSpace(attribute?.Value))
            {
                map[attribute.Value] = value;
            }
        }
        return map;
    }

    private static Dictionary<TEnum, string> BuildToJson()
    {
        return Enum.GetValues<TEnum>().ToDictionary(value => value, value => ToSnakeCase(value.ToString()));
    }

    private static string ToSnakeCase(string value)
    {
        var chars = new List<char>(value.Length + 8);
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (char.IsUpper(current) && index > 0)
            {
                chars.Add('_');
            }
            chars.Add(char.ToLowerInvariant(current));
        }
        return new string(chars.ToArray());
    }
}
```

Create `apps/Azookey.Core/Config/AppConfig.cs`:

```csharp
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Azookey.Core.Config;

public enum WidthMode { Half, Full }
public enum PunctuationStyle { ToutenKuten, FullwidthCommaFullwidthPeriod, ToutenFullwidthPeriod, FullwidthCommaKuten }
public enum SymbolStyle { CornerBracketMiddleDot, SquareBracketBackslash, CornerBracketBackslash, SquareBracketMiddleDot }
public enum SpaceInputMode { AlwaysHalf, [EnumMember(Value = "always_full")] FollowInputMode }
public enum NumpadInputMode { DirectInput, AlwaysHalf, FollowInputMode }

public sealed record CharacterWidthGroups
{
    public WidthMode Alphabet { get; init; } = WidthMode.Half;
    public WidthMode Number { get; init; } = WidthMode.Half;
    public WidthMode Bracket { get; init; } = WidthMode.Full;
    public WidthMode CommaPeriod { get; init; } = WidthMode.Full;
    public WidthMode MiddleDotCornerBracket { get; init; } = WidthMode.Full;
    public WidthMode Quote { get; init; } = WidthMode.Full;
    public WidthMode ColonSemicolon { get; init; } = WidthMode.Full;
    public WidthMode HashGroup { get; init; } = WidthMode.Half;
    public WidthMode Tilde { get; init; } = WidthMode.Full;
    public WidthMode MathSymbol { get; init; } = WidthMode.Full;
    public WidthMode QuestionExclamation { get; init; } = WidthMode.Full;
}

public sealed record GeneralConfig
{
    public PunctuationStyle PunctuationStyle { get; init; } = PunctuationStyle.ToutenKuten;
    public SymbolStyle SymbolStyle { get; init; } = SymbolStyle.CornerBracketMiddleDot;
    public SpaceInputMode SpaceInput { get; init; } = SpaceInputMode.AlwaysHalf;
    public NumpadInputMode NumpadInput { get; init; } = NumpadInputMode.DirectInput;
    public bool PunctuationCommit { get; init; }
    public bool PunctuationCommitPunctuation { get; init; } = true;
    public bool PunctuationCommitExclamation { get; init; } = true;
    public bool PunctuationCommitQuestion { get; init; } = true;
    public bool ShowCandidateWindowAfterSpace { get; init; }
    public bool ShowLiveConversionReading { get; init; } = true;
    public int LiveConversionReadingVerticalAdjustment { get; init; } = 4;
}

public sealed record RomajiRule
{
    public string Input { get; init; } = "";
    public string Output { get; init; } = "";
    public string NextInput { get; init; } = "";
}

public sealed record RomajiTableConfig
{
    public List<RomajiRule> Rows { get; init; } = DefaultRomajiTable.Load();
}

public sealed record ZenzaiConfig
{
    public bool Enable { get; init; }
    public string Profile { get; init; } = "";
    public string Backend { get; init; } = "cpu";
}

public sealed record ShortcutConfig
{
    public bool CtrlSpaceToggle { get; init; } = true;
    public bool AltBackquoteToggle { get; init; } = true;
    public bool EisuToggle { get; init; }
}

public sealed record DebugConfig
{
    public bool ServerLogEnabled { get; init; }
    public string ServerLogLevel { get; init; } = "warn";
    public bool ServerCrashTraceEnabled { get; init; } = true;
}

public sealed record CharacterWidthConfig
{
    public Dictionary<string, bool> SymbolFullwidth { get; init; } = CharacterWidthDefaults.CreateSymbolMap();
    public CharacterWidthGroups Groups { get; init; } = new();
}

public sealed record UserDictionaryEntry
{
    public string Reading { get; init; } = "";
    public string Word { get; init; } = "";
}

public sealed record UserDictionaryConfig
{
    public List<UserDictionaryEntry> Entries { get; init; } = [];
}

public sealed record AppConfig
{
    public const string ConfigVersion = "0.1.2";
    public string Version { get; init; } = ConfigVersion;
    public DebugConfig Debug { get; init; } = new();
    public ZenzaiConfig Zenzai { get; init; } = new();
    public ShortcutConfig Shortcuts { get; init; } = new();
    public GeneralConfig General { get; init; } = new();
    public RomajiTableConfig RomajiTable { get; init; } = new();
    public CharacterWidthConfig CharacterWidth { get; init; } = new();
    public UserDictionaryConfig UserDictionary { get; init; } = new();

    public static AppConfig CreateDefault() => new();
}
```

Create `apps/Azookey.Core/Config/DefaultRomajiTable.cs`:

```csharp
namespace Azookey.Core.Config;

public static class DefaultRomajiTable
{
    public static List<RomajiRule> Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "default_romaji_table.txt");
        if (!File.Exists(path))
        {
            path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "crates", "shared", "src", "default_romaji_table.txt"));
        }

        return File.ReadLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(Parse)
            .ToList();
    }

    private static RomajiRule Parse(string line)
    {
        string[] parts = line.Split('\t');
        return new RomajiRule
        {
            Input = parts[0].Trim(),
            Output = parts[1].Trim(),
            NextInput = parts.Length > 2 ? parts[2].Trim() : ""
        };
    }
}

public static class CharacterWidthDefaults
{
    private static readonly (string Symbol, bool IsFullwidth)[] Symbols =
    [
        ("0", false), ("1", false), ("2", false), ("3", false), ("4", false), ("5", false),
        ("6", false), ("7", false), ("8", false), ("9", false), ("!", true), ("\"", true),
        ("#", false), ("$", false), ("%", false), ("&", false), ("'", true), ("(", true),
        (")", true), ("*", true), ("+", true), (",", true), ("-", true), (".", true),
        ("/", true), (":", true), (";", true), ("<", true), ("=", true), (">", true),
        ("?", true), ("@", false), ("[", true), ("\\", false), ("]", true), ("^", false),
        ("_", false), ("`", false), ("{", true), ("|", false), ("}", true), ("~", true)
    ];

    public static Dictionary<string, bool> CreateSymbolMap()
    {
        return Symbols.ToDictionary(pair => pair.Symbol, pair => pair.IsFullwidth, StringComparer.Ordinal);
    }
}
```

Add this item to `apps/Azookey.Core/Azookey.Core.csproj`:

```xml
<ItemGroup>
  <Content Include="../../crates/shared/src/default_romaji_table.txt" Link="default_romaji_table.txt" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

- [ ] **Step 4: Run tests to verify they pass**

Run:

```powershell
dotnet test C:\Users\Takahiro\Documents\Azuki-Win\apps\Azookey.Core.Tests\Azookey.Core.Tests.csproj --filter AppConfigTests
```

Expected:

```text
Passed!  - Failed: 0
```

- [ ] **Step 5: Commit**

```powershell
git add apps/Azookey.Core apps/Azookey.Core.Tests
git commit -m "feat: port settings config schema to C#"
```

---

### Task 3: Config Store, Recovery, Migration, And Atomic Save

**Files:**
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Core/Config/ConfigStore.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Core.Tests/Config/ConfigStoreTests.cs`

**Interfaces:**
- Consumes: `AppConfig`, `AzookeyJson.Options`.
- Produces: `ConfigStore.LoadWithRecovery()`, `ConfigStore.Write(AppConfig config)`.
- Produces: `ConfigLoadResult(AppConfig Config, ConfigRecovery? Recovery, Exception? RewriteError)`.

- [ ] **Step 1: Write failing store tests**

Create `apps/Azookey.Core.Tests/Config/ConfigStoreTests.cs`:

```csharp
using System.Text.Json;
using Azookey.Core.Config;

namespace Azookey.Core.Tests.Config;

public sealed class ConfigStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "azookey-config-tests", Guid.NewGuid().ToString("N"));

    public void Dispose() => Directory.Delete(root, true);

    [Fact]
    public void MissingSettingsCreatesDefaultSettings()
    {
        var store = new ConfigStore(Path.Combine(root, "Azookey"));

        ConfigLoadResult result = store.LoadWithRecovery();

        Assert.Null(result.Recovery);
        Assert.Equal("0.1.2", result.Config.Version);
        Assert.True(File.Exists(Path.Combine(root, "Azookey", "settings.json")));
    }

    [Fact]
    public void CorruptedSettingsAreBackedUpAndReplaced()
    {
        string configRoot = Path.Combine(root, "Azookey");
        Directory.CreateDirectory(configRoot);
        string settings = Path.Combine(configRoot, "settings.json");
        File.WriteAllText(settings, "{not valid json");

        ConfigLoadResult result = new ConfigStore(configRoot).LoadWithRecovery();

        Assert.NotNull(result.Recovery);
        Assert.StartsWith("settings.json.broken-", Path.GetFileName(result.Recovery!.BackupPath));
        Assert.Equal("{not valid json", File.ReadAllText(result.Recovery.BackupPath));
        Assert.Equal("0.1.2", result.Config.Version);
    }

    [Fact]
    public void LegacyNumpadInputMigratesToCurrentMeaning()
    {
        string configRoot = Path.Combine(root, "Azookey");
        Directory.CreateDirectory(configRoot);
        AppConfig legacy = AppConfig.CreateDefault() with
        {
            Version = "0.1.1",
            General = AppConfig.CreateDefault().General with { NumpadInput = NumpadInputMode.AlwaysHalf }
        };
        File.WriteAllText(Path.Combine(configRoot, "settings.json"), JsonSerializer.Serialize(legacy, AzookeyJson.Options));

        ConfigLoadResult result = new ConfigStore(configRoot).LoadWithRecovery();

        Assert.Equal("0.1.2", result.Config.Version);
        Assert.Equal(NumpadInputMode.DirectInput, result.Config.General.NumpadInput);
    }

    [Fact]
    public void WriteLeavesNoTempFile()
    {
        string configRoot = Path.Combine(root, "Azookey");
        var store = new ConfigStore(configRoot);
        AppConfig config = AppConfig.CreateDefault() with { Zenzai = new ZenzaiConfig { Enable = true, Backend = "vulkan", Profile = "" } };

        store.Write(config);

        AppConfig saved = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(Path.Combine(configRoot, "settings.json")), AzookeyJson.Options)!;
        Assert.True(saved.Zenzai.Enable);
        Assert.Empty(Directory.EnumerateFiles(configRoot, "settings.json.tmp-*"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test C:\Users\Takahiro\Documents\Azuki-Win\apps\Azookey.Core.Tests\Azookey.Core.Tests.csproj --filter ConfigStoreTests
```

Expected:

```text
The type or namespace name 'ConfigStore' could not be found
```

- [ ] **Step 3: Implement ConfigStore**

Create `apps/Azookey.Core/Config/ConfigStore.cs`:

```csharp
using System.Text.Json;

namespace Azookey.Core.Config;

public sealed record ConfigRecovery(string OriginalPath, string BackupPath);
public sealed record ConfigLoadResult(AppConfig Config, ConfigRecovery? Recovery, Exception? RewriteError);

public sealed class ConfigStore
{
    public const string SettingsFileName = "settings.json";
    private readonly string configRoot;

    public ConfigStore(string configRoot) => this.configRoot = configRoot;

    public static ConfigStore FromAppData()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            throw new InvalidOperationException("APPDATA is not set");
        }
        return new ConfigStore(Path.Combine(appData, "Azookey"));
    }

    public ConfigLoadResult LoadWithRecovery()
    {
        Directory.CreateDirectory(configRoot);
        string configPath = Path.Combine(configRoot, SettingsFileName);
        AppConfig config;
        ConfigRecovery? recovery = null;

        if (!File.Exists(configPath))
        {
            config = AppConfig.CreateDefault();
        }
        else
        {
            try
            {
                config = ParseAndMigrate(File.ReadAllText(configPath));
            }
            catch (JsonException)
            {
                string backupPath = BackupCorruptedConfig(configPath);
                recovery = new ConfigRecovery(configPath, backupPath);
                config = AppConfig.CreateDefault();
            }
        }

        Exception? rewriteError = null;
        try
        {
            Write(config);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            rewriteError = error;
        }

        return new ConfigLoadResult(config, recovery, rewriteError);
    }

    public void Write(AppConfig config)
    {
        Directory.CreateDirectory(configRoot);
        string configPath = Path.Combine(configRoot, SettingsFileName);
        string tempPath = Path.Combine(configRoot, $"{SettingsFileName}.tmp-{Environment.ProcessId}-{DateTime.Now:yyyyMMddHHmmssffffff}");
        File.WriteAllText(tempPath, JsonSerializer.Serialize(config, AzookeyJson.Options));
        File.Move(tempPath, configPath, overwrite: true);
    }

    private static AppConfig ParseAndMigrate(string json)
    {
        AppConfig config = JsonSerializer.Deserialize<AppConfig>(json, AzookeyJson.Options)!;
        if (config.Version == AppConfig.ConfigVersion)
        {
            return config;
        }

        NumpadInputMode migratedNumpad = config.General.NumpadInput switch
        {
            NumpadInputMode.AlwaysHalf => NumpadInputMode.DirectInput,
            NumpadInputMode.FollowInputMode => NumpadInputMode.AlwaysHalf,
            _ => NumpadInputMode.DirectInput
        };

        List<RomajiRule> rows = config.RomajiTable.Rows
            .Where(row => !IsLegacyRemovedDefaultRow(row))
            .ToList();

        return config with
        {
            Version = AppConfig.ConfigVersion,
            General = config.General with { NumpadInput = migratedNumpad },
            RomajiTable = config.RomajiTable with { Rows = rows }
        };
    }

    private static bool IsLegacyRemovedDefaultRow(RomajiRule row)
    {
        return row.NextInput.Length == 0 && (row.Input, row.Output) is
            ("~", "〜") or (".", "。") or (",", "、") or ("[", "「") or ("]", "」");
    }

    private static string BackupCorruptedConfig(string configPath)
    {
        string parent = Path.GetDirectoryName(configPath)!;
        string baseName = $"{SettingsFileName}.broken-{DateTime.Now:yyyyMMddHHmmss}";
        for (int index = 0; index < 1000; index++)
        {
            string suffix = index == 0 ? "" : $"-{index}";
            string candidate = Path.Combine(parent, baseName + suffix);
            if (File.Exists(candidate))
            {
                continue;
            }
            File.Move(configPath, candidate);
            return candidate;
        }
        string overflow = Path.Combine(parent, baseName + "-overflow");
        File.Move(configPath, overflow);
        return overflow;
    }
}
```

- [ ] **Step 4: Run tests**

Run:

```powershell
dotnet test C:\Users\Takahiro\Documents\Azuki-Win\apps\Azookey.Core.Tests\Azookey.Core.Tests.csproj --filter "AppConfigTests|ConfigStoreTests"
```

Expected:

```text
Passed!  - Failed: 0
```

- [ ] **Step 5: Commit**

```powershell
git add apps/Azookey.Core apps/Azookey.Core.Tests
git commit -m "feat: port settings storage and migration"
```

---

### Task 4: Protobuf And Named Pipe IPC

**Files:**
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Core/Ipc/NamedPipeGrpcClientFactory.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.UI/Ipc/WindowAction.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.UI/Ipc/WindowServiceImpl.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.UI.Tests/Ipc/WindowServiceImplTests.cs`

**Interfaces:**
- Consumes: generated `Window.WindowService.WindowServiceBase`.
- Produces: `WindowAction` records matching current Rust `WindowAction`.
- Produces: `NamedPipeGrpcClientFactory.CreateChannel(string pipeName)`.

- [ ] **Step 1: Write failing IPC mapping tests**

Create `apps/Azookey.UI.Tests/Ipc/WindowServiceImplTests.cs`:

```csharp
using Azookey.UI.Ipc;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Window;

namespace Azookey.UI.Tests.Ipc;

public sealed class WindowServiceImplTests
{
    [Fact]
    public async Task SetWindowPositionWithoutPositionReturnsInvalidArgument()
    {
        var sink = new TestSink();
        var service = new WindowServiceImpl(sink);

        RpcException error = await Assert.ThrowsAsync<RpcException>(() =>
            service.SetWindowPosition(new SetPositionRequest(), TestServerCallContext.Create()));

        Assert.Equal(StatusCode.InvalidArgument, error.StatusCode);
    }

    [Fact]
    public async Task UpdateCandidateWindowSendsBatchedAction()
    {
        var sink = new TestSink();
        var service = new WindowServiceImpl(sink);
        var request = new UpdateCandidateWindowRequest
        {
            Visible = true,
            Position = new WindowPosition { Top = 1, Left = 2, Bottom = 3, Right = 4 },
            Candidates = new CandidateList { Candidates = { "候補" } },
            SelectedIndex = 0,
            InputMode = "あ",
            Reading = "こうほ",
            CandidateListVisible = true,
            ReadingVerticalAdjustment = 4
        };

        await service.UpdateCandidateWindow(request, TestServerCallContext.Create());

        var action = Assert.IsType<WindowAction.UpdateCandidateWindow>(Assert.Single(sink.Actions));
        Assert.True(action.Visible);
        Assert.Equal(new WindowRect(1, 2, 3, 4), action.Position);
        Assert.Equal(new[] { "候補" }, action.Candidates);
        Assert.Equal(0, action.SelectedIndex);
        Assert.Equal("あ", action.InputMode);
        Assert.Equal("こうほ", action.Reading);
        Assert.True(action.CandidateListVisible);
        Assert.Equal(4, action.ReadingVerticalAdjustment);
    }

    private sealed class TestSink : IWindowActionSink
    {
        public List<WindowAction> Actions { get; } = [];
        public ValueTask SendAsync(WindowAction action, CancellationToken cancellationToken)
        {
            Actions.Add(action);
            return ValueTask.CompletedTask;
        }
    }
}
```

Create a tiny test call context helper in the same file:

```csharp
internal sealed class TestServerCallContext : ServerCallContext
{
    public static ServerCallContext Create() => new TestServerCallContext();
    protected override string MethodCore => "test";
    protected override string HostCore => "localhost";
    protected override string PeerCore => "pipe";
    protected override DateTime DeadlineCore => DateTime.MaxValue;
    protected override Metadata RequestHeadersCore => [];
    protected override CancellationToken CancellationTokenCore => CancellationToken.None;
    protected override Metadata ResponseTrailersCore => [];
    protected override Status StatusCore { get; set; }
    protected override WriteOptions? WriteOptionsCore { get; set; }
    protected override AuthContext AuthContextCore => new("anonymous", []);
    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) => throw new NotSupportedException();
    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test C:\Users\Takahiro\Documents\Azuki-Win\apps\Azookey.UI.Tests\Azookey.UI.Tests.csproj --filter WindowServiceImplTests
```

Expected:

```text
The type or namespace name 'WindowServiceImpl' could not be found
```

- [ ] **Step 3: Implement action records and service**

Create `apps/Azookey.UI/Ipc/WindowAction.cs`:

```csharp
namespace Azookey.UI.Ipc;

public readonly record struct WindowRect(int Top, int Left, int Bottom, int Right);

public abstract record WindowAction
{
    public sealed record Show : WindowAction;
    public sealed record Hide : WindowAction;
    public sealed record SetPosition(WindowRect Position) : WindowAction;
    public sealed record SetSelection(int Index) : WindowAction;
    public sealed record SetCandidate(IReadOnlyList<string> Candidates) : WindowAction;
    public sealed record SetInputMode(string Mode) : WindowAction;
    public sealed record UpdateCandidateWindow(
        bool? Visible,
        WindowRect? Position,
        IReadOnlyList<string>? Candidates,
        int? SelectedIndex,
        string? InputMode,
        string? Reading,
        bool? CandidateListVisible,
        int? ReadingVerticalAdjustment) : WindowAction;
}

public interface IWindowActionSink
{
    ValueTask SendAsync(WindowAction action, CancellationToken cancellationToken);
}
```

Create `apps/Azookey.UI/Ipc/WindowServiceImpl.cs`:

```csharp
using Grpc.Core;
using Window;

namespace Azookey.UI.Ipc;

public sealed class WindowServiceImpl : WindowService.WindowServiceBase
{
    private readonly IWindowActionSink sink;

    public WindowServiceImpl(IWindowActionSink sink) => this.sink = sink;

    public override Task<EmptyResponse> ShowWindow(EmptyResponse request, ServerCallContext context)
        => Send(new WindowAction.Show(), context.CancellationToken);

    public override Task<EmptyResponse> HideWindow(EmptyResponse request, ServerCallContext context)
        => Send(new WindowAction.Hide(), context.CancellationToken);

    public override Task<EmptyResponse> SetWindowPosition(SetPositionRequest request, ServerCallContext context)
    {
        if (request.Position is null)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "position is required"));
        }
        return Send(new WindowAction.SetPosition(ToRect(request.Position)), context.CancellationToken);
    }

    public override Task<EmptyResponse> SetCandidate(SetCandidateRequest request, ServerCallContext context)
        => Send(new WindowAction.SetCandidate(request.Candidates.ToArray()), context.CancellationToken);

    public override Task<EmptyResponse> SetSelection(SetSelectionRequest request, ServerCallContext context)
        => Send(new WindowAction.SetSelection(request.Index), context.CancellationToken);

    public override Task<EmptyResponse> SetInputMode(SetInputModeRequest request, ServerCallContext context)
        => Send(new WindowAction.SetInputMode(request.Mode), context.CancellationToken);

    public override Task<EmptyResponse> UpdateCandidateWindow(UpdateCandidateWindowRequest request, ServerCallContext context)
    {
        WindowRect? position = request.Position is null ? null : ToRect(request.Position);
        IReadOnlyList<string>? candidates = request.Candidates is null ? null : request.Candidates.Candidates.ToArray();
        return Send(new WindowAction.UpdateCandidateWindow(
            request.HasVisible ? request.Visible : null,
            position,
            candidates,
            request.HasSelectedIndex ? request.SelectedIndex : null,
            request.HasInputMode ? request.InputMode : null,
            request.HasReading ? request.Reading : null,
            request.HasCandidateListVisible ? request.CandidateListVisible : null,
            request.HasReadingVerticalAdjustment ? request.ReadingVerticalAdjustment : null), context.CancellationToken);
    }

    private async Task<EmptyResponse> Send(WindowAction action, CancellationToken cancellationToken)
    {
        await sink.SendAsync(action, cancellationToken);
        return new EmptyResponse();
    }

    private static WindowRect ToRect(WindowPosition position)
        => new(position.Top, position.Left, position.Bottom, position.Right);
}
```

- [ ] **Step 4: Add named pipe client factory**

Create `apps/Azookey.Core/Ipc/NamedPipeGrpcClientFactory.cs`:

```csharp
using System.IO.Pipes;
using Grpc.Net.Client;
using Microsoft.Win32.SafeHandles;

namespace Azookey.Core.Ipc;

public static class NamedPipeGrpcClientFactory
{
    public static GrpcChannel CreateChannel(string pipeName, TimeSpan? timeout = null)
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (_, cancellationToken) =>
            {
                var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(timeout ?? TimeSpan.FromSeconds(3));
                await pipe.ConnectAsync(cts.Token);
                return pipe;
            }
        };
        return GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions { HttpHandler = handler });
    }
}
```

- [ ] **Step 5: Run tests**

Run:

```powershell
dotnet test C:\Users\Takahiro\Documents\Azuki-Win\apps\Azookey.UI.Tests\Azookey.UI.Tests.csproj --filter WindowServiceImplTests
```

Expected:

```text
Passed!  - Failed: 0
```

- [ ] **Step 6: Commit**

```powershell
git add apps/Azookey.Core apps/Azookey.UI apps/Azookey.UI.Tests apps/Azookey.Protos
git commit -m "feat: add C# gRPC IPC contracts"
```

---

### Task 5: Candidate UI State Reducer And Positioning

**Files:**
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.UI/Candidate/WindowGeometry.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.UI/Candidate/CandidateState.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.UI.Tests/Candidate/WindowGeometryTests.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.UI.Tests/Candidate/CandidateStateTests.cs`

**Interfaces:**
- Produces: `WindowGeometry.CandidateWindowPosition`, `WindowGeometry.CandidateWindowPositionWithRubyClearance`, `WindowGeometry.RubyWindowPosition`, `WindowGeometry.RubyWindowSizeForWorkArea`.
- Produces: immutable `CandidateState Apply(WindowAction action)`.

- [ ] **Step 1: Write failing geometry tests copied from Rust expectations**

Create `apps/Azookey.UI.Tests/Candidate/WindowGeometryTests.cs`:

```csharp
using Azookey.UI.Candidate;
using Azookey.UI.Ipc;

namespace Azookey.UI.Tests.Candidate;

public sealed class WindowGeometryTests
{
    private static WorkArea WorkArea() => new(0, 0, 800, 600);

    [Fact] public void PlacesWindowBelowWhenThereIsRoom()
        => Assert.Equal((85, 126), WindowGeometry.CandidateWindowPosition(new WindowRect(100, 100, 120, 180), new WindowSize(240, 120), WorkArea()));

    [Fact] public void PlacesWindowAboveNearBottomEdge()
        => Assert.Equal((85, 434), WindowGeometry.CandidateWindowPosition(new WindowRect(560, 100, 580, 180), new WindowSize(240, 120), WorkArea()));

    [Fact] public void ClampsWindowToRightEdge()
        => Assert.Equal((560, 126), WindowGeometry.CandidateWindowPosition(new WindowRect(100, 760, 120, 780), new WindowSize(240, 120), WorkArea()));

    [Fact] public void PlacesRubyWindowAboveInputCentered()
        => Assert.Equal((100, 60), WindowGeometry.RubyWindowPosition(new WindowRect(100, 100, 120, 180), new WindowSize(80, 48), WorkArea(), 0));

    [Fact] public void PositiveRubyAdjustmentMovesWindowUp()
        => Assert.Equal((100, 52), WindowGeometry.RubyWindowPosition(new WindowRect(100, 100, 120, 180), new WindowSize(80, 48), WorkArea(), 8));

    [Fact] public void KeepsCandidateWindowBelowRubyWhenRubyFallsBackUnderInput()
        => Assert.Equal((85, 92), WindowGeometry.CandidateWindowPositionWithRubyClearance(new WindowRect(20, 100, 40, 180), new WindowSize(240, 120), new WindowSize(80, 48), WorkArea(), 0));

    [Fact] public void ClampsRubyWidthUsingMonitorScaleFactor()
        => Assert.Equal(new RubyWindowSize(400, 39), WindowGeometry.RubyWindowSizeForWorkArea(1000, 39, WorkArea(), 2));
}
```

- [ ] **Step 2: Write failing state reducer tests**

Create `apps/Azookey.UI.Tests/Candidate/CandidateStateTests.cs`:

```csharp
using Azookey.UI.Candidate;
using Azookey.UI.Ipc;

namespace Azookey.UI.Tests.Candidate;

public sealed class CandidateStateTests
{
    [Fact]
    public void UpdateCandidateWindowMergesOnlyProvidedFields()
    {
        CandidateState state = CandidateState.Initial
            .Apply(new WindowAction.SetCandidate(["one", "two"]))
            .Apply(new WindowAction.SetSelection(1));

        CandidateState updated = state.Apply(new WindowAction.UpdateCandidateWindow(
            Visible: true,
            Position: new WindowRect(1, 2, 3, 4),
            Candidates: null,
            SelectedIndex: null,
            InputMode: "A",
            Reading: "read",
            CandidateListVisible: false,
            ReadingVerticalAdjustment: 4));

        Assert.True(updated.Visible);
        Assert.Equal(new[] { "one", "two" }, updated.Candidates);
        Assert.Equal(1, updated.SelectedIndex);
        Assert.Equal("A", updated.InputMode);
        Assert.Equal("read", updated.Reading);
        Assert.False(updated.CandidateListVisible);
        Assert.Equal(4, updated.ReadingVerticalAdjustment);
    }

    [Fact]
    public void SelectionIsClampedToCandidateRange()
    {
        CandidateState state = CandidateState.Initial
            .Apply(new WindowAction.SetCandidate(["one", "two"]))
            .Apply(new WindowAction.SetSelection(99));

        Assert.Equal(1, state.SelectedIndex);
    }
}
```

- [ ] **Step 3: Implement geometry**

Create `apps/Azookey.UI/Candidate/WindowGeometry.cs` with the same constants as Rust:

```csharp
using Azookey.UI.Ipc;

namespace Azookey.UI.Candidate;

public readonly record struct WorkArea(int Left, int Top, int Right, int Bottom);
public readonly record struct WindowSize(int Width, int Height);
public readonly record struct RubyWindowSize(double Width, double Height);

public static class WindowGeometry
{
    private const int CandidateXOffset = 15;
    private const int CandidateYGap = 6;
    private const int RubyYGap = 2;
    private const int RubyAutoAnchorMaxOffset = 18;
    private const int ReadingAdjustmentMin = -12;
    private const int ReadingAdjustmentMax = 12;

    public static (int X, int Y) CandidateWindowPosition(WindowRect target, WindowSize size, WorkArea workArea)
    {
        int x = ClampStart(target.Left - CandidateXOffset, size.Width, workArea.Left, workArea.Right);
        int below = target.Bottom + CandidateYGap;
        int above = target.Top - size.Height - CandidateYGap;
        int y;
        if (below + size.Height <= workArea.Bottom) y = below;
        else if (above >= workArea.Top) y = above;
        else
        {
            int belowSpace = Math.Max(0, workArea.Bottom - target.Bottom);
            int aboveSpace = Math.Max(0, target.Top - workArea.Top);
            int preferred = belowSpace >= aboveSpace ? below : above;
            y = ClampStart(preferred, size.Height, workArea.Top, workArea.Bottom);
        }
        return (x, y);
    }

    public static (int X, int Y) CandidateWindowPositionWithRubyClearance(WindowRect target, WindowSize candidateSize, WindowSize rubySize, WorkArea workArea, int verticalAdjustment)
    {
        (int x, int y) = CandidateWindowPosition(target, candidateSize, workArea);
        (_, int rubyY) = RubyWindowPosition(target, rubySize, workArea, verticalAdjustment);
        int rubyBottom = rubyY + rubySize.Height;
        bool rubyBelow = rubyY >= target.Bottom;
        bool rubyAbove = rubyY < target.Top;
        int candidateBottom = y + candidateSize.Height;
        bool candidateBelow = y >= target.Bottom;
        bool candidateAbove = y < target.Top;
        bool overlaps = rubyY < candidateBottom && rubyBottom > y;

        if (rubyBelow && candidateBelow && overlaps)
        {
            int shiftedBelowRuby = rubyBottom + RubyYGap;
            int candidateAboveInput = target.Top - candidateSize.Height - CandidateYGap;
            int nextY = shiftedBelowRuby + candidateSize.Height <= workArea.Bottom
                ? shiftedBelowRuby
                : candidateAboveInput >= workArea.Top ? candidateAboveInput : ClampStart(shiftedBelowRuby, candidateSize.Height, workArea.Top, workArea.Bottom);
            return (x, nextY);
        }

        if (rubyAbove && candidateAbove && overlaps)
        {
            int shiftedAboveRuby = rubyY - candidateSize.Height - RubyYGap;
            int candidateBelowInput = target.Bottom + CandidateYGap;
            int nextY = shiftedAboveRuby >= workArea.Top
                ? shiftedAboveRuby
                : candidateBelowInput + candidateSize.Height <= workArea.Bottom ? candidateBelowInput : ClampStart(shiftedAboveRuby, candidateSize.Height, workArea.Top, workArea.Bottom);
            return (x, nextY);
        }

        return (x, y);
    }

    public static (int X, int Y) RubyWindowPosition(WindowRect target, WindowSize size, WorkArea workArea, int verticalAdjustment)
    {
        int targetWidth = Math.Min(target.Right - target.Left, size.Width);
        int targetCenter = target.Left + targetWidth / 2;
        int x = ClampStart(targetCenter - size.Width / 2, size.Width, workArea.Left, workArea.Right);
        int targetHeight = target.Bottom - target.Top;
        int adjustment = Math.Clamp(verticalAdjustment, ReadingAdjustmentMin, ReadingAdjustmentMax);
        int anchorOffset = Math.Max(0, Math.Min(targetHeight / 2, RubyAutoAnchorMaxOffset) - adjustment);
        int above = target.Top + anchorOffset - size.Height - RubyYGap;
        int below = target.Bottom + RubyYGap;
        int y = above >= workArea.Top ? above : ClampStart(below, size.Height, workArea.Top, workArea.Bottom);
        return (x, y);
    }

    public static RubyWindowSize RubyWindowSizeForWorkArea(double measuredWidth, double measuredHeight, WorkArea workArea, double scaleFactor)
    {
        double scale = double.IsFinite(scaleFactor) && scaleFactor > 0 ? scaleFactor : 1;
        double width = double.IsFinite(measuredWidth) ? Math.Max(1, Math.Ceiling(measuredWidth)) : 1;
        double height = double.IsFinite(measuredHeight) ? Math.Max(1, Math.Ceiling(measuredHeight)) : 1;
        int workWidth = workArea.Right - workArea.Left;
        double maxWidth = workWidth > 0 ? Math.Max(1, workWidth / scale) : width;
        return new RubyWindowSize(Math.Min(width, maxWidth), height);
    }

    private static int ClampStart(int preferred, int length, int min, int max)
    {
        if (max <= min || length >= max - min) return min;
        return Math.Clamp(preferred, min, max - length);
    }
}
```

- [ ] **Step 4: Implement state reducer**

Create `apps/Azookey.UI/Candidate/CandidateState.cs`:

```csharp
using Azookey.UI.Ipc;

namespace Azookey.UI.Candidate;

public sealed record CandidateState
{
    public static CandidateState Initial { get; } = new();
    public bool Visible { get; init; }
    public WindowRect? Position { get; init; }
    public IReadOnlyList<string> Candidates { get; init; } = [];
    public int SelectedIndex { get; init; }
    public string InputMode { get; init; } = "あ";
    public string Reading { get; init; } = "";
    public bool CandidateListVisible { get; init; } = true;
    public int ReadingVerticalAdjustment { get; init; } = 4;

    public CandidateState Apply(WindowAction action)
    {
        return action switch
        {
            WindowAction.Show => this with { Visible = true },
            WindowAction.Hide => this with { Visible = false, Reading = "" },
            WindowAction.SetPosition set => this with { Position = set.Position },
            WindowAction.SetCandidate set => this with { Candidates = set.Candidates.ToArray(), CandidateListVisible = true, SelectedIndex = ClampSelection(SelectedIndex, set.Candidates.Count) },
            WindowAction.SetSelection set => this with { SelectedIndex = ClampSelection(set.Index, Candidates.Count) },
            WindowAction.SetInputMode set => this with { InputMode = set.Mode },
            WindowAction.UpdateCandidateWindow update => Apply(update),
            _ => this
        };
    }

    private CandidateState Apply(WindowAction.UpdateCandidateWindow update)
    {
        IReadOnlyList<string> candidates = update.Candidates?.ToArray() ?? Candidates;
        int selectedIndex = ClampSelection(update.SelectedIndex ?? SelectedIndex, candidates.Count);
        return this with
        {
            Visible = update.Visible ?? Visible,
            Position = update.Position ?? Position,
            Candidates = candidates,
            SelectedIndex = selectedIndex,
            InputMode = update.InputMode ?? InputMode,
            Reading = update.Reading ?? Reading,
            CandidateListVisible = update.CandidateListVisible ?? CandidateListVisible,
            ReadingVerticalAdjustment = update.ReadingVerticalAdjustment ?? ReadingVerticalAdjustment
        };
    }

    private static int ClampSelection(int index, int count)
    {
        if (count <= 0) return 0;
        return Math.Clamp(index, 0, count - 1);
    }
}
```

- [ ] **Step 5: Run tests**

Run:

```powershell
dotnet test C:\Users\Takahiro\Documents\Azuki-Win\apps\Azookey.UI.Tests\Azookey.UI.Tests.csproj --filter "WindowGeometryTests|CandidateStateTests"
```

Expected:

```text
Passed!  - Failed: 0
```

- [ ] **Step 6: Commit**

```powershell
git add apps/Azookey.UI apps/Azookey.UI.Tests
git commit -m "feat: port candidate window state and geometry"
```

---

### Task 6: WinUI Candidate, Ruby, And Indicator Windows

**Files:**
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.UI/App.xaml`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.UI/App.xaml.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.UI/Program.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.UI/Windows/CandidateWindow.xaml`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.UI/Windows/CandidateWindow.xaml.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.UI/Windows/RubyWindow.xaml`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.UI/Windows/RubyWindow.xaml.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.UI/Windows/IndicatorWindow.xaml`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.UI/Windows/IndicatorWindow.xaml.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Core/Win32/WindowInterop.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.UI/UiWindowCoordinator.cs`

**Interfaces:**
- Consumes: `CandidateState`, `WindowGeometry`, `WindowAction`.
- Produces: `UiWindowCoordinator : IWindowActionSink`.
- Produces: non-activating, tool, topmost WinUI windows equivalent to `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST` and `WS_POPUP`.

- [ ] **Step 1: Write a coordinator smoke test**

Create `apps/Azookey.UI.Tests/Candidate/UiWindowCoordinatorTests.cs`:

```csharp
using Azookey.UI;
using Azookey.UI.Candidate;
using Azookey.UI.Ipc;

namespace Azookey.UI.Tests.Candidate;

public sealed class UiWindowCoordinatorTests
{
    [Fact]
    public async Task CoordinatorAppliesActionsToStateBeforeRendering()
    {
        var renderer = new RecordingRenderer();
        var coordinator = new UiWindowCoordinator(renderer);

        await coordinator.SendAsync(new WindowAction.SetCandidate(["a", "b"]), CancellationToken.None);
        await coordinator.SendAsync(new WindowAction.SetSelection(1), CancellationToken.None);

        Assert.Equal(2, renderer.LastState.Candidates.Count);
        Assert.Equal(1, renderer.LastState.SelectedIndex);
    }

    private sealed class RecordingRenderer : IUiWindowRenderer
    {
        public CandidateState LastState { get; private set; } = CandidateState.Initial;
        public void Render(CandidateState state) => LastState = state;
    }
}
```

- [ ] **Step 2: Implement coordinator**

Create `apps/Azookey.UI/UiWindowCoordinator.cs`:

```csharp
using Azookey.UI.Candidate;
using Azookey.UI.Ipc;

namespace Azookey.UI;

public interface IUiWindowRenderer
{
    void Render(CandidateState state);
}

public sealed class UiWindowCoordinator : IWindowActionSink
{
    private readonly IUiWindowRenderer renderer;
    private CandidateState state = CandidateState.Initial;

    public UiWindowCoordinator(IUiWindowRenderer renderer) => this.renderer = renderer;

    public ValueTask SendAsync(WindowAction action, CancellationToken cancellationToken)
    {
        state = state.Apply(action);
        renderer.Render(state);
        return ValueTask.CompletedTask;
    }
}
```

- [ ] **Step 3: Add Win32 interop helper**

Create `apps/Azookey.Core/Win32/WindowInterop.cs`:

```csharp
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace Azookey.Core.Win32;

public static partial class WindowInterop
{
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsExToolWindow = 0x00000080;
    private const int WsExTopmost = 0x00000008;
    private const int WsExNoActivate = 0x08000000;
    private const int SwShownoactivate = 4;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpShowWindow = 0x0040;
    private static readonly IntPtr HwndTopmost = new(-1);

    public static IntPtr GetHwnd(object window) => WindowNative.GetWindowHandle(window);

    public static void MakeImeToolWindow(object window)
    {
        IntPtr hwnd = GetHwnd(window);
        SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(WsExToolWindow | WsExNoActivate | WsExTopmost));
        SetWindowLongPtr(hwnd, GwlStyle, new IntPtr(WsPopup));
    }

    public static void ShowNoActivate(object window)
    {
        IntPtr hwnd = GetHwnd(window);
        ShowWindow(hwnd, SwShownoactivate);
        SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoActivate | SwpNoZOrder | SwpShowWindow);
    }

    public static void MoveNoActivate(object window, int x, int y, int width, int height)
    {
        SetWindowPos(GetHwnd(window), HwndTopmost, x, y, width, height, SwpNoActivate | SwpShowWindow);
    }

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
```

- [ ] **Step 4: Create WinUI XAML windows**

Create `CandidateWindow.xaml` with a 5-row list and footer:

```xml
<Window
    x:Class="Azookey.UI.Windows.CandidateWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Border x:Name="RootBorder" Padding="8" CornerRadius="8" BorderThickness="1" Background="{ThemeResource CardBackgroundFillColorDefaultBrush}" BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}">
        <Grid RowDefinitions="Auto,1,Auto">
            <ItemsRepeater x:Name="CandidateRepeater" Grid.Row="0" />
            <Rectangle Grid.Row="1" Height="1" Fill="{ThemeResource DividerStrokeColorDefaultBrush}" />
            <TextBlock Grid.Row="2" Text="Azookey" FontSize="12" Opacity="0.7" Margin="8,6,8,0" />
        </Grid>
    </Border>
</Window>
```

Create `RubyWindow.xaml`:

```xml
<Window
    x:Class="Azookey.UI.Windows.RubyWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Border Padding="12,4" MinWidth="44" MinHeight="30" CornerRadius="15" BorderThickness="1" Background="{ThemeResource CardBackgroundFillColorDefaultBrush}" BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}">
        <TextBlock x:Name="ReadingText" FontFamily="Yu Gothic UI, Meiryo" FontSize="16" TextTrimming="CharacterEllipsis" TextAlignment="Center" />
    </Border>
</Window>
```

Create `IndicatorWindow.xaml`:

```xml
<Window
    x:Class="Azookey.UI.Windows.IndicatorWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Border Width="90" Height="90" CornerRadius="8" BorderThickness="1" Background="{ThemeResource CardBackgroundFillColorDefaultBrush}" BorderBrush="{ThemeResource AccentFillColorDefaultBrush}">
        <TextBlock x:Name="ModeText" Text="あ" FontSize="28" HorizontalAlignment="Center" VerticalAlignment="Center" />
    </Border>
</Window>
```

- [ ] **Step 5: Implement window code-behind**

Each window constructor calls `WindowInterop.MakeImeToolWindow(this)` after `InitializeComponent()`. `CandidateWindow.Render(CandidateState state)` updates candidates, selected visual state, and list visibility. `RubyWindow.Render(CandidateState state)` updates `ReadingText.Text`. `IndicatorWindow.Render(CandidateState state)` updates `ModeText.Text`.

Use this shared method shape:

```csharp
public void Render(CandidateState state)
{
    if (state.Visible)
    {
        WindowInterop.ShowNoActivate(this);
    }
    else
    {
        AppWindow.Hide();
    }
}
```

- [ ] **Step 6: Host named pipe gRPC in `ui.exe`**

Create `Program.cs` and `App.xaml.cs` so startup creates the three windows, creates a renderer, starts Kestrel on `azookey_ui`, and registers `WindowServiceImpl`.

The Kestrel named pipe binding must be:

```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenNamedPipe("azookey_ui", listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
});
```

- [ ] **Step 7: Run tests and smoke-start `ui.exe`**

Run:

```powershell
dotnet test C:\Users\Takahiro\Documents\Azuki-Win\apps\Azookey.UI.Tests\Azookey.UI.Tests.csproj
dotnet publish C:\Users\Takahiro\Documents\Azuki-Win\apps\Azookey.UI\Azookey.UI.csproj -c Debug -r win-x64 --self-contained true -p:PublishDir=C:\Users\Takahiro\Documents\Azuki-Win\.local\winui-smoke\ui\
Start-Process C:\Users\Takahiro\Documents\Azuki-Win\.local\winui-smoke\ui\ui.exe -WindowStyle Hidden
Get-ChildItem \\.\pipe\ | Where-Object Name -eq 'azookey_ui'
```

Expected:

```text
azookey_ui
```

- [ ] **Step 8: Commit**

```powershell
git add apps/Azookey.Core apps/Azookey.UI apps/Azookey.UI.Tests
git commit -m "feat: add WinUI IME windows"
```

---

### Task 7: Settings App State And Save Workflow

**Files:**
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Settings/App.xaml`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Settings/App.xaml.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Settings/MainWindow.xaml`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Settings/MainWindow.xaml.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Settings/Services/SettingsAppState.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Settings/Services/ServerConfigNotifier.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Settings.Tests/Services/SettingsAppStateTests.cs`

**Interfaces:**
- Consumes: `ConfigStore`, generated `AzookeyService.AzookeyServiceClient`.
- Produces: `SettingsAppState.Config`, `SettingsAppState.SaveAsync(AppConfig config)`.
- Produces: `SaveResult(bool Saved, bool ServerApplied, string? Message)`.

- [ ] **Step 1: Write failing settings state tests**

Create `apps/Azookey.Settings.Tests/Services/SettingsAppStateTests.cs`:

```csharp
using Azookey.Core.Config;
using Azookey.Settings.Services;

namespace Azookey.Settings.Tests.Services;

public sealed class SettingsAppStateTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "azookey-settings-tests", Guid.NewGuid().ToString("N"));
    public void Dispose() => Directory.Delete(root, true);

    [Fact]
    public async Task SaveReportsSavedWhenServerIsUnavailable()
    {
        var store = new ConfigStore(Path.Combine(root, "Azookey"));
        var notifier = new RecordingNotifier(false);
        var state = new SettingsAppState(store, notifier);
        AppConfig config = AppConfig.CreateDefault() with { Zenzai = new ZenzaiConfig { Enable = true, Backend = "cpu", Profile = "" } };

        SaveResult result = await state.SaveAsync(config);

        Assert.True(result.Saved);
        Assert.False(result.ServerApplied);
        Assert.Contains("not available", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(state.Config.Zenzai.Enable);
    }

    [Fact]
    public async Task SaveFailureDoesNotReplaceInMemoryState()
    {
        var store = new ThrowingStore(Path.Combine(root, "Azookey"));
        var state = new SettingsAppState(store, new RecordingNotifier(true));

        await Assert.ThrowsAsync<InvalidOperationException>(() => state.SaveAsync(AppConfig.CreateDefault() with { Zenzai = new ZenzaiConfig { Enable = true, Backend = "cpu", Profile = "" } }));

        Assert.False(state.Config.Zenzai.Enable);
    }

    private sealed class RecordingNotifier(bool succeeds) : IServerConfigNotifier
    {
        public Task NotifyAsync(CancellationToken cancellationToken)
        {
            if (succeeds)
            {
                return Task.CompletedTask;
            }

            throw new InvalidOperationException("not available");
        }
    }

    private sealed class ThrowingStore(string configRoot) : IConfigStore
    {
        public ConfigLoadResult LoadWithRecovery()
        {
            return new ConfigLoadResult(AppConfig.CreateDefault(), null, null);
        }

        public void Write(AppConfig config)
        {
            throw new InvalidOperationException($"write failed for {configRoot}");
        }
    }
}
```

- [ ] **Step 2: Implement services**

Create `apps/Azookey.Settings/Services/SettingsAppState.cs`:

```csharp
using Azookey.Core.Config;

namespace Azookey.Settings.Services;

public sealed record SaveResult(bool Saved, bool ServerApplied, string? Message);

public interface IConfigStore
{
    ConfigLoadResult LoadWithRecovery();
    void Write(AppConfig config);
}

public interface IServerConfigNotifier
{
    Task NotifyAsync(CancellationToken cancellationToken);
}

public sealed class SettingsAppState
{
    private readonly IConfigStore store;
    private readonly IServerConfigNotifier notifier;
    public AppConfig Config { get; private set; }
    public ConfigLoadResult LoadResult { get; }

    public SettingsAppState(IConfigStore store, IServerConfigNotifier notifier)
    {
        this.store = store;
        this.notifier = notifier;
        LoadResult = store.LoadWithRecovery();
        Config = LoadResult.Config;
    }

    public async Task<SaveResult> SaveAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        store.Write(config);
        Config = config;
        try
        {
            await notifier.NotifyAsync(cancellationToken);
            return new SaveResult(true, true, null);
        }
        catch (Exception error)
        {
            return new SaveResult(true, false, $"Server is not available: {error.Message}");
        }
    }
}
```

Make `ConfigStore` implement `IConfigStore`.

Create `apps/Azookey.Settings/Services/ServerConfigNotifier.cs`:

```csharp
using Azookey.Core.Ipc;
using Azookey;

namespace Azookey.Settings.Services;

public sealed class ServerConfigNotifier : IServerConfigNotifier
{
    public async Task NotifyAsync(CancellationToken cancellationToken)
    {
        using Grpc.Net.Client.GrpcChannel channel = NamedPipeGrpcClientFactory.CreateChannel("azookey_server", TimeSpan.FromSeconds(3));
        var client = new AzookeyService.AzookeyServiceClient(channel);
        await client.UpdateConfigAsync(new UpdateConfigRequest { RequestId = 0 }, cancellationToken: cancellationToken);
    }
}
```

- [ ] **Step 3: Create shell window**

Create `MainWindow.xaml`:

```xml
<Window
    x:Class="Azookey.Settings.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <NavigationView x:Name="Navigation" PaneDisplayMode="Left" IsBackButtonVisible="Collapsed">
        <NavigationView.MenuItems>
            <NavigationViewItem Content="General" Tag="general" Icon="Setting" />
            <NavigationViewItem Content="Zenzai" Tag="zenzai" Icon="Library" />
            <NavigationViewItem Content="Dictionary" Tag="dictionary" Icon="Edit" />
            <NavigationViewItem Content="Debug" Tag="debug" Icon="Repair" />
            <NavigationViewItem Content="About" Tag="about" Icon="Help" />
        </NavigationView.MenuItems>
        <Frame x:Name="ContentFrame" />
    </NavigationView>
</Window>
```

- [ ] **Step 4: Run tests**

Run:

```powershell
dotnet test C:\Users\Takahiro\Documents\Azuki-Win\apps\Azookey.Settings.Tests\Azookey.Settings.Tests.csproj --filter SettingsAppStateTests
```

Expected:

```text
Passed!  - Failed: 0
```

- [ ] **Step 5: Commit**

```powershell
git add apps/Azookey.Core apps/Azookey.Settings apps/Azookey.Settings.Tests
git commit -m "feat: add WinUI settings state"
```

---

### Task 8: Settings Pages Feature Parity

**Files:**
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Settings/Pages/GeneralPage.xaml`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Settings/Pages/GeneralPage.xaml.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Settings/Pages/ZenzaiPage.xaml`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Settings/Pages/ZenzaiPage.xaml.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Settings/Pages/DictionaryPage.xaml`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Settings/Pages/DictionaryPage.xaml.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Settings/Pages/DebugPage.xaml`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Settings/Pages/DebugPage.xaml.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Settings/Pages/AboutPage.xaml`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Settings/Pages/AboutPage.xaml.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Settings/ViewModels/SettingsPageViewModels.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Settings.Tests/ViewModels/SettingsPageViewModelTests.cs`

**Interfaces:**
- Consumes: `SettingsAppState.SaveAsync`.
- Produces: page handlers for every existing React/Tauri setting field.

- [ ] **Step 1: Write failing view model tests for page behavior**

Create `apps/Azookey.Settings.Tests/ViewModels/SettingsPageViewModelTests.cs`:

```csharp
using Azookey.Core.Config;
using Azookey.Settings.ViewModels;

namespace Azookey.Settings.Tests.ViewModels;

public sealed class SettingsPageViewModelTests
{
    [Fact]
    public void DictionaryRejectsMoreThanFiftyEntries()
    {
        var entries = Enumerable.Range(0, 51).Select(index => new UserDictionaryEntry { Reading = $"r{index}", Word = $"w{index}" }).ToList();

        DictionaryValidationResult result = DictionaryViewModel.Validate(entries);

        Assert.False(result.IsValid);
        Assert.Equal("Dictionary entries must be 50 or fewer.", result.Message);
    }

    [Fact]
    public void DictionaryRejectsDuplicateReadingWordPair()
    {
        var entries = new List<UserDictionaryEntry>
        {
            new() { Reading = "かな", Word = "仮名" },
            new() { Reading = "かな", Word = "仮名" }
        };

        DictionaryValidationResult result = DictionaryViewModel.Validate(entries);

        Assert.False(result.IsValid);
        Assert.Equal("Dictionary contains duplicate reading and word pairs.", result.Message);
    }

    [Fact]
    public void ReadingAdjustmentIsClamped()
    {
        Assert.Equal(12, GeneralViewModel.ClampReadingAdjustment(20));
        Assert.Equal(-12, GeneralViewModel.ClampReadingAdjustment(-20));
    }
}
```

- [ ] **Step 2: Implement page view model helpers**

Create `apps/Azookey.Settings/ViewModels/SettingsPageViewModels.cs`:

```csharp
using Azookey.Core.Config;

namespace Azookey.Settings.ViewModels;

public static class GeneralViewModel
{
    public static int ClampReadingAdjustment(int value) => Math.Clamp(value, -12, 12);
}

public sealed record DictionaryValidationResult(bool IsValid, string? Message)
{
    public static DictionaryValidationResult Valid { get; } = new(true, null);
}

public static class DictionaryViewModel
{
    public static DictionaryValidationResult Validate(IReadOnlyList<UserDictionaryEntry> entries)
    {
        if (entries.Count > 50)
        {
            return new DictionaryValidationResult(false, "Dictionary entries must be 50 or fewer.");
        }
        if (entries.Any(entry => string.IsNullOrWhiteSpace(entry.Reading) || string.IsNullOrWhiteSpace(entry.Word)))
        {
            return new DictionaryValidationResult(false, "Dictionary reading and word must not be empty.");
        }
        bool hasDuplicate = entries
            .GroupBy(entry => (entry.Reading.Trim(), entry.Word.Trim()))
            .Any(group => group.Count() > 1);
        return hasDuplicate
            ? new DictionaryValidationResult(false, "Dictionary contains duplicate reading and word pairs.")
            : DictionaryValidationResult.Valid;
    }
}
```

- [ ] **Step 3: Build General page controls**

`GeneralPage.xaml` must expose these controls and save each change through `SettingsAppState.SaveAsync`:

```text
PunctuationStyle: touten_kuten, fullwidth_comma_fullwidth_period, touten_fullwidth_period, fullwidth_comma_kuten
SymbolStyle: corner_bracket_middle_dot, square_bracket_backslash, corner_bracket_backslash, square_bracket_middle_dot
SpaceInput: always_half, follow_input_mode
NumpadInput: direct_input, always_half, follow_input_mode
ShowCandidateWindowAfterSpace: ToggleSwitch
ShowLiveConversionReading: ToggleSwitch
LiveConversionReadingVerticalAdjustment: Slider Minimum=-12 Maximum=12 StepFrequency=1
PunctuationCommit: ToggleSwitch
PunctuationCommitPunctuation: ToggleSwitch
PunctuationCommitExclamation: ToggleSwitch
PunctuationCommitQuestion: ToggleSwitch
RomajiTable.Rows: editable dialog with input, output, next_input
Shortcuts.CtrlSpaceToggle: ToggleSwitch
Shortcuts.AltBackquoteToggle: ToggleSwitch
Shortcuts.EisuToggle: ToggleSwitch
```

The code-behind uses `state.Config with { General = state.Config.General with { ... } }` for record updates and calls `await state.SaveAsync(nextConfig)`.

- [ ] **Step 4: Build Zenzai page controls**

`ZenzaiPage.xaml` must expose:

```text
Zenzai.Enable: ToggleSwitch
Zenzai.Profile: TextBox with AcceptsReturn=True
Zenzai.Backend: ComboBox values cpu, cuda, vulkan
Capability badges: cpu, cuda, vulkan
```

The code-behind computes capability:

```csharp
public sealed record Capability(bool Cpu, bool Cuda, bool Vulkan);
```

CPU uses `X86Base.IsSupported && Avx.IsSupported`. CUDA requires both `cudart64_12.dll` and `cublas64_12.dll` in `PATH` or `AppContext.BaseDirectory`. Vulkan requires `vulkan-1.dll` in `PATH` or `AppContext.BaseDirectory`.

- [ ] **Step 5: Build Dictionary page controls**

`DictionaryPage.xaml` uses a `ListView` with two `TextBox` columns, add/remove buttons, and a save button. On save:

```csharp
DictionaryValidationResult validation = DictionaryViewModel.Validate(entries);
if (!validation.IsValid)
{
    ShowInfoBar(validation.Message!);
    return;
}
```

Then write `UserDictionary = new UserDictionaryConfig { Entries = entries.Select(trimmed).ToList() }`.

- [ ] **Step 6: Build Debug and About pages**

`DebugPage.xaml` exposes:

```text
Debug.ServerLogEnabled: ToggleSwitch
Debug.ServerLogLevel: ComboBox values off, error, warn, info, debug
Debug.ServerCrashTraceEnabled: ToggleSwitch
RestartServer: Button
```

`AboutPage.xaml` exposes app name, version, and the Discord URL `https://discord.com/invite/dY9gHuyZN5` through a `HyperlinkButton`.

- [ ] **Step 7: Run tests and build settings app**

Run:

```powershell
dotnet test C:\Users\Takahiro\Documents\Azuki-Win\apps\Azookey.Settings.Tests\Azookey.Settings.Tests.csproj
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe'
& $msbuild C:\Users\Takahiro\Documents\Azuki-Win\apps\Azookey.Settings\Azookey.Settings.csproj /restore /p:Configuration=Debug /p:Platform=x64
```

Expected:

```text
Passed!  - Failed: 0
Build succeeded.
```

- [ ] **Step 8: Commit**

```powershell
git add apps/Azookey.Settings apps/Azookey.Settings.Tests
git commit -m "feat: add WinUI settings pages"
```

---

### Task 9: Updates, Restart, Runtime Check, And Existing Launcher Interop

**Files:**
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Core/Update/UpdateService.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Core/Process/LauncherClient.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Core/Process/ServerRestartService.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Core/Runtime/WindowsAppRuntimeChecker.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Core.Tests/Update/UpdateServiceTests.cs`
- Create: `C:/Users/Takahiro/Documents/Azuki-Win/apps/Azookey.Core.Tests/Process/LauncherClientTests.cs`

**Interfaces:**
- Produces: release check and installer launch behavior equivalent to `frontend/src-tauri/src/updater.rs`.
- Produces: restart behavior equivalent to `frontend/src-tauri/src/server_process.rs`.

- [ ] **Step 1: Write failing update tests**

Create `apps/Azookey.Core.Tests/Update/UpdateServiceTests.cs`:

```csharp
using Azookey.Core.Update;

namespace Azookey.Core.Tests.Update;

public sealed class UpdateServiceTests
{
    [Fact]
    public void SelectsSetupAndSha256Assets()
    {
        var release = new GitHubRelease("v1", false, [
            new ReleaseAsset("azookey-setup.exe", "https://example.test/setup.exe"),
            new ReleaseAsset("SHA256SUMS.txt", "https://example.test/SHA256SUMS.txt")
        ]);

        ReleaseAssets assets = UpdateService.SelectAssets(release);

        Assert.Equal("https://example.test/setup.exe", assets.SetupUrl);
        Assert.Equal("https://example.test/SHA256SUMS.txt", assets.Sha256Url);
    }

    [Fact]
    public void RejectsSha256Mismatch()
    {
        byte[] bytes = "abc"u8.ToArray();
        string sums = "0000000000000000000000000000000000000000000000000000000000000000  azookey-setup.exe";

        Assert.False(UpdateService.VerifySha256(bytes, sums, "azookey-setup.exe"));
    }
}
```

Create `apps/Azookey.Core.Tests/Process/LauncherClientTests.cs`:

```csharp
using Azookey.Core.Process;

namespace Azookey.Core.Tests.Process;

public sealed class LauncherClientTests
{
    [Theory]
    [InlineData("ok", true)]
    [InlineData("OK\r\n", true)]
    [InlineData("error: restart failed", false)]
    [InlineData("unexpected", false)]
    public void ParsesLauncherRestartResponse(string response, bool expected)
    {
        Assert.Equal(expected, LauncherClient.LauncherRestartSucceeded(response));
    }
}
```

- [ ] **Step 2: Implement update service contracts**

Create `apps/Azookey.Core/Update/UpdateService.cs`:

```csharp
using System.Security.Cryptography;

namespace Azookey.Core.Update;

public sealed record ReleaseAsset(string Name, string BrowserDownloadUrl);
public sealed record GitHubRelease(string TagName, bool Prerelease, IReadOnlyList<ReleaseAsset> Assets);
public sealed record ReleaseAssets(string SetupUrl, string Sha256Url);

public static class UpdateService
{
    public static ReleaseAssets SelectAssets(GitHubRelease release)
    {
        string setup = release.Assets.Single(asset => asset.Name == "azookey-setup.exe").BrowserDownloadUrl;
        string sha = release.Assets.Single(asset => asset.Name == "SHA256SUMS.txt").BrowserDownloadUrl;
        return new ReleaseAssets(setup, sha);
    }

    public static bool VerifySha256(byte[] bytes, string sums, string fileName)
    {
        string actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        foreach (string line in sums.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[^1] == fileName)
            {
                return string.Equals(parts[0], actual, StringComparison.OrdinalIgnoreCase);
            }
        }
        return false;
    }
}
```

- [ ] **Step 3: Implement launcher and server restart services**

`LauncherClient` connects to `azookey_launcher`, sends the existing restart command string used by `frontend/src-tauri/src/server_process.rs`, accepts `ok`, and treats `error:` as failure. `ServerRestartService` falls back to direct `azookey-server.exe` start from the install directory when the launcher pipe is unavailable.

Create `apps/Azookey.Core/Process/LauncherClient.cs` with this response parser:

```csharp
namespace Azookey.Core.Process;

public static class LauncherClient
{
    public static bool LauncherRestartSucceeded(string response)
    {
        if (response.Trim().Equals("ok", StringComparison.OrdinalIgnoreCase)) return true;
        if (response.StartsWith("error:", StringComparison.OrdinalIgnoreCase)) return false;
        return false;
    }
}
```

- [ ] **Step 4: Implement runtime checker**

Create `apps/Azookey.Core/Runtime/WindowsAppRuntimeChecker.cs`:

```csharp
namespace Azookey.Core.Runtime;

public static class WindowsAppRuntimeChecker
{
    public static bool IsRuntimeLikelyInstalled()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string packages = Path.Combine(localAppData, "Microsoft", "WindowsApps");
        return Directory.Exists(packages) || OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041);
    }
}
```

- [ ] **Step 5: Run tests**

Run:

```powershell
dotnet test C:\Users\Takahiro\Documents\Azuki-Win\apps\Azookey.Core.Tests\Azookey.Core.Tests.csproj --filter "UpdateServiceTests|LauncherClientTests"
```

Expected:

```text
Passed!  - Failed: 0
```

- [ ] **Step 6: Commit**

```powershell
git add apps/Azookey.Core apps/Azookey.Core.Tests
git commit -m "feat: port settings updater and restart services"
```

---

### Task 10: Build Script And Installer Migration

**Files:**
- Modify: `C:/Users/Takahiro/Documents/Azuki-Win/Makefile.toml`
- Modify: `C:/Users/Takahiro/Documents/Azuki-Win/installer/Installer.iss`
- Modify: `C:/Users/Takahiro/Documents/Azuki-Win/installer/CodeDependencies.iss`

**Interfaces:**
- Consumes: `apps/Azookey.UI/Azookey.UI.csproj`, `apps/Azookey.Settings/Azookey.Settings.csproj`.
- Produces: `build/ui.exe` and `build/frontend.exe`.
- Produces: installer that deploys Windows App SDK Runtime and no longer deploys WebView2 Runtime.

- [ ] **Step 1: Modify `Makefile.toml`**

Replace `[tasks.build_tauri]` with:

```toml
[tasks.build_winui]
description = "Build the WinUI apps"
script_runner = "powershell"
script_extension = "ps1"
script = """
$ErrorActionPreference = "Stop"
$root = Resolve-Path "."
$publish = Join-Path $root "build"
New-Item -ItemType Directory -Force $publish | Out-Null
dotnet publish apps/Azookey.UI/Azookey.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishDir=$publish/
dotnet publish apps/Azookey.Settings/Azookey.Settings.csproj -c Release -r win-x64 --self-contained true -p:PublishDir=$publish/
if (!(Test-Path (Join-Path $publish "ui.exe"))) { throw "ui.exe was not published" }
if (!(Test-Path (Join-Path $publish "frontend.exe"))) { throw "frontend.exe was not published" }
"""
```

Remove these lines from `[tasks.post_build]`:

```powershell
cp target/$str/ui.exe build
cp target/$str/frontend.exe build
```

Change build dependencies from `build_tauri` to `build_winui`.

- [ ] **Step 2: Add Windows App SDK Runtime dependency helper**

Add to `installer/CodeDependencies.iss`:

```pascal
procedure Dependency_AddWindowsAppRuntime220x64;
begin
  if not RegKeyExists(HKLM, 'SOFTWARE\Microsoft\WindowsAppRuntime\2.2') then begin
    Dependency_Add('WindowsAppRuntimeInstall-2.2.0-x64.exe',
      '--quiet',
      'Windows App SDK Runtime 2.2.0 x64',
      'https://aka.ms/windowsappsdk/2.2/2.2.0/windowsappruntimeinstall-x64.exe',
      '', False, False);
  end;
end;
```

- [ ] **Step 3: Modify installer dependency calls**

In `installer/Installer.iss`, replace:

```pascal
  Dependency_AddWebView2;
```

with:

```pascal
  Dependency_AddWindowsAppRuntime220x64;
```

Keep:

```pascal
  Dependency_AddVC2015To2022x64;
  Dependency_AddVC2015To2022x86;
```

Remove these cleanup entries:

```pascal
Type: filesandordirs; Name: "{app}\frontend.exe.WebView2"
Type: filesandordirs; Name: "{app}\ui.exe.WebView2"
```

Keep:

```pascal
#define MySettingsAppName "frontend.exe"
RegWriteStringValue(..., 'MainBinaryName', '{#MySettingsAppName}')
```

- [ ] **Step 4: Build all binaries and installer**

Run:

```powershell
cargo make build --release
```

Expected:

```text
build\ui.exe
build\frontend.exe
installer\Output\azookey-setup.exe
```

- [ ] **Step 5: Commit**

```powershell
git add Makefile.toml installer/Installer.iss installer/CodeDependencies.iss
git commit -m "build: publish WinUI apps in installer"
```

---

### Task 11: End-To-End Verification And Retire Old UI Build Inputs

**Files:**
- Modify: `C:/Users/Takahiro/Documents/Azuki-Win/README.md`
- Modify: `C:/Users/Takahiro/Documents/Azuki-Win/crates/ui/Cargo.toml` only if the workspace build still tries to compile the Rust UI after `Makefile.toml` no longer consumes it.
- Modify: `C:/Users/Takahiro/Documents/Azuki-Win/frontend/README.md` to state that the Tauri frontend has been replaced by `apps/Azookey.Settings`.

**Interfaces:**
- Produces: verified local install where launcher starts C# `ui.exe`, language bar opens C# `frontend.exe`, and IME candidate windows receive `UpdateCandidateWindow`.

- [ ] **Step 1: Run all C# tests**

Run:

```powershell
dotnet test C:\Users\Takahiro\Documents\Azuki-Win\apps\Azookey.WinUI.sln
```

Expected:

```text
Passed!  - Failed: 0
```

- [ ] **Step 2: Run source build**

Run:

```powershell
cargo make build --release
```

Expected:

```text
build\ui.exe
build\frontend.exe
build\launcher.exe
build\azookey-server.exe
installer\Output\azookey-setup.exe
```

- [ ] **Step 3: Install locally**

Run:

```powershell
Start-Process C:\Users\Takahiro\Documents\Azuki-Win\installer\Output\azookey-setup.exe -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -Wait
```

Expected installed files:

```powershell
Test-Path $env:APPDATA\Azookey\ui.exe
Test-Path $env:APPDATA\Azookey\frontend.exe
```

Both print `True`.

- [ ] **Step 4: Verify runtime processes and pipes**

Run:

```powershell
Start-Process $env:APPDATA\Azookey\launcher.exe -WindowStyle Hidden
Start-Sleep -Seconds 3
Get-Process launcher,azookey-server,ui -ErrorAction SilentlyContinue | Select-Object ProcessName,Path
Get-ChildItem \\.\pipe\ | Where-Object { $_.Name -in @('azookey_ui','azookey_server','azookey_launcher') }
```

Expected:

```text
launcher
azookey-server
ui
azookey_ui
azookey_server
azookey_launcher
```

- [ ] **Step 5: Verify settings launch path**

Run:

```powershell
Start-Process $env:APPDATA\Azookey\frontend.exe
Start-Sleep -Seconds 3
Get-Process frontend -ErrorAction SilentlyContinue | Select-Object ProcessName,Path
```

Expected:

```text
frontend  C:\Users\Takahiro\AppData\Roaming\Azookey\frontend.exe
```

- [ ] **Step 6: Commit docs and cleanup**

```powershell
git add README.md frontend/README.md crates/ui/Cargo.toml
git commit -m "docs: document WinUI UI migration"
```

---

## Self Review

- Spec coverage: Candidate list, ruby reading, input mode indicator, settings pages, config schema, gRPC named pipes, executable names, Build Tools 2026 MSBuild, Windows App SDK Runtime prerequisite, self-contained .NET publish, installer dependency migration, and local verification are each covered by tasks above.
- Red flag scan: The plan contains no red-flag markers and every task has concrete files, commands, interfaces, and expected verification output.
- Type consistency: `AppConfig`, `ConfigStore`, `WindowAction`, `CandidateState`, `UiWindowCoordinator`, `SettingsAppState`, and `UpdateService` names are introduced before later tasks consume them.
