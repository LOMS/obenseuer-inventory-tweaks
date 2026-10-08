using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace InventoryTweaks.Patches
{
    /// <summary>
    /// Shift+LMB moves the whole stack, Shift+RMB moves one item, instantly.
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

            int amount;
            switch (eventData.button)
            {
                case PointerEventData.InputButton.Left:
                    amount = __instance.amount;
                    break;
                case PointerEventData.InputButton.Right:
                    amount = 1;
                    break;
                default:
                    return true;
            }

            // Same feedback as the vanilla hold-to-move path (ItemData.HoldMove)
            if (amount > 0 && __instance.QuickMove(amount))
            {
                __instance.item?.PlaySound();
            }

            // Skip vanilla: its Shift handling ("move one" double-click) is replaced
            return false;
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
