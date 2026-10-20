using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using OS.Items;
using UnityEngine;

namespace InventoryTweaks.Patches
{
    /// <summary>
    /// The player's main inventory acts as a continuation of the open garden / animal cage
    /// storage for planting. Vanilla only looks at the garden's own storage, so:
    /// reads (recipe list, recipe multiplier, ingredient display) count the inventory too,
    /// and right before Growing.StartGrow the missing ingredients are really moved into
    /// the garden storage, where the vanilla RemoveItems finds them.
    /// Only for plain item inputs; recipes with item groups, liquids or variants
    /// behave as in vanilla.
    /// </summary>
    internal static class PlantFromInventory
    {
        private static readonly AccessTools.FieldRef<CraftingBase, Recipe> SelectedRecipe =
            AccessTools.FieldRefAccess<CraftingBase, Recipe>("selectedRecipe");

        /// <summary>The garden if it is the open one (so Autofarm and the like are untouched).</summary>
        internal static Growing OpenGrowing(CraftingBase crafting)
        {
            var growing = crafting as Growing;
            return growing != null && CraftingBase.currentCraftingBase == growing
                && growing.InputOutputStorage != null && Inventory.instance != null
                ? growing
                : null;
        }

        internal static Recipe Selected(Growing growing) => SelectedRecipe(growing);

        /// <summary>Plain item inputs only (the cases the extension is written for).</summary>
        internal static bool IsSimple(Recipe recipe)
        {
            if (recipe == null)
            {
                return false;
            }
            if (recipe.Input == null)
            {
                recipe.SetItems();
            }
            if (recipe.Input == null || recipe.Input.Count == 0 || recipe.variantItemCategory != VariantCategory.None)
            {
                return false;
            }
            // useAmmoAsInput is set on every garden recipe; ammo stacks are skipped when counting instead
            if (recipe.metaSettings != null && recipe.metaSettings.useLiquidContainers)
            {
                return false;
            }
            foreach (KeyValuePair<Item, float> input in recipe.Input)
            {
                if (input.Value <= 0f || input.Key.GetMetaOfType<ItemGroupData>() != null
                    || input.Key.Categories.Contains("Liquid") || input.Key.Categories.Contains("ItemGroup"))
                {
                    return false;
                }
            }
            return true;
        }

        private static int AmountIn(ItemStack[] stacks, int itemId)
        {
            int total = 0;
            foreach (ItemStack stack in stacks)
            {
                if (stack != null && stack.itemId == itemId && stack.itemAmount > 0)
                {
                    total += stack.itemAmount;
                }
            }
            return total;
        }

        /// <summary>Non-empty inventory stack that counts as a plain ingredient (ammo stacks do not).</summary>
        private static bool IsUsable(SlotController slot)
        {
            return ItemKind.HasItem(slot) && slot.itemStack.GetMetaOfType<ItemAmmodata>() == null;
        }

        internal static int InventoryAmount(int itemId)
        {
            int total = 0;
            foreach (SlotController slot in Inventory.instance.Slots)
            {
                if (IsUsable(slot) && slot.itemStack.itemId == itemId)
                {
                    total += slot.itemStack.itemAmount;
                }
            }
            return total;
        }

        /// <summary>Garden stacks followed by the inventory's non-empty stacks.</summary>
        internal static ItemStack[] CombinedStacks(Growing growing)
        {
            var stacks = new List<ItemStack>(growing.InputOutputStorage.storageSlots);
            foreach (SlotController slot in Inventory.instance.Slots)
            {
                if (IsUsable(slot))
                {
                    stacks.Add(slot.itemStack);
                }
            }
            return stacks.ToArray();
        }

        /// <summary>How many times the recipe can be made from the garden storage + inventory.</summary>
        internal static int Multiplier(Growing growing, Recipe recipe)
        {
            int result = int.MaxValue;
            foreach (KeyValuePair<Item, float> input in recipe.Input)
            {
                int have = AmountIn(growing.InputOutputStorage.storageSlots, input.Key.ID) + InventoryAmount(input.Key.ID);
                result = Mathf.Min(result, Mathf.FloorToInt(have / input.Value));
            }
            return Mathf.Max(result, 0);
        }

