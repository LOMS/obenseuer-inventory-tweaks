using System.Collections.Generic;
using HarmonyLib;
using OS.Items;
using UnityEngine;

namespace InventoryTweaks.Patches
{
    /// <summary>
    /// Items produced by the player's own actions that do not fit into a container
    /// go to the player's inventory instead of being dropped on the ground:
    /// - item actions (Break, Slaughter, ...) used on an item inside a container;
    /// - crafting at a station whose UI the player has open;
    /// - harvesting a garden / animal cage from its UI.
    /// Only what fits nowhere is dropped, at the same spot as in vanilla.
    /// </summary>
    internal static class OverflowToPlayer
    {
        private static int _activeScopes;

        public static bool Active => _activeScopes > 0;

        public static void Begin()
        {
            _activeScopes++;
        }

        public static void End()
        {
            if (_activeScopes > 0)
            {
                _activeScopes--;
            }
        }

        public static bool IsContainerSlot(SlotController slot)
        {
            return slot != null && !Inventory.instance.InPlayerSlot(slot);
        }
    }

    [HarmonyPatch(typeof(ItemConsumable), nameof(ItemConsumable.Invoke), typeof(ItemData), typeof(bool))]
    internal static class ConsumableInvokeScopePatch
    {
        private static void Prefix(ItemData itemData, out bool __state)
        {
            __state = itemData != null && OverflowToPlayer.IsContainerSlot(itemData.slotController);
            if (__state)
            {
                OverflowToPlayer.Begin();
            }
        }

        // Finalizer runs even if the method throws, so the scope never leaks
        private static void Finalizer(bool __state)
        {
            if (__state)
            {
                OverflowToPlayer.End();
            }
        }
    }

    [HarmonyPatch(typeof(CraftingBase), "CreateItems",
        typeof(Recipe), typeof(List<KeyValuePair<ItemStack, int>>), typeof(int), typeof(bool), typeof(CraftingBase.CraftingInputData), typeof(int))]
    internal static class CraftingScopePatch
    {
        private static void Prefix(CraftingBase __instance, out bool __state)
        {
            // Only while the player has this station open; background crafting keeps vanilla behavior
            __state = __instance != null && CraftingBase.currentCraftingBase == __instance;
            if (__state)
            {
                OverflowToPlayer.Begin();
            }
        }

        private static void Finalizer(bool __state)
        {
            if (__state)
            {
                OverflowToPlayer.End();
            }
        }
    }

    /// <summary>
    /// Harvest from a garden / animal cage UI: the Harvest button (GrowingPanel.PressHarvestButton)
    /// and clicking a progress slot (ProgressSlot). Both are player actions only.
    /// </summary>
    [HarmonyPatch]
    internal static class HarvestScopePatch
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Growing), nameof(Growing.Harvest), System.Type.EmptyTypes);
            yield return AccessTools.Method(typeof(Growing), nameof(Growing.Harvest), new[] { typeof(GameObject) });
        }

        private static void Prefix()
        {
            OverflowToPlayer.Begin();
        }

        private static void Finalizer()
        {
            OverflowToPlayer.End();
        }
    }

    [HarmonyPatch(typeof(SlotController), nameof(SlotController.AddItemAndReturnRemaining))]
    internal static class AddItemOverflowPatch
    {
        private static bool Prefix(
            SlotController __instance, Item item, int amount, int owner, object[] meta, bool dropItems,
            GameObject dropper, bool notify, int stackAmount, ItemOperations.DropItemValues dropItemValues,
            ItemOperations.ItemOperationSettings settings, ref int __result)
        {
            if (!dropItems || !OverflowToPlayer.Active || item == null || !OverflowToPlayer.IsContainerSlot(__instance))
            {
                return true;
            }

            // Vanilla add (target slot, then container siblings), but without dropping
            int remaining = __instance.AddItemAndReturnRemaining(
                item, amount, owner, meta, dropItems: false, dropper, notify, stackAmount, dropItemValues, settings);

            if (remaining > 0)
            {
                // Player inventory: main slots, character slots, backpack (vanilla rules)
                remaining = ItemOperations.AddItemAndReturnRemaining(item, owner, null, meta, remaining);
            }
            if (remaining > 0)
            {
                ItemOperations.DropItem(item, remaining, owner, meta, dropper, dropItemValues);
            }

            // Vanilla returns 0 when leftovers were dropped
            __result = 0;
            return false;
        }
    }
}
