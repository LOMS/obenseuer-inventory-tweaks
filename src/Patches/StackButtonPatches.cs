using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InventoryTweaks.Patches
{
    /// <summary>
    /// Extra button in the storage panel, between "Sort" and "Take all":
    /// - "Stack": moves items of every kind already present in the open container
    ///   from the player's inventory into it (existing stacks first, then free slots);
    /// - Alt + Click, "Stack allowed" (only for containers with an allowed-categories
    ///   filter): moves every item the container accepts.
    /// Shift+Click also takes from the backpack ("Stack+" / "Stack allowed+").
    /// </summary>
    internal static class StackButton
    {
        private const string StackLabel = "Stack";
        private const string StackTooltipDetails =
            "Move items of the types already in this container from your inventory.\n" +
            "Shift + Click: also from your backpack.";
        private const string AllowedLabel = "Stack allowed";
        private const string FilteredTooltipDetails =
            "Move items of the types already in this container from your inventory.\n" +
            "Alt + Click: move every item this container accepts.\n" +
            "Shift + Click: also from your backpack.";
        private const string BackpackSuffix = "+";
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

        private static readonly AccessTools.FieldRef<StoragePanelUI, TextMeshProUGUI> TitleText =
            AccessTools.FieldRefAccess<StoragePanelUI, TextMeshProUGUI>("storageName");

        private const float RowPadding = 4f;

        private sealed class PanelButton
        {
            public GameObject GameObject;
            public RectTransform Rect;
            public TextMeshProUGUI Text;
        }

        private static StoragePanelUI _panel;
        private static PanelButton _stack;
        // Vanilla elements the layout may move; restored before every layout
        private static Vector2 _sortOriginalPosition;
        private static Vector2 _takeAllOriginalPosition;
        private static Vector2 _titleOriginalPosition;
        private static Vector2 _titleOriginalSize;
        private static TextWrappingModes _titleOriginalWrapping;
        private static TextOverflowModes _titleOriginalOverflow;
        private static bool _parentHasLayoutGroup;
        private static bool? _layoutWithAllowed;

        private static bool ShiftHeld => Input.GetKey(KeyCode.LeftShift);

        /// <summary>Alt switches the button to "Stack allowed" in filtered containers.</summary>
        private static bool AllowedMode(Storage storage) => TakeSimilar.AltHeld && HasAllowedFilter(storage);

        /// <summary>Creates the buttons once and refreshes their visibility.</summary>
        public static void OnPanelEnabled(StoragePanelUI panel)
        {
            if (_stack == null || _stack.GameObject == null || _panel != panel)
            {
                Create(panel);
            }
            if (panel.takeAllButton != null && panel.takeAllButton.GetComponent<HintTooltip>() == null)
            {
                HintTooltip.Attach(panel.takeAllButton, TakeAllTooltipTitle, TakeAllTooltipDetails);
            }
            UpdateButtons();
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

            _panel = panel;
            _sortOriginalPosition = sort.GetComponent<RectTransform>().anchoredPosition;
            _takeAllOriginalPosition = takeAll.GetComponent<RectTransform>().anchoredPosition;
            TextMeshProUGUI title = TitleText(panel);
            if (title != null)
            {
                _titleOriginalPosition = title.rectTransform.anchoredPosition;
                _titleOriginalSize = title.rectTransform.sizeDelta;
                _titleOriginalWrapping = title.textWrappingMode;
                _titleOriginalOverflow = title.overflowMode;
                Plugin.Log.LogInfo(
                    $"Storage title: font={title.fontSize:0.#} autoSize={title.enableAutoSizing} " +
                    $"min={title.fontSizeMin:0.#} max={title.fontSizeMax:0.#} overflow={title.overflowMode} " +
                    $"wrap={title.textWrappingMode} align={title.alignment} margin={title.margin} " +
                    $"size={title.rectTransform.rect.size} parentIsHeader={title.transform.parent == sort.transform.parent}");
            }
            _parentHasLayoutGroup = sort.transform.parent.GetComponent<LayoutGroup>() != null;
            _layoutWithAllowed = null;

            _stack = CreateButton(takeAll, "Stack button", StackLabel, OnStackClick);
            if (_parentHasLayoutGroup)
            {
                // Order matters for a LayoutGroup: Sort, Stack, Take all
                _stack.Rect.SetSiblingIndex(takeAll.transform.GetSiblingIndex());
            }
        }

        private static bool GetStackHint(out string title, out string details)
        {
            title = StackLabel;
            details = _panel != null && HasAllowedFilter(CurrentStorage(_panel))
                ? FilteredTooltipDetails
                : StackTooltipDetails;
            return true;
        }

        /// <summary>
        /// Clones a vanilla button (Take all) so the new one looks identical. The clone is
        /// made under an inactive holder, so none of its components run Awake/OnEnable
        /// until savable components (Take all's Relay: GUID-based, saved, can fire
        /// quest outputs) are removed; only then is it moved into the panel.
        /// </summary>
        private static PanelButton CreateButton(GameObject template, string name, string label,
            UnityEngine.Events.UnityAction onClick)
        {
            var holder = new GameObject("InventoryTweaks clone holder");
            holder.SetActive(false);
            GameObject clone = Object.Instantiate(template, holder.transform, false);
            clone.name = name;

            foreach (SavableScript savable in clone.GetComponentsInChildren<SavableScript>(true))
            {
                Object.DestroyImmediate(savable);
            }

            clone.transform.SetParent(template.transform.parent, false);
            Object.Destroy(holder);

            // Fully qualified: the game has its own global "Button" type
            UnityEngine.UI.Button button = clone.GetComponent<UnityEngine.UI.Button>();
            if (button != null)
            {
                // Replace the cloned persistent "take all" listener
                button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
                button.onClick.AddListener(onClick);
            }

            TextMeshProUGUI text = clone.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                // Make sure localization cannot overwrite our label
                foreach (Component component in text.GetComponents<Component>())
                {
                    if (component != null && component.GetType().Name.Contains("Locali"))
                    {
                        Object.Destroy(component);
                    }
                }
                text.text = label;
            }

            HintTooltip.Attach(clone, GetStackHint);

            return new PanelButton
            {
                GameObject = clone,
                Rect = clone.GetComponent<RectTransform>(),
                Text = text
            };
        }

        /// <summary>
        /// Take all is wide; shrink the clone to its widest label (incl. the "+" suffix),
        /// keeping the same horizontal padding Take all has around its text.
        /// </summary>
        private static void FitWidthToLabel(GameObject template, PanelButton button, string label)
        {
            TextMeshProUGUI templateText = template.GetComponentInChildren<TextMeshProUGUI>(true);
            if (button.Text == null || templateText == null)
            {
                return;
            }
            float templateWidth = template.GetComponent<RectTransform>().rect.width;
            // Take all's own padding is generous; cap it so the clones stay compact
            float padding = Mathf.Clamp(templateWidth - templateText.GetPreferredValues(templateText.text).x, 16f, 30f);
            float labelWidth = button.Text.GetPreferredValues(label + BackpackSuffix).x;
            float width = Mathf.Min(templateWidth, labelWidth + padding);
            button.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        private static string Describe(string name, RectTransform rect)
        {
            float left = rect.localPosition.x + rect.rect.xMin;
            float right = rect.localPosition.x + rect.rect.xMax;
            return $"{name}: x={rect.localPosition.x:0.#} [{left:0.#}..{right:0.#}] w={rect.rect.width:0.#} " +
                   $"anchors={rect.anchorMin.x:0.##}-{rect.anchorMax.x:0.##} pivot={rect.pivot.x:0.##}";
        }

        /// <summary>
        /// Lays out the header. Restores the vanilla positions first. Filtered containers
        /// (wide "Stack allowed" label on Alt) always use two rows (title on top, buttons
        /// below); ordinary containers keep a single row.
        /// </summary>
        private static void Layout(bool withAllowed)
        {
            // Fixed width for the widest label of this container, so Alt does not move anything
            FitWidthToLabel(_panel.takeAllButton, _stack, withAllowed ? AllowedLabel : StackLabel);
            if (_parentHasLayoutGroup)
            {
                return;
            }

            RectTransform sort = _panel.sortButton.GetComponent<RectTransform>();
            RectTransform takeAll = _panel.takeAllButton.GetComponent<RectTransform>();
            TextMeshProUGUI title = TitleText(_panel);
            sort.anchoredPosition = _sortOriginalPosition;
            takeAll.anchoredPosition = _takeAllOriginalPosition;
            if (title != null)
            {
                title.rectTransform.sizeDelta = _titleOriginalSize;
                title.rectTransform.anchoredPosition = _titleOriginalPosition;
                title.textWrappingMode = _titleOriginalWrapping;
                title.overflowMode = _titleOriginalOverflow;
            }

            var buttons = new List<PanelButton> { _stack };

            // Clones start at Take all's height (they may have been moved by the two-row layout)
            float rowY = CenterY(takeAll);
            foreach (PanelButton button in buttons)
            {
                MoveCenterY(button.Rect, rowY);
            }

            if (withAllowed)
            {
                TwoRowLayout(sort, takeAll, title, buttons);
                return;
            }
            SingleRowLayout(sort, takeAll, buttons);
        }

        /// <summary>
        /// Filtered containers: buttons in the bottom row, the title
        /// gets the whole top row (its rect is resized; the game's text auto-sizing fits it).
        /// </summary>
        private static void TwoRowLayout(RectTransform sort, RectTransform takeAll, TextMeshProUGUI title,
            List<PanelButton> buttons)
        {
            var parent = (RectTransform)sort.parent;
            float rowHeight = takeAll.rect.height;

            // Bottom row: buttons
            float rowY = parent.rect.yMin + RowPadding + rowHeight / 2f;

            // Top row: the rest of the header, full width from the title's left edge
            if (title != null)
            {
                RectTransform rect = title.rectTransform;
                float titleTop = parent.rect.yMax - RowPadding;
                float titleBottom = rowY + rowHeight / 2f + RowPadding;
                float titleLeft = HeaderX(rect, rect.rect.xMin);
                float titleRight = parent.rect.xMax - RowPadding;
                SetHeaderRect(rect, titleLeft, titleRight, titleBottom, titleTop);
                // One line, never truncated: with the vanilla overflow mode a line taller
                // than the (now shorter) rect is not drawn at all
                title.textWrappingMode = TextWrappingModes.NoWrap;
                title.overflowMode = TextOverflowModes.Overflow;
            }
            MoveCenterY(takeAll, rowY);
            MoveCenterY(sort, rowY);

            // The row is free now: pack from Take all to the left, Sort last
            float right = takeAll.localPosition.x + takeAll.rect.xMin - Margin;
            for (int i = buttons.Count - 1; i >= 0; i--)
            {
                right = PlaceRightEdge(buttons[i].Rect, right) - Margin;
                MoveCenterY(buttons[i].Rect, rowY);
            }
            PlaceRightEdge(sort, right);

            LogLayout("two rows", sort, takeAll, buttons, gap: 0f, needed: 0f);
            if (title != null)
            {
                Plugin.Log.LogInfo("  " + Describe("title", title.rectTransform) +
                                   $" h={title.rectTransform.rect.height:0.#}");
            }
        }

        /// <summary>X in the header's space of a point given in the rect's own local space.</summary>
        private static float HeaderX(RectTransform rect, float localX)
        {
            Transform header = _panel.sortButton.transform.parent;
            return header.InverseTransformPoint(rect.TransformPoint(new Vector3(localX, 0f, 0f))).x;
        }

        /// <summary>
        /// Resizes and moves a rect to the given box in the header's space (works for any
        /// anchors/pivot and for a rect whose parent is not the header).
        /// </summary>
        private static void SetHeaderRect(RectTransform rect, float left, float right, float bottom, float top)
        {
            Transform header = _panel.sortButton.transform.parent;
            // Header units → rect parent units (scale only)
            Vector3 size = rect.parent.InverseTransformVector(header.TransformVector(new Vector3(right - left, top - bottom, 0f)));
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Abs(size.x));
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Abs(size.y));

            // Move so the rect's center lands on the box center
            Vector3 currentCenter = header.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
            var delta = new Vector3((left + right) / 2f - currentCenter.x, (bottom + top) / 2f - currentCenter.y, 0f);
            Vector3 local = rect.parent.InverseTransformVector(header.TransformVector(delta));
            rect.anchoredPosition += new Vector2(local.x, local.y);
        }

        /// <summary>Moves a rect so its right edge is at x (parent space); returns its new left edge.</summary>
        private static float PlaceRightEdge(RectTransform rect, float x)
        {
            float currentRight = rect.localPosition.x + rect.rect.xMax;
            rect.anchoredPosition += new Vector2(x - currentRight, 0f);
            return x - rect.rect.width;
        }

        /// <summary>Vertical center of a rect in the header's (Sort's parent) space.</summary>
        private static float CenterY(RectTransform rect)
        {
            Transform header = _panel.sortButton.transform.parent;
            return header.InverseTransformPoint(rect.TransformPoint(rect.rect.center)).y;
        }

        /// <summary>Moves a rect vertically so its center is at y in the header's space.</summary>
        private static void MoveCenterY(RectTransform rect, float y)
        {
            Transform header = _panel.sortButton.transform.parent;
            float delta = y - CenterY(rect);
            // Convert the header-space delta into the rect's own parent space
            Vector3 local = rect.parent.InverseTransformVector(header.TransformVector(new Vector3(0f, delta, 0f)));
            rect.anchoredPosition += new Vector2(0f, local.y);
        }

        /// <summary>
        /// Places the extra buttons as a group between Sort and Take all; if the gap is too
        /// small, Sort is moved left by the missing amount.
        /// </summary>
        private static void SingleRowLayout(RectTransform sort, RectTransform takeAll, List<PanelButton> buttons)
        {
            float groupWidth = (buttons.Count - 1) * Margin;
            foreach (PanelButton button in buttons)
            {
                groupWidth += button.Rect.rect.width;
            }
            float sortRight = sort.localPosition.x + sort.rect.xMax;
            float takeLeft = takeAll.localPosition.x + takeAll.rect.xMin;
            float gap = takeLeft - sortRight;
            float needed = groupWidth + 2f * Margin;

            float groupLeft;
            if (gap >= needed)
            {
                groupLeft = sortRight + (gap - groupWidth) / 2f;
            }
            else
            {
                float shift = needed - gap;
                sort.anchoredPosition -= new Vector2(shift, 0f);
                sortRight -= shift;
                groupLeft = sortRight + Margin;
            }

            float left = groupLeft;
            foreach (PanelButton button in buttons)
            {
                RectTransform rect = button.Rect;
                float center = left + rect.rect.width / 2f;
                // Same parent, fixed anchors: a localPosition delta equals an anchoredPosition delta,
                // so this works whatever anchors the cloned button has
                float pivotX = center - rect.rect.center.x;
                rect.anchoredPosition += new Vector2(pivotX - rect.localPosition.x, 0f);
                left += rect.rect.width + Margin;
            }

            LogLayout("single row", sort, takeAll, buttons, gap, needed);
        }

        private static void LogLayout(string mode, RectTransform sort, RectTransform takeAll,
            List<PanelButton> buttons, float gap, float needed)
        {
            var parentRect = (RectTransform)sort.parent;
            string log = $"Stack buttons layout ({buttons.Count}, {mode}): parent w={parentRect.rect.width:0.#} " +
                         $"h={parentRect.rect.height:0.#}, gap={gap:0.#}, needed={needed:0.#}\n  " +
                         Describe("sort", sort) + "\n  " + Describe("takeAll", takeAll);
            foreach (PanelButton button in buttons)
            {
                log += "\n  " + Describe(button.GameObject.name, button.Rect);
            }
            Plugin.Log.LogInfo(log);
        }

        /// <summary>
        /// Called every frame: visibility (follows Take all, which vanilla hides in trade),
        /// layout (depends on the container's filter) and the label (Alt / Shift).
        /// </summary>
        public static void UpdateButtons()
        {
            if (_panel == null || _stack == null || _stack.GameObject == null || _panel.takeAllButton == null)
            {
                return;
            }

            bool visible = _panel.takeAllButton.activeSelf;
            Storage storage = CurrentStorage(_panel);
            bool withAllowed = visible && HasAllowedFilter(storage);
            if (_stack.GameObject.activeSelf != visible)
            {
                _stack.GameObject.SetActive(visible);
            }
            if (_layoutWithAllowed != withAllowed)
            {
                _layoutWithAllowed = withAllowed;
                Layout(withAllowed);
            }

            if (_stack.Text == null || !_stack.GameObject.activeInHierarchy)
            {
                return;
            }
            string text = (AllowedMode(storage) ? AllowedLabel : StackLabel) + (ShiftHeld ? BackpackSuffix : "");
            if (_stack.Text.text != text)
            {
                _stack.Text.text = text;
            }
        }

        private static bool HasAllowedFilter(Storage storage)
        {
            return storage != null && storage.AllowedCategories != null && storage.AllowedCategories.Length > 0;
        }

        private static void OnStackClick()
        {
            if (!TryGetStorage(out StoragePanelUI panel, out Storage storage))
            {
                return;
            }
            if (AllowedMode(storage))
            {
                StackAllowed(panel, storage);
                return;
            }
            HashSet<ItemKind> kinds = KindsIn(storage.Slots);
            if (kinds.Count == 0)
            {
                return;
            }
            MoveIntoStorage(panel, storage, source => kinds.Contains(ItemKind.Of(source.itemStack)));
        }

        private static void StackAllowed(StoragePanelUI panel, Storage storage)
        {
            // Same category check as SlotController.AddItem (filter + default forbidden categories)
            SlotController filter = storage.Slots.Length > 0 ? storage.Slots[0] : null;
            if (filter == null)
            {
                return;
            }
            MoveIntoStorage(panel, storage, source =>
            {
                Item item = GetItem(source.itemStack);
                return item != null && filter.CheckIfAllowed(item);
            });
        }

        private static bool TryGetStorage(out StoragePanelUI panel, out Storage storage)
        {
            panel = _panel;
            storage = null;
            if (panel == null || TradePanel.instance.activeTrade != null)
            {
                return false;
            }
            storage = CurrentStorage(panel);
            return storage != null && storage.Slots != null;
        }

        private static void MoveIntoStorage(StoragePanelUI panel, Storage storage, System.Func<SlotController, bool> shouldMove)
        {
            bool movedAny = false;
            bool leftOver = false;
            foreach (SlotController source in GetSources(storage, ShiftHeld))
            {
                if (!ItemKind.HasItem(source) || !shouldMove(source))
                {
                    continue;
                }
                movedAny |= MoveToContainer(source, storage.Slots) > 0;
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

        private static Item GetItem(ItemStack stack)
        {
            if (stack.itemReference == null)
            {
                stack.SetItemReference();
            }
            return stack.itemReference?.Item;
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
