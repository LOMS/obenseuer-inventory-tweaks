using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace InventoryTweaks.Patches
{
    /// <summary>
    /// Shift + Click on a recipe in a garden / animal cage list plants as many as possible
    /// right away: min(recipe multiplier, free spots) — the same as selecting the recipe,
    /// pressing Plant, moving the slider to max and pressing OK. Uses the game's own
    /// Growing.StartGrow, with the same conditions as the vanilla Plant button.
    /// </summary>
    internal static class PlantMax
    {
        private const string TooltipTitle = "Plant";

        private static bool ShiftHeld => Input.GetKey(KeyCode.LeftShift);

        /// <summary>The garden the button belongs to, if it is the open one.</summary>
        private static Growing GetGrowing(RecipeButton button)
        {
            var growing = button.activeCraftingBase as Growing;
            return growing != null && CraftingBase.currentCraftingBase == growing ? growing : null;
        }

        /// <summary>How many can be planted now (0 = vanilla Plant would be disabled or useless).</summary>
        public static int GetMaxAmount(RecipeButton button)
        {
            Growing growing = GetGrowing(button);
            Recipe recipe = button.recipe;
            if (growing == null || recipe == null || button.disabledButton)
            {
                return 0;
            }

            // Same enable conditions as the vanilla Plant button (RecipeInfoUI.ShowRecipe)
            RecipeInfoUI info = button.recipeInfo;
            if (info != null && (info.craftingDisabled || !RecipeInfoUI.CheckModifiers(recipe, info.modifiers)))
            {
                return 0;
            }

            int freeSpots = 0;
            foreach (GrowingSpot spot in growing.GrowingSlots)
            {
                if (spot != null && spot.Plant == null)
                {
                    freeSpots++;
                }
            }
            return Mathf.Min(growing.GetRecipeMultiplier(recipe), freeSpots);
        }

        /// <summary>Returns true when the click was handled.</summary>
        public static bool TryPlantMax(RecipeButton button)
        {
            if (!ShiftHeld || AmountSliderUI.instance == null || AmountSliderUI.instance.sliderIsVisible)
            {
                return false;
            }
            Growing growing = GetGrowing(button);
            if (growing == null)
            {
                return false;
            }

            int amount = GetMaxAmount(button);
            if (amount > 0)
            {
                // Same as the vanilla flow: select the recipe, then Plant with the slider's value
                growing.SetSelectedRecipe(button.recipe);
                growing.StartGrow(amount);
            }
            // Handled either way: Shift + Click never toggles the recipe info
            return true;
        }

        public static bool GetHint(RecipeButton button, out string title, out string details)
        {
            title = TooltipTitle;
            details = null;
            if (button == null || GetGrowing(button) == null || button.disabledButton)
            {
                return false;
            }
            int amount = GetMaxAmount(button);
            details = amount > 0
                ? $"Shift + Click: plant as many as possible ({amount})."
                : "Shift + Click: plant as many as possible (nothing to plant right now).";
            return true;
        }
    }

    [HarmonyPatch(typeof(RecipeButton), nameof(RecipeButton.OnPointerClick))]
    internal static class RecipeButtonClickPatch
    {
        private static bool Prefix(RecipeButton __instance, PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return true;
            }
            return !PlantMax.TryPlantMax(__instance);
        }
    }

    /// <summary>Tooltip on garden / cage recipe buttons (the list is rebuilt after each planting).</summary>
    [HarmonyPatch(typeof(RecipeListUI), nameof(RecipeListUI.CreateButton))]
    internal static class RecipeListCreateButtonPatch
    {
        private static void Postfix(RecipeListUI __instance)
        {
            if (GrowingPanel.instance == null || GrowingPanel.instance.growingPanelUI == null
                || GrowingPanel.instance.growingPanelUI.recipeListUI != __instance)
            {
                return;
            }

            // The button created last is the last child of the list
            RecipeButton button = LastRecipeButton(__instance);
            if (button != null && !button.disabledButton)
            {
                HintTooltip.Attach(button.gameObject,
                    (out string title, out string details) => PlantMax.GetHint(button, out title, out details));
            }
        }

        private static readonly AccessTools.FieldRef<RecipeListUI, System.Collections.Generic.List<GameObject>> RecipeButtons =
            AccessTools.FieldRefAccess<RecipeListUI, System.Collections.Generic.List<GameObject>>("recipeButtons");

        private static RecipeButton LastRecipeButton(RecipeListUI list)
        {
            System.Collections.Generic.List<GameObject> buttons = RecipeButtons(list);
            if (buttons == null || buttons.Count == 0 || buttons[buttons.Count - 1] == null)
            {
                return null;
            }
            return buttons[buttons.Count - 1].GetComponent<RecipeButton>();
        }
    }
}
