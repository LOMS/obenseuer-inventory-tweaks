using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using OS.Items;
using TMPro;
using UnityEngine;

namespace InventoryTweaks.Patches
{
    /// <summary>
    /// Automates the Craft button of a station (manufacturing):
    /// - Alt + Click: "Craft amount" — pick how many times to craft (the game's amount slider);
    /// - Ctrl + Click: "Craft all" — craft until the ingredients run out;
    /// - ESC stops the run (and is swallowed, so the station window stays open).
    /// Every step is the game's own manufacturing.Craft(), issued only when the vanilla Craft
    /// button is enabled again. A run also ends when the window is closed, another recipe is
    /// selected or the station cannot be powered.
    /// </summary>
    internal static class CraftAutomation
    {
        // Own purpose of the game's amount slider (the game's enum ends at Grow)
        public const AmountSliderType SliderType = (AmountSliderType)100;

        private const string CraftAmountLabel = "Craft amount";
        private const string CraftAllLabel = "Craft all";

        private static readonly AccessTools.FieldRef<CraftingBase, Recipe> SelectedRecipe =
            AccessTools.FieldRefAccess<CraftingBase, Recipe>("selectedRecipe");

        private static readonly AccessTools.FieldRef<manufacturing, int> InProgress =
            AccessTools.FieldRefAccess<manufacturing, int>("CraftingInProgress");

        private static readonly AccessTools.FieldRef<AmountSliderUI, int> SliderValue =
            AccessTools.FieldRefAccess<AmountSliderUI, int>("currentValue");

        private static readonly AccessTools.FieldRef<AmountSliderUI, TextMeshProUGUI> SliderOkText =
            AccessTools.FieldRefAccess<AmountSliderUI, TextMeshProUGUI>("okButtonText");

        // Run state
        private static manufacturing _station;
        private static int _recipeId;
        private static int _remaining;
        private static int _done;
        private static bool _unlimited;

        // Amount slider state (between Alt + Click and OK)
        private static manufacturing _pendingStation;
        private static Recipe _pendingRecipe;
        private static GameObject _sliderItemObject;

        // Stations we switched on ourselves, to be switched off when their crafting ends
        private static readonly HashSet<manufacturing> AutoPowered = new HashSet<manufacturing>();

        // ESC handling: the press that stopped a run is hidden from every game handler of that frame
        private static int _escapeFrame = -1;

        // Craft() called by us must not be intercepted again
        private static bool _inside;

        // Craft button variants (enabled / disabled / progress) and their cached labels
        private static readonly GameObject[] LabelButtons = new GameObject[3];
        private static readonly TextMeshProUGUI[] LabelTexts = new TextMeshProUGUI[3];

        // Labels we have overridden (restored when the override ends)
        private static readonly List<KeyValuePair<TextMeshProUGUI, string>> Overridden =
            new List<KeyValuePair<TextMeshProUGUI, string>>();

        public static bool Active => _station != null;

        private static bool CtrlHeld => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        private static bool AltHeld => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        private static RecipeInfoUI RecipeInfo
        {
            get
            {
                ManufacturingPanel panel = ManufacturingPanel.instance;
                return panel != null && panel.manufacturingPanelUI != null
                    ? panel.manufacturingPanelUI.recipeListUI.recipeInfo
                    : null;
            }
        }

        private static bool IsOpen(manufacturing station)
        {
            return station != null
                && ManufacturingPanel.instance != null
                && ManufacturingPanel.instance.ActiveManu == station
                && CraftingBase.currentCraftingBase == station;
        }

        // ---- Click on the Craft button -------------------------------------------------

        /// <summary>Returns true when the click was taken over (vanilla Craft must not run).</summary>
        public static bool TryIntercept(manufacturing station)
        {
            if (_inside || station == null || station.Autocraft || !IsOpen(station))
            {
                return false;
            }

            bool ctrl = CtrlHeld;
            bool alt = AltHeld;
            if (!ctrl && !alt)
            {
                // A manual click means the player has taken over; vanilla Craft then runs
                // (it only flashes the power icon if the station is off)
                Stop();
                EnsurePower(station);
                return false;
            }

            RecipeInfoUI info = RecipeInfo;
            Recipe recipe = info != null ? info.CurrentRecipe() : null;
            if (recipe == null || AmountSliderUI.instance == null || AmountSliderUI.instance.sliderIsVisible)
            {
                return false;
            }

            if (ctrl)
            {
                Start(station, recipe, int.MaxValue);
            }
            else
            {
                BeginAmount(station, recipe);
            }
            return true;
        }

