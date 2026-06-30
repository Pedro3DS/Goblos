# NeoConsolePlus

NeoConsolePlus adds an Editor console and a Development Build runtime overlay for Unity. It lets you create slash commands with a simple attribute, run those commands from the Editor or in-game overlay, use autocomplete/ghost text, navigate command history and keep debug logs out of regular release builds.

## Requirements

- Unity 2022.3 or newer.
- Works in the Editor and Development Builds.
- NeoConsolePlus command and overlay code is not compiled into regular release builds.

## Public API

Most projects only need these APIs:

- `[NeoCommand]`, `[NeoCommandEditorOnly]` and `[NeoCommandRuntimeOnly]` to register debug commands.
- `NeoConsole` for optional command execution and runtime overlay control from code.
- `NeoDebug` for debug logs that are stripped from regular release builds.

## Open NeoConsole+

Open the Editor window from:

```text
Tools > NeoUtils > NeoConsolePlus > Open NeoConsole
```

Open the settings window from:

```text
Tools > NeoUtils > NeoConsolePlus > Open Settings
```

You can also open it as a dockable Unity window:

```text
Window > General > NeoConsole+
```

## Create a command

Add `using Neo.ConsolePlus;` and mark a method with `[NeoCommand]`.

```csharp
using Neo.ConsolePlus;
using Neo.Debugging;

public static class DebugCommands
{
    [NeoCommand("log.test", "Logs a test message")]
    private static void LogTest(string message)
    {
        NeoDebug.Log(message);
    }
}
```

Run commands with `/` followed by the command name:

```text
/log.test "hello"
```

The `/` prefix is typed only in the console input. Do not include it in the attribute name.
Command names cannot contain whitespace because spaces are used to separate command arguments. Use `.`, `_` or `-` instead.

Commands can be `public`, `private`, `static` or instance methods on active `MonoBehaviour` components.

## Command scope

Use `[NeoCommand]` when a command should work in both the Editor console and the runtime overlay:

```csharp
[NeoCommand("debug.ping", "Works in both consoles")]
private static void Ping()
{
}
```

Use `[NeoCommandEditorOnly]` for commands that must only appear and run in the Editor console:

```csharp
[NeoCommandEditorOnly("assets.reimport", "Editor-only asset operation")]
private static void ReimportAssets()
{
}
```

Use `[NeoCommandRuntimeOnly]` for commands that must only appear and run in the runtime overlay:

```csharp
[NeoCommandRuntimeOnly("player.godmode", "Runtime-only gameplay test")]
private void ToggleGodMode()
{
}
```

| Attribute | Editor console | Runtime overlay |
|---|---:|---:|
| `[NeoCommand]` | Yes | Yes |
| `[NeoCommandEditorOnly]` | Yes | No |
| `[NeoCommandRuntimeOnly]` | No | Yes |

## Supported parameter types

NeoConsolePlus supports these parameter types:

- `string`
- `char`
- `int`
- `long`
- `float`
- `double`
- `bool`
- `enum`
- arrays, for example `string[]`
- `List<T>`, `IList<T>`, `ICollection<T>`, `IEnumerable<T>`, `HashSet<T>`, `Queue<T>` and `Stack<T>`
- `Dictionary<TKey, TValue>` and `IDictionary<TKey, TValue>`
- serializable custom classes and structs using public fields or `[SerializeField]` fields

NeoConsolePlus uses a JSON-like command syntax. It is intentionally friendlier than strict JSON:

```text
/spawn.enemy { Name: "Goblin", Level: 3, Team: Enemy }
```

Supported syntax examples:

```text
/test.setbool true
/test.setenum Epic
/test.settags ["Weapon", "Sword", "Debug"]
/test.setmaterials { "Iron": 10, "Wood": 5 }
/test.setstats [{ StatName: "Attack", Amount: 25.5, IsPercent: false }]
```

Custom object fields can be written as named fields:

```text
/spawn.enemy { Name: "Goblin", Level: 3, Team: Enemy }
```

Positional values are also accepted in field order:

```text
/spawn.enemy { "Goblin", 3, Enemy }
```

## Example: complex object command

```csharp
using System;
using System.Collections.Generic;
using Neo.ConsolePlus;
using Neo.Debugging;
using UnityEngine;

public enum TestRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

[Serializable]
public class StatData
{
    public string StatName;
    public float Amount;
    public bool IsPercent;
}

[Serializable]
public class ItemData
{
    public string ItemId;
    public string DisplayName;
    public TestRarity Rarity;
    public int Level;
    public bool IsEquipped;
    public StatData MainStat;
    public List<StatData> BonusStats;
    public Dictionary<string, int> Materials;
    public string[] Tags;
}

public class TestNeoConsole : MonoBehaviour
{
    [NeoCommand("test.setitem")]
    private static void SetItem(ItemData item)
    {
        NeoDebug.Log(item);
    }
}
```

Usage:

