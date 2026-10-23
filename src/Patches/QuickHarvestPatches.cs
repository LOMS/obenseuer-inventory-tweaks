using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace InventoryTweaks.Patches
{
    /// <summary>
    /// Shift + Click on a plant in the progress grid of a farm (garden, terrarium, aquarium,
    /// animal cage; any Growing whose UI allows harvesting) harvests every ripe plant from the
    /// clicked one to the end of the grid (row by row), straight into the player's inventory.
    /// "Ripe" = past the growing stage: spoiling / spoiled, or fruiting with fruits ready.
    /// Each plant goes through the game's own Growing.Harvest; only the destination of the
    /// produced items is redirected (see HarvestToPlayerPatch).
    /// </summary>
    internal static class QuickHarvest
    {
        private static readonly AccessTools.FieldRef<ProgressSlot, Growing> ActiveGrowing =
            AccessTools.FieldRefAccess<ProgressSlot, Growing>("activeGrowing");

        private static readonly AccessTools.FieldRef<ProgressSlot, GameObject> CurrentPlant =
            AccessTools.FieldRefAccess<ProgressSlot, GameObject>("currentPlant");

        private static bool _active;
        private static bool _full;
        private static bool _soundPlayed;

        private static bool ShiftHeld => Input.GetKey(KeyCode.LeftShift);

        /// <summary>True while a quick harvest runs: the farm's own storage must be skipped.</summary>
        internal static bool Active => _active;

        /// <summary>Called by the redirect when the player's inventory could not take everything.</summary>
        internal static void MarkFull() => _full = true;

        internal static bool IsFarmStorage(Storage storage)
        {
            var growing = CraftingBase.currentCraftingBase as Growing;
            return growing != null && storage == (growing.OutputStorage ?? growing.InputOutputStorage);
        }

        /// <summary>Only the first pickup sound of a quick harvest is played.</summary>
        internal static bool AllowPickupSound()
        {
            if (!_active || !_soundPlayed)
            {
                _soundPlayed |= _active;
                return true;
            }
            return false;
        }

        /// <summary>Returns true when the click was handled and vanilla must be skipped.</summary>
        public static bool TryHarvest(ProgressSlot slot, PointerEventData eventData)
        {
            if (_active || !ShiftHeld || (eventData != null && eventData.button != PointerEventData.InputButton.Left))
            {
                return false;
            }
            Growing growing = ActiveGrowing(slot);
            GameObject clicked = CurrentPlant(slot);
            // Slots of farms with manual harvest have no activeGrowing: vanilla does nothing there
            if (growing == null || clicked == null || CraftingBase.currentCraftingBase != growing)
            {
                return false;
            }

            // Same order as GrowingPanel.UpdateItemsInProgress fills the grid cells
            var plants = new List<GameObject>();
            foreach (GrowingSpot spot in growing.GrowingSlots)
            {
                if (spot != null && spot.Plant != null)
                {
                    plants.Add(spot.Plant);
                }
            }
            int start = plants.IndexOf(clicked);
            if (start < 0)
            {
                return true;
            }

            // Decide before harvesting: a harvest changes the state of the plants
            List<GameObject> ripe = plants.Skip(start).Where(IsRipe).ToList();
            if (ripe.Count == 0)
            {
                return true;
            }

            _active = true;
            _full = false;
            _soundPlayed = false;
            try
            {
                foreach (GameObject plant in ripe)
                {
                    if (_full)
                    {
                        break;
                    }
                    if (plant != null)
                    {
                        growing.Harvest(plant);
                    }
                }
            }
            finally
            {
                _active = false;
            }
            if (_full)
            {
                Inventory.instance.inventoryPanelUI.InventoryFull();
            }
            // Handled either way: Shift + Click never touches an unripe plant (vanilla would uproot it)
            return true;
        }

        /// <summary>Mirrors the cases of Growing.Harvest(GameObject), but only for finished plants.</summary>
        private static bool IsRipe(GameObject plant)
        {
            grow growth = plant != null ? plant.GetComponent<grow>() : null;
            if (growth == null)
            {
                return plant != null && plant.GetComponentInChildren<CollectibleItem>() != null;
            }
            switch (growth.GetCurrentStage())
            {
                case ProcessStage.Growing:
                    return false;
                case ProcessStage.GrowingFruits:
                    // Without ripe fruits vanilla would harvest (and destroy) the whole plant
                    return growth.VisibleItemsReadyToHarvest() > 0;
                default:
                    return true;
            }
        }
    }

    [HarmonyPatch(typeof(ProgressSlot), nameof(ProgressSlot.OnPointerClick))]
    internal static class ProgressSlotClickPatch
    {
        private static bool Prefix(ProgressSlot __instance, PointerEventData eventData)
        {
            return !QuickHarvest.TryHarvest(__instance, eventData);
        }
    }

    /// <summary>
    /// During a quick harvest the produced items go to the player's inventory first, not into
    /// the farm's storage. What does not fit continues along the vanilla path (farm storage,
    /// then overflow) and stops the harvest.
    /// </summary>
    [HarmonyPatch(typeof(Storage), "AddToSlots")]
    internal static class HarvestToPlayerPatch
    {
        private static bool Prefix(Storage __instance, Item item, ref int amount, int owner, object[] meta, ref bool __result)
        {
            if (!QuickHarvest.Active || !QuickHarvest.IsFarmStorage(__instance))
            {
                return true;
            }
            int remaining = ItemOperations.AddItemAndReturnRemaining(item, owner, null, meta, amount);
            if (remaining <= 0)
            {
                __result = true;
                return false;
            }
            QuickHarvest.MarkFull();
            amount = remaining;
            return true;
        }
    }

    /// <summary>Collapses the repeated item pickup sounds of a quick harvest into one.</summary>
    [HarmonyPatch(typeof(ItemSounds), nameof(ItemSounds.PlaySound), typeof(PickupSoundName))]
    internal static class HarvestSoundPatch
    {
        private static bool Prefix()
        {
            return QuickHarvest.AllowPickupSound();
        }
    }
}
