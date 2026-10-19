using HarmonyLib;
using OS.Items;
using UnityEngine;

namespace InventoryTweaks.Patches
{
    /// <summary>
    /// Items produced by an item action (Break, Slaughter, ...) used on an item inside a container
    /// that do not fit into it go to the player's inventory instead of being dropped on the ground.
    /// Only what fits nowhere is dropped, at the same spot as in vanilla.
    /// Crafting and harvesting from a station / garden UI are handled by the game itself
    /// (Storage.AddItem with overflowToPlayer), so they are not touched here.
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
