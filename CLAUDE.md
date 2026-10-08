# Inventory Tweaks — a mod for Obenseuer

Reply in the same language as the user's prompt. Code comments in English.

## Features
Inventory quality-of-life mod. User-facing description: `README.md`;
research and design decisions: `reference\notes.md`.
- Quick transfer between inventories (`src\Patches\ShiftClickPatches.cs`):
  Shift+LMB — whole stack, Shift+RMB — one item, Shift+Ctrl+LMB — every stack of
  that item in the container. Vanilla Shift ("move one" on hold / double-click)
  is disabled.
- Bulk item actions (`src\Patches\BulkActionPatches.cs`): Shift+Break breaks the
  whole stack; Shift+Slaughter slaughters all animals of the same kind in the
  container (click-again confirmation, "Your Cat" excluded); button labels change
  while Shift is held; repeated action sounds are collapsed.
- Overflow to player (`src\Patches\OverflowToPlayerPatches.cs`): results of item
  actions in containers, crafting with the station UI open and harvesting from
  the garden / animal cage UI go to the player's inventory instead of the ground
  when the container is full.
- All moves/actions reuse the game's own methods (`QuickMove`, `ItemAction.Invoke`,
  ...) so vanilla checks are never bypassed.

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
- `src\Plugin.cs` — entry point (Harmony `PatchAll`, per-frame button labels)
- `src\Patches\` — Harmony patches, one file per feature
- `reference\notes.md` — research notes (committed)
- `reference\decompiled\` — decompiled game code (NOT committed, do NOT copy into `src\`)
- `tools\export-game-code.bat|.ps1` — refresh the decompiled code (requires `ilspycmd`)
- `tools\build.bat`, `tools\deploy.bat` — shortcuts for the commands below
  (deploy refuses to run while the game is running)
- `LICENSE` — MIT; GitHub: https://github.com/LOMS/obenseuer-inventory-tweaks

## Commands
- Build: `dotnet build`
- Deploy: `dotnet build -t:Deploy` → `<GameDir>\BepInEx\plugins\InventoryTweaks\`
  (only with the user's permission and with the game closed)
- Version lives in two places: `<Version>` in `InventoryTweaks.csproj` and
  `Plugin.Version` in `src\Plugin.cs` — keep them in sync.

## Rules
- Do not modify, delete or overwrite game files. The only exception is the
  `BepInEx\plugins` folder when deploying.
- The game must be closed before deploying; deploy only after the user's "ok".
- Ask before adding any external dependencies (NuGet packages, third-party DLLs).
  References to DLLs already shipped with the game or BepInEx (`Managed\`,
  `BepInEx\core\`) are fine.
- Do not copy large chunks of game code outside `reference\`.
- Workflow: research → notes (`reference\notes.md`) → plan →
  user approval → code.
- If data from the running game is needed (UI hierarchy, field values), tell
  the user what to look at in Unity Explorer and wait for the answer.
- When in doubt, ask instead of guessing.
