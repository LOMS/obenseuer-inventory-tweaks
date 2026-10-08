using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InventoryTweaks.Patches
{
    /// <summary>
    /// "Stack" button in the storage panel, between "Sort" and "Take all".
    /// Moves items of every type already present in the open container from the
    /// player's inventory into it: existing stacks first, then free slots.
    /// Shift+Click also takes from the backpack ("Stack+").
    /// </summary>
    internal static class StackButton
    {
        private const string Label = "Stack";
        private const string LabelWithBackpack = "Stack+";
        private const string TooltipTitle = "Stack";
        private const string TooltipDetails =
            "Move items of the types already in this container from your inventory.\n" +
            "Shift + Click: also from your backpack.";
        private const string TakeAllTooltipTitle = "Take all";
        private const string TakeAllTooltipDetails =
            "Take everything from this container.\n" +
            "Alt + Click: take only the item types you already have in your inventory (backpack not counted).";
        private const float Margin = 8f;

        private static readonly AccessTools.FieldRef<StoragePanelUI, Storage> CurrentStorage =
            AccessTools.FieldRefAccess<StoragePanelUI, Storage>("currentStorage");

        private static readonly AccessTools.FieldRef<StoragePanelUI, SoundInfo> TakeAllSound =
            AccessTools.FieldRefAccess<StoragePanelUI, SoundInfo>("takeAllSound");

        private static readonly AccessTools.FieldRef<Storage, bool> StorageChanged =
            AccessTools.FieldRefAccess<Storage, bool>("storageChanged");

        private static StoragePanelUI _panel;
        private static GameObject _button;
        private static TextMeshProUGUI _label;

        private static bool ShiftHeld => Input.GetKey(KeyCode.LeftShift);

        /// <summary>Creates the button once and keeps its visibility in sync with "Take all".</summary>
        public static void OnPanelEnabled(StoragePanelUI panel)
        {
            if (_button == null || _panel != panel)
            {
                Create(panel);
            }
            if (_button != null)
            {
                // Vanilla hides Take all during trade; follow it
                _button.SetActive(panel.takeAllButton.activeSelf);
            }
            if (panel.takeAllButton != null && panel.takeAllButton.GetComponent<HintTooltip>() == null)
            {
                HintTooltip.Attach(panel.takeAllButton, TakeAllTooltipTitle, TakeAllTooltipDetails);
            }
        }

        private static void Create(StoragePanelUI panel)
        {
            GameObject sort = panel.sortButton;
            GameObject takeAll = panel.takeAllButton;
            if (sort == null || takeAll == null)
            {
                Plugin.Log.LogWarning("Stack button: sort/take-all button not found, skipping");
                return;
            }

            // Clone Sort, not Take all: Take all has a Relay (savable, GUID-based)
            _panel = panel;
            _button = Object.Instantiate(sort, sort.transform.parent);
            _button.name = "Stack button";

            // Fully qualified: the game has its own global "Button" type
            UnityEngine.UI.Button button = _button.GetComponent<UnityEngine.UI.Button>();
            if (button != null)
            {
                // Replace the cloned persistent "sort" listener
                button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
                button.onClick.AddListener(OnClick);
            }

            _label = _button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (_label != null)
            {
                // Make sure localization cannot overwrite our label
                foreach (Component component in _label.GetComponents<Component>())
                {
                    if (component != null && component.GetType().Name.Contains("Locali"))
                    {
                        Object.Destroy(component);
                    }
                }
                _label.text = Label;
            }

            HintTooltip.Attach(_button, TooltipTitle, TooltipDetails);

            Place(sort.GetComponent<RectTransform>(), takeAll.GetComponent<RectTransform>(),
                _button.GetComponent<RectTransform>());
        }

        private static void Place(RectTransform sort, RectTransform takeAll, RectTransform stack)
        {
            Transform parent = sort.parent;
            if (parent.GetComponent<LayoutGroup>() != null)
            {
                int sortIndex = sort.GetSiblingIndex();
                int takeIndex = takeAll.GetSiblingIndex();
                stack.SetSiblingIndex(sortIndex < takeIndex ? takeIndex : sortIndex);
                Plugin.Log.LogInfo("Stack button: placed by the parent's LayoutGroup");
                return;
            }

            float width = stack.rect.width;
            float sortRight = sort.localPosition.x + sort.rect.xMax;
            float takeLeft = takeAll.localPosition.x + takeAll.rect.xMin;
            float gap = takeLeft - sortRight;
            float needed = width + 2f * Margin;

            Plugin.Log.LogInfo(
                $"Stack button layout: sort x={sort.localPosition.x} w={sort.rect.width}, " +
                $"takeAll x={takeAll.localPosition.x} w={takeAll.rect.width}, gap={gap}, needed={needed}");

            float center;
            if (gap >= needed)
            {
                center = (sortRight + takeLeft) / 2f;
            }
            else
            {
                // Not enough room: move Sort left by the missing amount
                float shift = needed - gap;
                sort.anchoredPosition -= new Vector2(shift, 0f);
                sortRight -= shift;
                center = sortRight + Margin + width / 2f;
            }

            // Clone shares Sort's anchors and pivot, so a local delta equals an anchored delta
            float pivotX = center - stack.rect.center.x;
            stack.anchoredPosition = sort.anchoredPosition + new Vector2(pivotX - sort.localPosition.x, 0f);
        }

        /// <summary>Called every frame: "Stack+" while Shift is held.</summary>
        public static void UpdateLabel()
        {
            if (_label == null || !_button.activeInHierarchy)
            {
                return;
            }
            string text = ShiftHeld ? LabelWithBackpack : Label;
            if (_label.text != text)
            {
                _label.text = text;
            }
        }

        private static void OnClick()
        {
            StoragePanelUI panel = _panel;
            if (panel == null || TradePanel.instance.activeTrade != null)
            {
                return;
            }
            Storage storage = CurrentStorage(panel);
            if (storage == null || storage.Slots == null)
            {
                return;
            }

            SlotController[] targets = storage.Slots;
            HashSet<ItemKind> kinds = KindsIn(targets);
            if (kinds.Count == 0)
            {
                return;
            }

            bool movedAny = false;
            bool leftOver = false;
            foreach (SlotController source in GetSources(storage, ShiftHeld))
            {
                if (!ItemKind.HasItem(source) || !kinds.Contains(ItemKind.Of(source.itemStack)))
                {
                    continue;
                }
                int moved = MoveToContainer(source, targets);
                movedAny |= moved > 0;
                leftOver |= ItemKind.HasItem(source);
            }

            if (movedAny)
            {
                StorageChanged(storage) = true;
                MiscAudioPlayer.PlaySound(TakeAllSound(panel));
            }
            if (leftOver)
            {
                panel.StorageFull();
            }
        }

        /// <summary>Kinds (item + liquid) of everything in the given slots.</summary>
        internal static HashSet<ItemKind> KindsIn(SlotController[] slots)
        {
            var kinds = new HashSet<ItemKind>();
            foreach (SlotController slot in slots)
            {
                if (ItemKind.HasItem(slot))
                {
                    kinds.Add(ItemKind.Of(slot.itemStack));
                }
            }
            return kinds;
        }

        private static IEnumerable<SlotController> GetSources(Storage openStorage, bool includeBackpack)
        {
            foreach (SlotController slot in Inventory.instance.Slots)
            {
                yield return slot;
            }

            if (!includeBackpack)
            {
                yield break;
            }
            Storage backpack = BackpackStorage.instance.GetStorage();
            if (backpack == null || backpack == openStorage || backpack.Slots == null)
            {
                yield break;
            }
            foreach (SlotController slot in backpack.Slots)
            {
                yield return slot;
            }
        }

        /// <summary>
        /// Moves one source stack into the target slots (stacks of the same kind first,
        /// then free slots); returns the moved amount. Shared with "Take similar".
        /// </summary>
        internal static int MoveToContainer(SlotController source, SlotController[] targets)
        {
            ItemStack stack = source.itemStack;
            if (stack.itemReference == null)
            {
                stack.SetItemReference();
                if (stack.itemReference == null)
                {
                    return 0;
                }
            }

            Item item = stack.itemReference.Item;
            ItemKind kind = ItemKind.Of(stack);
            int stackAmount = stack.itemAmount;
            int remaining = stackAmount;

            // Pass 1: top up stacks of the same kind (incl. liquid); pass 2: free slots
            for (int pass = 0; pass < 2 && remaining > 0; pass++)
            {
                foreach (SlotController target in targets)
                {
                    if (target == null || (target.slot != null && target.slot.noDrop))
                    {
                        continue;
                    }
                    bool suitable = pass == 0
                        ? ItemKind.HasItem(target) && ItemKind.Of(target.itemStack) == kind
                        : target.itemStack.itemId == -1;
                    if (!suitable)
                    {
                        continue;
                    }
                    // Vanilla checks (OnDroppedCheck, categories, stack size); never drops
                    target.AddItem(item, ref remaining, stack.ownerId, stack.Meta, isSibling: true, onlycheck: false,
                        mergingLiquidStackAlways: false, stackAmount);
                    if (remaining <= 0)
                    {
                        break;
                    }
                }
            }

            int moved = stackAmount - remaining;
            if (moved <= 0)
            {
                return 0;
            }

            // Shrink the source the same way SlotController.MoveToOtherInventory does
            stack.itemAmount = remaining;
            if (remaining == 0)
            {
                source.RemoveItem();
            }
            else
            {
                source.slot?.UpdateSlot();
            }
            return moved;
        }
    }

    /// <summary>
    /// Alt + "Take all" = "Take similar": takes from the open container only the item
    /// types already in the player's main inventory (backpack ignored), into the main
    /// inventory — the reverse of "Stack".
    /// </summary>
    internal static class TakeSimilar
    {
        private const string Label = "Take similar";

        private static readonly AccessTools.FieldRef<StoragePanelUI, Storage> CurrentStorage =
            AccessTools.FieldRefAccess<StoragePanelUI, Storage>("currentStorage");

        private static readonly AccessTools.FieldRef<StoragePanelUI, SoundInfo> TakeAllSound =
            AccessTools.FieldRefAccess<StoragePanelUI, SoundInfo>("takeAllSound");

        private static readonly AccessTools.FieldRef<Storage, bool> StorageChanged =
            AccessTools.FieldRefAccess<Storage, bool>("storageChanged");

        private static TextMeshProUGUI _label;
        private static string _originalText;
        private static bool _overridden;

        public static bool AltHeld => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        /// <summary>Called every frame: "Take similar" on the Take all button while Alt is held.</summary>
        public static void UpdateLabel()
        {
            StoragePanelUI panel = Inventory.instance != null ? Inventory.instance.storagePanelUI : null;
            if (panel == null || panel.takeAllButton == null || !panel.takeAllButton.activeInHierarchy)
            {
                return;
            }
            if (_label == null)
            {
                _label = panel.takeAllButton.GetComponentInChildren<TextMeshProUGUI>(true);
                _overridden = false;
                if (_label == null)
                {
                    return;
                }
            }

            if (AltHeld)
            {
                if (!_overridden)
                {
                    _originalText = _label.text;
                    _overridden = true;
                }
                if (_label.text != Label)
                {
                    _label.text = Label;
                }
            }
            else if (_overridden)
            {
                _overridden = false;
                _label.text = _originalText;
            }
        }

        public static void Run(StoragePanelUI panel)
        {
            Storage storage = CurrentStorage(panel);
            if (storage == null || storage.Slots == null)
            {
                return;
            }

            SlotController[] targets = Inventory.instance.Slots;
            HashSet<ItemKind> kinds = StackButton.KindsIn(targets);

            bool movedAny = false;
            bool leftOver = false;
            foreach (SlotController source in storage.Slots)
            {
                if (!ItemKind.HasItem(source) || !kinds.Contains(ItemKind.Of(source.itemStack)))
                {
                    continue;
                }
                movedAny |= StackButton.MoveToContainer(source, targets) > 0;
                leftOver |= ItemKind.HasItem(source);
            }

            if (movedAny)
            {
                StorageChanged(storage) = true;
                MiscAudioPlayer.PlaySound(TakeAllSound(panel));
            }
            if (leftOver)
            {
                Inventory.instance.inventoryPanelUI.InventoryFull();
            }
        }
    }

    [HarmonyPatch(typeof(StoragePanelUI), nameof(StoragePanelUI.TakeAll))]
    internal static class TakeAllPatch
    {
        private static bool Prefix(StoragePanelUI __instance)
        {
            // Same guard as vanilla TakeAll; otherwise let vanilla run (and reject)
            if (!TakeSimilar.AltHeld || TradePanel.instance.activeTrade != null)
            {
                return true;
            }
            TakeSimilar.Run(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(StoragePanelUI), "OnEnable")]
    internal static class StoragePanelOnEnablePatch
    {
        private static void Postfix(StoragePanelUI __instance)
        {
            try
            {
                StackButton.OnPanelEnabled(__instance);
            }
            catch (System.Exception e)
            {
                // Never break the vanilla storage panel because of the extra button
                Plugin.Log.LogError($"Stack button setup failed: {e}");
            }
        }
    }
}
