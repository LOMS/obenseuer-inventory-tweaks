using System.Linq;
using HarmonyLib;

namespace InventoryTweaks.Patches
{
    /// <summary>
    /// The base game lets an open garden / animal cage use the player's inventory (and backpack)
    /// as part of its input (<c>Growing.ExtraInputSlots</c>): recipe list, multiplier, ingredient
    /// display and the removal on planting all see those stacks. The only thing left to the mod is
    /// the order of consumption: vanilla takes the garden's own stacks first and then the
    /// inventory in slot order; here the inventory stacks are consumed worst quality first.
    /// </summary>
    [HarmonyPatch(typeof(Growing), "StartGrow",
        typeof(Recipe), typeof(int), typeof(GrowingSpot), typeof(ItemStack[]))]
    internal static class StartGrowQualityOrderPatch
    {
        private static void Prefix(Growing __instance, ref ItemStack[] items)
        {
            // Callers that pass their own stacks (quick planting in the world) are left alone
            if (items != null || CraftingBase.currentCraftingBase != __instance
                || __instance.InputOutputStorage == null)
            {
                return;
            }

            ItemStack[] all = __instance.InputSlots;
            int own = __instance.InputOutputStorage.storageSlots.Length;
            if (all.Length <= own)
            {
                return;
            }

            // Garden stacks keep their place; the rest is sorted by quality (stable, so equal
            // stacks keep their inventory order). RemoveItems takes the first matching stacks.
            items = all.Take(own)
                .Concat(all.Skip(own).OrderBy(stack => stack.GetQualitydata()?.quality ?? 0f))
                .ToArray();
        }
    }
}
