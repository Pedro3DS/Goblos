# NeoDebug

NeoDebug is the lightweight logging API included with NeoConsolePlus.

```csharp
using Neo.Debugging;

NeoDebug.Log("Message");
NeoDebug.Warning("Warning message");
NeoDebug.Error("Error message");
```

NeoDebug logs appear in Unity's Console and in NeoConsole+.

The prefix color can be configured in NeoConsole+:

```text
Tools > NeoUtils > NeoConsolePlus > Open Settings
```

NeoDebug calls are stripped from regular release builds. They are available only in the Editor and Development Builds.