        private static void BeginAmount(manufacturing station, Recipe recipe)
        {
            int max = Mathf.Max(1, station.GetRecipeMultiplier(recipe));
            Item display = DisplayItem(recipe);
            if (max == 1 || display == null)
            {
                Start(station, recipe, max);
                return;
            }

            // Same preview the game builds for the Plant slider
            if (_sliderItemObject != null)
            {
                Object.Destroy(_sliderItemObject);
            }
            _sliderItemObject = new GameObject("InventoryTweaks.CraftAmount");
            _sliderItemObject.transform.SetParent(AmountSliderUI.instance.transform, false);
            ItemData data = _sliderItemObject.AddComponent<ItemData>();
            data.item = display;
            data.amount = max;
            // The prototype's quality is not what the craft will give, so the preview shows none
            data.meta = display.CloneMeta(fullCopy: false, max).Where(m => !(m is ItemQualityData)).ToArray();

            _pendingStation = station;
            _pendingRecipe = recipe;
            AmountSliderUI.instance.ShowSlider(data, SliderType);
            SliderOkText(AmountSliderUI.instance).text = "Craft";
        }

        private static Item DisplayItem(Recipe recipe)
        {
            if (recipe.Output != null && recipe.Output.Count > 0)
            {
                return recipe.Output.First().Key;
            }
            if (recipe.variantItemCategory == VariantCategory.LiquidContainer)
            {
                return ItemVariantManager.instance.GetDefaultItem(VariantCategory.LiquidContainer);
            }
            return recipe.Input != null && recipe.Input.Count > 0 ? recipe.Input.First().Key : null;
        }

        /// <summary>OK on the amount slider. Returns true when the slider was ours.</summary>
        public static bool TryConfirmAmount(AmountSliderUI slider)
        {
            if (slider.Type != SliderType)
            {
                return false;
            }
            if (!slider.sliderIsVisible)
            {
                return true;
            }

            int amount = SliderValue(slider);
            manufacturing station = _pendingStation;
            Recipe recipe = _pendingRecipe;
            _pendingStation = null;
            _pendingRecipe = null;
            slider.HideSlider();

            RecipeInfoUI info = RecipeInfo;
            if (IsOpen(station) && recipe != null && info != null && info.CurrentRecipe() != null
                && info.CurrentRecipe().ID == recipe.ID && amount > 0)
            {
                Start(station, recipe, amount);
            }
            return true;
        }

        // ---- Run -------------------------------------------------------------------------

        private static void Start(manufacturing station, Recipe recipe, int count)
        {
            _station = station;
            _recipeId = recipe.ID;
            _unlimited = count == int.MaxValue;
            _remaining = count;
            _done = 0;
        }

        public static void Stop()
        {
            _station = null;
        }

        /// <summary>Called every frame from Plugin.LateUpdate.</summary>
        public static void Tick()
        {
            if (_station != null)
            {
                try
                {
                    Step();
                }
                catch
                {
                    // Never keep retrying a failing step every frame
                    Stop();
                    throw;
                }
            }
            ReleasePower();
            UpdateLabels();
        }

        private static void Step()
        {
            manufacturing station = _station;
            RecipeInfoUI info = RecipeInfo;
            if (!IsOpen(station) || info == null)
            {
                Stop();
                return;
            }

            // Another recipe selected, or the recipe is gone from the list (ingredients ran out)
            Recipe shown = info.CurrentRecipe();
            if (shown == null || shown.ID != _recipeId)
            {
                Stop();
                return;
            }

            // The recipe went grey in the list (ingredients, modifiers or skill): nothing to craft.
            // The Craft button itself is not looked at: it is also grey while the station is busy
            // and is redrawn a moment after a timed craft finishes, and Craft() does not need it.
            if (!CanCraft(shown, info))
            {
                Stop();
                return;
            }

            // Station still busy with the previous craft: wait until it can take another one
            if (InProgress(station) >= station.AllowSimultaneous)
            {
                return;
            }

            if (!EnsurePower(station))
            {
                Notify("Crafting stopped", "The station cannot be powered.");
                Stop();
                return;
            }

            _inside = true;
            try
            {
                station.Craft();
            }
            finally
            {
                _inside = false;
            }

            _done++;
            if (!_unlimited && --_remaining <= 0)
            {
                Stop();
            }
        }

        /// <summary>
        /// Whether the recipe is grey in the station's list. The game decides that on every list
        /// rebuild (ingredients, skill) and keeps the shown recipe's button in recipeInfo.currentButton.
        /// </summary>
        private static bool CanCraft(Recipe recipe, RecipeInfoUI info)
        {
            RecipeButton button = info.currentButton;
            return button != null && !button.disabledButton
                && RecipeInfoUI.CheckModifiers(recipe, info.modifiers);
        }

        /// <summary>
        /// Turns the station on like the power button does; false if it stays unpowered.
        /// A station switched on here is remembered and switched off again when its crafting ends.
        /// </summary>
        private static bool EnsurePower(manufacturing station)
        {
            if (station.PoweredAndActive)
            {
                return true;
            }

            bool wasOn = station.On;
            station.Toggle(true);
            if (station.PoweredAndActive)
            {
                AutoPowered.Add(station);
                return true;
            }

            // No fuel / no electricity: leave the switch as the player had it
            if (!wasOn)
            {
                station.Toggle(false);
            }
            return false;
        }

