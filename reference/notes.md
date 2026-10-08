# Research notes

File paths are relative to `reference\decompiled\`. Only summaries and names
here; game code is not copied.

## Environment (verified)
- `BepInEx\core`: BepInEx.Core / BepInEx.Unity.Mono / BepInEx.Unity.Common
  6.0.0-be.788, 0Harmony 2.10.2 (HarmonyX). Built for .NET 3.5 (mscorlib 2.0).
- `Obenseuer_Data\Managed`: Assembly-CSharp references mscorlib 4.0 and
  netstandard 2.1; UnityEngine.InputLegacyModule and Unity.InputSystem are present.
- Plugin is deployed to `BepInEx\plugins\InventoryTweaks\` (v0.2.0).

## 1. Slot and item, mouse input
- `Slot.cs` — `Slot : MonoBehaviour` + `IPointerDownHandler`, `IPointerClickHandler`,
  `IDropHandler`, enter/exit. This is a grid cell.
  - `slotcontrol` (`SlotController`) — cell data; `playerSlot` — whether the cell
    belongs to the player or to a foreign container.
  - `OnPointerDown` (line ~636) just forwards the event to `ItemData.OnPointerDown`
    of the item in the cell. `OnPointerClick` only selects the slot.
- `ItemData.cs` — UI object of the item inside a slot (prefab
  `Inventory.instance.inventoryPanelUI.inventoryItem`, created in
  `Slot.SetSlotController` / `Slot.UpdateSlot`).
  - Implements begin/drag/end drag, pointer down/enter/exit/click.
  - `static currentHoverItemData` — set in `OnPointerEnter`, cleared in
    `Deactivate()` (`OnPointerExit`, `OnDestroy`).
  - `static globaldrag` — the item currently being dragged.

## 2. Hold-to-move
- `HoldController.cs` (singleton `HoldController.instance`):
  - `holdTimeLength` — `[SerializeField]`, defaults to `1f` (actual value in the
    scene not checked).
  - `Update()`: every frame calls `currentHoverItemData.HoldMove(false, 0f, ...)`
    (this **resets** the indicator fill to 0), then `UpdateHold` for Mouse0 and
    Mouse1.
  - `UpdateHold`: while the button is held, accumulates `Time.deltaTime`; after
    20 % of the threshold passes progress via `HoldMove(false, t/holdTimeLength)`;
    when the threshold is reached calls `HoldMove(true, ...)` (move) and, if
    `resetHold`, resets the timer (i.e. holding repeats the move every second).
  - `Holding()` — used in `ItemData.CantDrag()` so that holding does not turn
    into dragging.
- `ItemData.HoldMove(bool move, float value, out bool resetHold)` (line ~748):
  - picks the amount via `GetMoveMode()` (Full / Half / One);
  - `move == true` → `QuickMove(num)`; otherwise writes `value` into
    `progressIndicatorImage.fillAmount`.
- Double-click (`ItemData.OnPointerDown`, line ~568 → `DoubleClickController.DoubleClick`,
  0.5 s window) also calls `QuickMove`.
- **The method that actually moves:** `ItemData.QuickMove(int amount)` (line ~518):
  1. refuses if `itemDisabled`, `itemMoveDisabled`, `isDragging`, `globaldrag`,
     `!canMove`;
  2. special modes: `BottleRecyclingPanel.instance.open`,
     `LiquidStorage.currentStorage`, `TradePanel.instance.activeTrade`
     (trade → `Trade.TradeAndMove`);
  3. otherwise `slotController.MoveToOtherInventory(out moved, amount)`;
  4. **if `Inventory.instance.ForeignSlots == null` (no storage open), invokes the
     item's `ItemEquipable` actions** (i.e. a double-click with no storage open
     equips the item).
- **Target selection:** `SlotController.MoveToOtherInventory(out, amount)` (line ~639):
  - `slot.playerSlot` → `MoveToForeignInventory` → `Inventory.instance.ForeignSlots[0]`
    (if `ForeignSlots == null`, returns false and does nothing);
  - otherwise → `MoveToInventory` (target `null` = player inventory).
  - Inside: `ItemOperations.AddItemAndReturnRemaining(...)`, then shrinks the
    source stack; if nothing was moved — `InventoryFull()` / `StorageFull()`.
  - `ForeignSlots` is set by `Storage` on open (`Storage.cs` ~706/749),
    cleared on close (~792); also by `LiquidStorage`, `GrowingPanel`.

## 3. Input / Shift
- The game reads the **legacy `UnityEngine.Input`** (`Input.GetKey(KeyCode...)`,
  `GetMouseButtonDown`). The new Input System is used only in
  `ComputerControllerInput.cs` (the in-game computer).
- `InputManager.cs` — wrapper over key bindings (`GetKey("Walk")`, etc.) +
  `inputType` (Keyboard / Controller). Not used for mouse inventory input.
- **Vanilla Shift in the inventory:** `ItemData.GetMoveMode()` (line ~610):
  - `LeftControl` → Half (half the stack);
  - `LeftShift` **or** right mouse button → One (single item);
  - otherwise Full.
  So in vanilla, Shift+hold and Shift+double-click move **one item at a time**.
  Only `LeftShift` is checked (not `RightShift`).
- Gamepad/keyboard: `InventoryNavigationHandler` (~1017) — the `"Move Item"` key (F)
  while storage is open.

## 4. Existing indicator
- `ItemData.progressIndicatorImage` (`[SerializeField] Image`) — hold progress fill
  (`fillAmount`), driven only by `HoldMove` and reset in `Deactivate()`.
- `ItemData.progressIndicatorImage2` + `SetProgressIndicator2Value(float)` —
  a second indicator; **no callers found in game code** (looks unused).
- Fill type (Radial360 or other), sprite and color live in the prefab and are
  not visible from code (not checked; the indicator was dropped from scope).
- Note: `HoldController.Update` writes 0 into `progressIndicatorImage` of the
  hovered item every frame, so any custom value must be set **after** it
  (Postfix on `HoldController.Update` or `LateUpdate`).

## 5. Transfer checks (must not be bypassed)
All run inside `QuickMove` → `MoveToOtherInventory` →
`ItemOperations.AddItemAndReturnRemaining` → `SlotController.AddItemAndReturnRemaining`:
- `itemDisabled` / `itemMoveDisabled` flags, active drag;
- trade (`Trade.TradeAndMove`, money/prices), bottle recycling, liquids;
- `SlotController.OnDroppedCheck` (custom container checks);
- `CheckIfAllowed` — allowed/forbidden categories (by default `Storage` and
  `Unstorable` cannot be stored);
- `ItemStack.AddItemToPanel` — stack size, liquid/quality merging;
- `Siblings` — overflow into neighboring cells;
- for the player inventory: character slots, then the backpack
  (`BackpackStorage.instance.GetStorage()`); money (ID 0/1) → `Money.AddMoney`;
  theft (`CheckForAddedStolenItems`, `Crime`).
- **No weight system** found on these paths.
- Conclusion: reuse `ItemData.QuickMove(amount)` so all checks are preserved
  automatically.

## User decisions (v0.2.0)
- No circular indicator.
- Shift+LMB — whole stack, Shift+RMB — one item, instantly, via `QuickMove`.
- Vanilla Shift ("move one" in `GetMoveMode`) is disabled.
- With no storage open — same as vanilla (`QuickMove` equips the item).
- Left Shift only.
- Implementation: `src\Patches\ShiftClickPatches.cs` — Prefix on `ItemData.OnPointerDown`,
  Transpiler on `ItemData.GetMoveMode` (replaces `Input.GetKey` with a version that
  ignores LeftShift).