```text
/test.setitem { ItemId: "sword_001", DisplayName: "Training Sword", Rarity: Rare, Level: 12, IsEquipped: true, MainStat: { StatName: "Attack", Amount: 25.5, IsPercent: false }, BonusStats: [{ StatName: "CriticalChance", Amount: 10, IsPercent: true }], Materials: { "Iron": 10, "Wood": 5 }, Tags: ["Weapon", "Sword"] }
```

## Instance MonoBehaviour commands

Instance commands are methods declared on `MonoBehaviour` classes.

```csharp
using Neo.ConsolePlus;
using Neo.Debugging;
using UnityEngine;

public class EnemyDebugCommands : MonoBehaviour
{
    [NeoCommand("enemy.damage", "Applies damage to an enemy")]
    private void Damage(int amount)
    {
        NeoDebug.Log($"Damaged {gameObject.name} by {amount}");
    }

    [NeoCommand("enemy.kill", "Destroys an enemy")]
    private void Kill()
    {
        Destroy(gameObject);
    }
}
```

For instance commands, NeoConsolePlus automatically supports a single-target command and an `.All` variant.

```text
/enemy.damage "Goblin_01" 25
/enemy.damage.All 25
/enemy.kill "Goblin_01"
/enemy.kill.All
```

Target names are resolved from active GameObjects/components. If there is only one active instance, you can run the command without a target:

```text
/enemy.kill
```

If multiple active instances exist, provide a target name or Unity object identifier.

## Autocomplete and ghost text

Type `/` to show registered commands.

```text
/
```

Keep typing to filter:

```text
/enemy
```

Use the same shortcuts in the Editor window and in the runtime overlay:

- `Up` / `Down` to move through autocomplete suggestions. If there is no navigable suggestion, the input/caret is left untouched.
- `Ctrl + Up` / `Ctrl + Down` on Windows/Linux, or `Command + Up` / `Command + Down` on macOS, to recall command history.
- `Ctrl + Backspace` / `Command + Backspace` to safely delete the previous word without letting IMGUI process the native text operation.
- `Ctrl + Delete` / `Command + Delete` to safely delete the next word.
- `Tab` to complete the selected suggestion.
- `Enter` to execute the current command.

String parameters are completed with quotes and the cursor is placed between them:

```text
/log.test "|"
```

`|` represents the cursor position.

Custom object parameters are completed step by step. Each `Tab` advances the currently active value instead of inserting the entire object at once.

```text
/test.setitem { ItemId: "", DisplayName: "", Rarity: Common, Level: 1 }
```

Autocomplete understands nested objects, lists and dictionaries:

```text
/test.setitem { MainStat: { StatName: "Attack", Amount: 25.5, IsPercent: false }, BonusStats: [{ StatName: "Crit", Amount: 10, IsPercent: true }], Materials: { "Iron": 10 } }
```

The inline ghost renderer supports multiline input. When a suggestion wraps to the next line, the input height is measured using the full suggestion so the ghost stays visible.

For instance commands, active targets are suggested after the command name:

```text
/enemy.damage 
```

If active `EnemyDebugCommands` instances exist, the suggestion list shows their GameObject names. Press `Tab` to insert the selected target:

```text
/enemy.damage "Goblin_01"
```

When multiple active targets have the same GameObject name, NeoConsolePlus appends an object identifier to keep the target unambiguous.

## Runtime overlay

The runtime overlay is available in the Editor while playing and in Development Builds.

Default shortcut:

```text
Backquote (`) or F1
```

The overlay shows recent logs and a command input. It uses the same command syntax, autocomplete, command history and navigation shortcuts as the Editor window. Plain arrow keys stay dedicated to autocomplete navigation, while command history uses the explicit modifier shortcuts described above.

## Runtime overlay auto creation

By default, NeoConsolePlus creates the runtime overlay automatically in the Editor and Development Builds.

To disable or re-enable this behavior, open:

```text
Tools > NeoUtils > NeoConsolePlus > Open Settings
```

Then toggle:

```text
Automatically create runtime overlay
```

When this option is disabled, NeoConsolePlus adds this scripting define symbol to the active build target:

```text
NEO_CONSOLEPLUS_DISABLE_RUNTIME_OVERLAY
```

This is useful when you only want the Editor console or when you prefer to show the overlay manually:

```csharp
NeoConsole.ShowRuntimeOverlay();
NeoConsole.HideRuntimeOverlay();
NeoConsole.ToggleRuntimeOverlay();
```

## Optional code API

Execute a command from your own code:

```csharp
using Neo.ConsolePlus;

bool success = NeoConsole.Execute("/log.test \"hello\"", out string message);
```

`NeoConsole.Execute` safely returns `false` in regular release builds.

## Logs

NeoConsolePlus listens to Unity logs, so messages written with `Debug.Log`, `Debug.LogWarning`, `Debug.LogError` and `NeoDebug` appear in NeoConsole+.

The main log list intentionally truncates long entries to keep the console readable. Selecting a log shows the full message in the details panel. Tooltips are shown only when the visible text is actually truncated.

The `Clear` button clears NeoConsolePlus captured logs. In the Unity Editor, it also attempts to clear the standard Unity Console window.

Compiler diagnostics may reappear if the underlying script errors or warnings still exist in the project.

## NeoDebug

NeoDebug is included with NeoConsolePlus.

```csharp
using Neo.Debugging;