        /// <summary>
        /// Moves the ingredients for <paramref name="amount"/> plantings that the garden storage
        /// lacks from the inventory into it, worst quality first; returns how many plantings the garden storage
        /// can now serve (less than <paramref name="amount"/> when the move was not possible).
        /// </summary>
        internal static int PullIngredients(Growing growing, Recipe recipe, int amount)
        {
            Storage garden = growing.InputOutputStorage;
            int possible = amount;
            foreach (KeyValuePair<Item, float> input in recipe.Input)
            {
                int id = input.Key.ID;
                int missing = Mathf.CeilToInt(input.Value * amount) - AmountIn(garden.storageSlots, id);
                // Worst quality first (stable, so equal stacks keep their inventory order)
                IEnumerable<SlotController> sources = Inventory.instance.Slots
                    .Where(source => IsUsable(source) && source.itemStack.itemId == id
                        && recipe.AllowedToUse(id, source.itemStack.ownerId))
                    .OrderBy(source => source.itemStack.GetQualitydata()?.quality ?? 0f)
                    .ToList();
                foreach (SlotController source in sources)
                {
                    if (missing <= 0)
                    {
                        break;
                    }
                    missing -= StackButton.MoveToContainer(source, garden.Slots, missing);
                }
                possible = Mathf.Min(possible, Mathf.FloorToInt(AmountIn(garden.storageSlots, id) / input.Value));
            }
            return Mathf.Max(possible, 0);
        }
    }

    /// <summary>Recipes whose ingredients are in the inventory show up as normal (plantable) buttons.</summary>
    [HarmonyPatch(typeof(CraftingBase), "CompareRecipeToItems")]
    internal static class CompareRecipePatch
    {
        private static void Prefix(CraftingBase __instance, Recipe recipe, ref ItemStack[] items)
        {
            if (items != null)
            {
                return;
            }
            Growing growing = PlantFromInventory.OpenGrowing(__instance);
            if (growing != null && PlantFromInventory.IsSimple(recipe))
            {
                items = PlantFromInventory.CombinedStacks(growing);
            }
        }
    }

    /// <summary>The recipe multiplier (slider maximum, Plant max) counts the inventory too.</summary>
    [HarmonyPatch(typeof(CraftingBase), nameof(CraftingBase.GetRecipeMultiplier))]
    internal static class RecipeMultiplierPatch
    {
        private static void Postfix(CraftingBase __instance, Recipe recipe, ref int __result)
        {
            Growing growing = PlantFromInventory.OpenGrowing(__instance);
            if (growing != null && PlantFromInventory.IsSimple(recipe))
            {
                __result = PlantFromInventory.Multiplier(growing, recipe);
            }
        }
    }

    /// <summary>Ingredient availability (green / red) in the recipe info counts the inventory too.</summary>
    [HarmonyPatch(typeof(Storage), nameof(Storage.CheckForItem))]
    internal static class CheckForItemPatch
    {
        private static void Postfix(Storage __instance, int id, ref float missing,
            bool liquid, bool itemGroup, ref bool __result)
        {
            if (__result || liquid || itemGroup)
            {
                return;
            }
            Growing growing = PlantFromInventory.OpenGrowing(CraftingBase.currentCraftingBase);
            if (growing == null || growing.InputOutputStorage != __instance)
            {
                return;
            }
            missing -= PlantFromInventory.InventoryAmount(id);
            __result = missing <= 0f;
        }
    }

    /// <summary>Moves the missing ingredients into the garden right before planting.</summary>
    [HarmonyPatch(typeof(Growing), nameof(Growing.StartGrow))]
    internal static class StartGrowPatch
    {
        private static bool Prefix(Growing __instance, ref int amount)
        {
            Growing growing = PlantFromInventory.OpenGrowing(__instance);
            Recipe recipe = growing != null ? PlantFromInventory.Selected(growing) : null;
            if (recipe == null || amount <= 0 || !PlantFromInventory.IsSimple(recipe))
            {
                return true;
            }
            // The lists are not refreshed when the inventory changes, so re-check what is really there
            amount = PlantFromInventory.PullIngredients(growing, recipe, amount);
            // Vanilla plants one even for amount 0, so skip it when there is nothing to plant
            if (amount > 0)
            {
                return true;
            }
            growing.CheckItems();
            return false;
        }
    }
}
