# Inventory Tweaks — a mod for Obenseuer

Reply in the same language as the user's prompt. Code comments in English.

## Goal
Inventory UI mod: Shift+Click on an item moves it between inventories
(backpack ↔ storage, etc.), duplicating the vanilla hold-to-move.
- Shift+LMB moves the whole stack, Shift+RMB moves one item, instantly.
- Vanilla Shift ("move one" on hold / double-click) is disabled.
- No progress indicator.

## Environment
- Game: Obenseuer, Unity 6000.0.77f1, Mono, Windows x64
- GameDir: `C:\Program Files (x86)\Steam\steamapps\common\Obenseuer`
  (set in `Config.Build.user.props`; template: `Config.Build.user.props.template`)
- Loader: BepInEx 6.0.0-be.788 (Unity.Mono), HarmonyX 2.10.2;
  log: `<GameDir>\BepInEx\LogOutput.log`
- Unity Explorer (F7 in game) — used by the user themselves
- .NET SDK 10.0.401, VS Code
- Build: `net472` against the game's BCL from `Obenseuer_Data\Managed`
  (no NuGet); all references via HintPath relative to `GameDir`.
- Plugin API: `BepInEx.Unity.Mono.BaseUnityPlugin` + `[BepInPlugin]`.

## Layout
- `src\` — mod code
- `reference\notes.md` — research notes (committed)
- `reference\decompiled\` — decompiled game code (NOT committed, do NOT copy into `src\`)
- `tools\export-game-code.bat|.ps1` — refresh the decompiled code (requires `ilspycmd`)
- `tools\build.bat`, `tools\deploy.bat` — shortcuts for the commands below
  (deploy refuses to run while the game is running)

## Commands
- Build: `dotnet build`
- Deploy: `dotnet build -t:Deploy` → `<GameDir>\BepInEx\plugins\InventoryTweaks\`
  (only with the user's permission and with the game closed)

## Rules
- Do not modify, delete or overwrite game files. The only exception is the
  `BepInEx\plugins` folder when deploying.
- The game must be closed before deploying; deploy only after the user's "ok".
- Ask before adding any dependencies (NuGet, DLLs).
- Do not copy large chunks of game code outside `reference\`.
- Workflow: research → notes (`reference\notes.md`) → plan →
  user approval → code.
- If data from the running game is needed (UI hierarchy, field values), tell
  the user what to look at in Unity Explorer and wait for the answer.
- When in doubt, ask instead of guessing.