NeoDebug.Log("Message");
NeoDebug.Warning("Warning");
NeoDebug.Error("Error");
```

NeoDebug can format serializable custom objects:

```csharp
NeoDebug.Log(new StatData
{
    StatName = "Attack",
    Amount = 25.5f,
    IsPercent = false
});
```

Regular release builds strip NeoDebug calls because the methods use conditional compilation.

## Performance notes

NeoConsolePlus is designed for Editor and Development Build workflows.

Current performance-oriented behavior:

- registered command list is cached after registry refresh;
- serializable field reflection is cached per type;
- active `MonoBehaviour` target lookup is cached briefly to avoid scanning large scenes every repaint;
- runtime logs are capped by `NeoConsoleLogBuffer.MaxEntries`;
- runtime overlay shows a limited number of recent logs;
- runtime overlay and command execution code are not compiled into regular release builds.

If your project has very large scenes, avoid relying on target autocomplete for thousands of active objects at the same time. Prefer specific debug manager commands or `.All` variants for bulk operations.

## Internal architecture

The codebase is organized around a few responsibilities:

```text
Runtime/
  NeoCommandRegistry        discovers and executes commands
  NeoCommandAutoComplete    resolves command/argument suggestions
  NeoCommandTypeUtility     shared type/reflection helpers with caching
  NeoCommandTargetCache     cached active MonoBehaviour target lookup
  NeoConsoleTextUtility     shared text, ghost and truncation helpers
  NeoConsoleLogBuffer       captured Unity/NeoDebug logs
  NeoRuntimeOverlay         development-build overlay UI

Editor/
  NeoConsoleWindow          dockable Editor UI
  NeoConsolePlusSettings    Editor-only preferences/configuration

NeoDebug/
  NeoDebug                  release-safe logging facade
  NeoDebugFormatter         object/value formatter
```

The user-facing API remains small, while shared runtime helpers reduce duplication between the Editor console and runtime overlay.

## Troubleshooting

### My command does not appear

Check that:

- the method has `[NeoCommand("command.name")]`, `[NeoCommandEditorOnly("command.name")]` or `[NeoCommandRuntimeOnly("command.name")]`;
- the command name does not start with `/` and does not contain spaces;
- you are opening the correct console for the command scope;
- the project compiled without errors;
- the command method is not generic;
- instance commands are declared on a `MonoBehaviour`;
- parameter types are supported.

### My instance command asks for a target

This happens when more than one active instance exists. Use the GameObject name:

```text
/enemy.kill "Goblin_01"
```

Or use the `.All` variant:

```text
/enemy.kill.All
```

### The runtime overlay does not open

Check that:

- you are in Play Mode or a Development Build;
- the shortcut was not changed in NeoConsole+ Settings;
- `NEO_CONSOLEPLUS_DISABLE_RUNTIME_OVERLAY` is not defined if you expect automatic overlay creation.

### Autocomplete looks stale for scene targets

Target lookup is cached for a short interval to reduce scene scans in large projects. Wait a fraction of a second or keep typing; the cache refreshes automatically.

## Built-in commands

NeoConsolePlus includes a small set of built-in commands for debug workflows:

| Command group | Commands | Scope |
| --- | --- | --- |
| Core | `/help`, `/clear`, `/version` | Editor Console and Runtime Console |
| Time | `/neo.time.info`, `/neo.time.scale <value>`, `/neo.time.pause`, `/neo.time.resume`, `/neo.time.fixed_delta <value>` | Runtime Console only |
| Scene | `/neo.scene.current`, `/neo.scene.list`, `/neo.scene.reload`, `/neo.scene.load <scene name>`, `/neo.scene.load_index <index>` | Editor Console and Runtime Console |
| FPS | `/neo.fps.show`, `/neo.fps.hide`, `/neo.fps.toggle`, `/neo.fps.info` | Runtime Console only |

`/help` only lists commands available in the current console context. Runtime-only commands, such as `/neo.fps.show`, are not listed or executable from the Editor Console.

`/neo.scene.load` suggestions are populated from the scenes registered in Unity Build Settings. Scene names containing spaces are suggested with quotes automatically. Runtime suggestions only include enabled Build Settings scenes; Edit Mode suggestions include all registered scenes so they can be opened in the Editor.

The `neo.` command prefix is reserved for NeoConsolePlus built-ins. User commands starting with `neo.` are ignored to avoid conflicts with tool commands.

## DebugCommands example generator

Open `Tools > NeoUtils > NeoConsolePlus > Open Settings` and use the `Generate DebugCommands.cs` button in the Example section to create `Assets/DebugCommands.cs`. After creating the file, NeoConsolePlus selects and pings the script in the Project window.