        /// <summary>
        /// Switches off the stations we switched on once nothing is crafting in them any more.
        /// Switching off earlier would stop the timers of crafts in progress.
        /// </summary>
        private static void ReleasePower()
        {
            if (AutoPowered.Count == 0)
            {
                return;
            }

            AutoPowered.RemoveWhere(station =>
            {
                // Destroyed, always on, or the player switched it off themselves
                if (station == null || station.AlwaysOn || !station.On)
                {
                    return true;
                }
                if (station == _station || InProgress(station) > 0)
                {
                    return false;
                }
                station.Toggle(false);
                return true;
            });
        }

        private static void Notify(string title, string text)
        {
            if (Notifications.instance != null)
            {
                Notifications.instance.CreateNotification(title, text);
            }
        }

        // ---- ESC ---------------------------------------------------------------------------

        /// <summary>True if this "Pause Menu" press must be hidden from the game.</summary>
        public static bool ConsumeEscape()
        {
            if (_station != null)
            {
                Stop();
                _escapeFrame = Time.frameCount;
                return true;
            }
            return _escapeFrame == Time.frameCount;
        }

        // ---- Button label and tooltip -------------------------------------------------------

        private static string DesiredLabel()
        {
            if (_station != null)
            {
                return _unlimited ? $"Crafting {_done}" : $"Crafting {_done}/{_done + _remaining}";
            }
            if (ManufacturingPanel.instance == null || ManufacturingPanel.instance.ActiveManu == null)
            {
                return null;
            }
            if (CtrlHeld)
            {
                return CraftAllLabel;
            }
            return AltHeld ? CraftAmountLabel : null;
        }

        private static void UpdateLabels()
        {
            RecipeInfoUI info = RecipeInfo;
            string label = info != null && info.gameObject.activeInHierarchy ? DesiredLabel() : null;

            // Forget labels of destroyed buttons (the game recreates them on every list rebuild)
            Overridden.RemoveAll(entry => entry.Key == null);

            if (label == null)
            {
                foreach (KeyValuePair<TextMeshProUGUI, string> entry in Overridden)
                {
                    entry.Key.text = entry.Value;
                }
                Overridden.Clear();
                return;
            }

            for (int i = 0; i < LabelButtons.Length; i++)
            {
                GameObject button = i == 0 ? info.createButtonEnabled
                    : i == 1 ? info.createButtonDisabled
                    : info.createButtonProgress;
                if (button == null || !button.activeInHierarchy)
                {
                    continue;
                }

                // The game recreates the buttons on every list rebuild: look the label up once per new button
                if (LabelButtons[i] != button)
                {
                    LabelButtons[i] = button;
                    LabelTexts[i] = button.GetComponentInChildren<TextMeshProUGUI>();
                }
                TextMeshProUGUI text = LabelTexts[i];
                if (text == null)
                {
                    continue;
                }

                if (!IsOverridden(text))
                {
                    Overridden.Add(new KeyValuePair<TextMeshProUGUI, string>(text, text.text));
                }
                if (text.text != label)
                {
                    text.text = label;
                }
            }
        }

        private static bool IsOverridden(TextMeshProUGUI text)
        {
            foreach (KeyValuePair<TextMeshProUGUI, string> entry in Overridden)
            {
                if (entry.Key == text)
                {
                    return true;
                }
            }
            return false;
        }

        public static void AttachTooltip(RecipeInfoUI info)
        {
            if (info == null || info.createButtonEnabled == null || RecipeInfo != info)
            {
                return;
            }
            // The tooltip goes on the object with the button script, which receives the hover
            DefaultUIButton button = info.createButtonEnabled.GetComponentInChildren<DefaultUIButton>();
            if (button != null)
            {
                HintTooltip.Attach(button.gameObject, "Craft",
                    "Alt + Click: craft several times (choose the amount).\n" +
                    "Ctrl + Click: craft until the ingredients run out.\n" +
                    "Esc: stop.");
            }
        }
    }

    [HarmonyPatch(typeof(manufacturing), nameof(manufacturing.Craft))]
    internal static class CraftClickPatch
    {
        private static bool Prefix(manufacturing __instance)
        {
            return !CraftAutomation.TryIntercept(__instance);
        }
    }

    [HarmonyPatch(typeof(AmountSliderUI), nameof(AmountSliderUI.PressedOk))]
    internal static class CraftAmountOkPatch
    {
        private static bool Prefix(AmountSliderUI __instance)
        {
            return !CraftAutomation.TryConfirmAmount(__instance);
        }
    }

    [HarmonyPatch(typeof(RecipeInfoUI), nameof(RecipeInfoUI.ShowRecipe))]
    internal static class CraftButtonShowRecipePatch
    {
        private static void Postfix(RecipeInfoUI __instance)
        {
            CraftAutomation.AttachTooltip(__instance);
        }
    }

    /// <summary>ESC ("Pause Menu") during a run only stops it; the game never sees that press.</summary>
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.GetKeyDown), typeof(string))]
    internal static class CraftEscapePatch
    {
        private static void Postfix(string key, ref bool __result)
        {
            if (__result && key == "Pause Menu" && CraftAutomation.ConsumeEscape())
            {
                __result = false;
            }
        }
    }
}
