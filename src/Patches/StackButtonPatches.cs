using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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

            _button.AddComponent<StackButtonTooltip>().Set(TooltipTitle, TooltipDetails);

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
            var itemIds = new HashSet<int>();
            foreach (SlotController target in targets)
            {
                if (target != null && target.itemStack.itemId != -1 && target.itemStack.itemAmount > 0)
                {
                    itemIds.Add(target.itemStack.itemId);
                }
            }
            if (itemIds.Count == 0)
            {
                return;
            }

            bool movedAny = false;
            bool leftOver = false;
            foreach (SlotController source in GetSources(storage, ShiftHeld))
            {
                if (source == null || !itemIds.Contains(source.itemStack.itemId) || source.itemStack.itemAmount <= 0)
                {
                    continue;
                }
                int moved = MoveToContainer(source, targets);
                movedAny |= moved > 0;
                leftOver |= source.itemStack.itemId != -1 && source.itemStack.itemAmount > 0;
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

        /// <summary>Moves one source stack into the container; returns the moved amount.</summary>
        private static int MoveToContainer(SlotController source, SlotController[] targets)
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
            int itemId = stack.itemId;
            int stackAmount = stack.itemAmount;
            int remaining = stackAmount;

            // Pass 1: top up stacks of the same item; pass 2: free slots
            for (int pass = 0; pass < 2 && remaining > 0; pass++)
            {
                int wantedId = pass == 0 ? itemId : -1;
                foreach (SlotController target in targets)
                {
                    if (target == null || target.itemStack.itemId != wantedId || (target.slot != null && target.slot.noDrop))
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

    /// <summary>Shows the game's text tooltip while the pointer is over the button.</summary>
    internal class StackButtonTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private string _title;
        private string _details;
        private bool _shown;

        public void Set(string title, string details)
        {
            _title = title;
            _details = details;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (ToolTip.instance != null)
            {
                ToolTip.instance.Activate(_title, _details);
                _shown = true;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Hide();
        }

        private void OnDisable()
        {
            Hide();
        }

        private void Hide()
        {
            if (_shown && ToolTip.instance != null)
            {
                ToolTip.instance.Deactivate();
            }
            _shown = false;
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
