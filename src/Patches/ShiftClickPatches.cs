using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace InventoryTweaks.Patches
{
    /// <summary>
    /// Shift+LMB moves the whole stack, Shift+RMB moves one item, instantly.
    /// Shift+Ctrl+LMB moves every stack of the same item in the same container.
    /// The move goes through the game's own ItemData.QuickMove, so all vanilla
    /// checks (trade, categories, stack limits, equip when no storage is open) apply.
    /// </summary>
    [HarmonyPatch(typeof(ItemData), nameof(ItemData.OnPointerDown))]
    internal static class ShiftClickPatch
    {
        private static bool Prefix(ItemData __instance, PointerEventData eventData)
        {
            if (!Input.GetKey(KeyCode.LeftShift) || eventData == null)
            {
                return true;
            }

            bool moved;
            switch (eventData.button)
            {
                case PointerEventData.InputButton.Left:
                    moved = Input.GetKey(KeyCode.LeftControl) && CanMoveAllOfType()
                        ? MoveAllOfType(__instance)
                        : MoveStack(__instance, __instance.amount);
                    break;
                case PointerEventData.InputButton.Right:
                    moved = MoveStack(__instance, 1);
                    break;
                default:
                    return true;
            }

            // Same feedback as the vanilla hold-to-move path (ItemData.HoldMove); once per click
            if (moved)
            {
                __instance.item?.PlaySound();
            }

            // Skip vanilla: its Shift handling ("move one" double-click) is replaced
            return false;
        }

        private static bool MoveStack(ItemData itemData, int amount)
        {
            return amount > 0 && itemData.QuickMove(amount);
        }

        /// <summary>
        /// Bulk move only for a plain storage transfer. Trade, liquid storage and bottle
        /// recycling have special QuickMove handling; with no storage open QuickMove equips.
        /// In those cases Shift+Ctrl+Click behaves like Shift+Click.
        /// </summary>
        private static bool CanMoveAllOfType()
        {
            return Inventory.instance.ForeignSlots != null
                && TradePanel.instance.activeTrade == null
                && LiquidStorage.currentStorage == null
                && !BottleRecyclingPanel.instance.open;
        }

        private static bool MoveAllOfType(ItemData clicked)
        {
            SlotController origin = clicked.slotController;
            if (origin == null)
            {
                return false;
            }

            int itemId = origin.itemStack.itemId;
            SlotController[] slots = origin.Siblings ?? new[] { origin };
            bool movedAny = false;

            foreach (SlotController slot in slots)
            {
                if (slot == null || slot.itemStack.itemId != itemId || slot.itemStack.itemAmount <= 0)
                {
                    continue;
                }
                // QuickMove needs the item's UI object; slots without one are skipped
                ItemData data = slot.slot != null ? slot.slot.GetItemItemData() : null;
                if (data == null)
                {
                    continue;
                }
                if (!data.QuickMove(data.amount))
                {
                    // Target is full: stop instead of repeating the "full" message per stack
                    break;
                }
                movedAny = true;
            }
            return movedAny;
        }
    }

    /// <summary>
    /// Removes LeftShift from ItemData.GetMoveMode so vanilla hold/double-click
    /// no longer treat Shift as "move one". Ctrl (half) and RMB (one) still work.
    /// </summary>
    [HarmonyPatch(typeof(ItemData), "GetMoveMode")]
    internal static class DisableVanillaShiftPatch
    {
        private static readonly MethodInfo InputGetKey =
            AccessTools.Method(typeof(Input), nameof(Input.GetKey), new[] { typeof(KeyCode) });

        private static readonly MethodInfo GetKeyIgnoringShift =
            AccessTools.Method(typeof(DisableVanillaShiftPatch), nameof(GetKeyIgnoringShiftImpl));

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int replaced = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(InputGetKey))
                {
                    instruction.operand = GetKeyIgnoringShift;
                    replaced++;
                }
                yield return instruction;
            }

            if (replaced == 0)
            {
                Plugin.Log.LogWarning("GetMoveMode: Input.GetKey call not found, vanilla Shift is still active");
            }
        }

        private static bool GetKeyIgnoringShiftImpl(KeyCode key)
        {
            return key != KeyCode.LeftShift && Input.GetKey(key);
        }
    }
}
