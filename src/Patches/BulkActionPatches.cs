using System.Collections.Generic;
using HarmonyLib;
using OS.Items;
using TMPro;
using UnityEngine;

namespace InventoryTweaks.Patches
{
    internal enum BulkKind
    {
        None,
        BreakAll,
        SlaughterAll
    }

    /// <summary>
    /// Shift + "Break" breaks the whole stack; Shift + "Slaughter" slaughters every
    /// animal of the same item in the same container (with a click-again confirmation).
    /// Each unit goes through the game's own action, so tools, sounds and spawned
    /// items behave exactly like repeated vanilla clicks.
    /// </summary>
    internal static class BulkActions
    {
        private const string BreakActionName = "Break";
        private const int PlayerCatItemId = 15650; // "Your Cat", the player's pet
        private const float ConfirmTimeout = 3f;

        private static readonly AccessTools.FieldRef<ItemInfoPanel, TextMeshProUGUI> PrimaryButtonText =
            AccessTools.FieldRefAccess<ItemInfoPanel, TextMeshProUGUI>("buttonText");

        private static readonly AccessTools.FieldRef<ItemInfoPanel, TextMeshProUGUI> SecondaryButtonText =
            AccessTools.FieldRefAccess<ItemInfoPanel, TextMeshProUGUI>("secondaryButtonText");

        // Pending "Slaughter all" confirmation
        private static SlotController _pendingSlot;
        private static int _pendingItemId;
        private static int _pendingIndex;
        private static int _pendingCount;
        private static float _pendingUntil;

        private static readonly bool[] LabelOverridden = new bool[2];

        private static bool ShiftHeld => Input.GetKey(KeyCode.LeftShift);

        public static BulkKind GetKind(ItemData itemData, int index)
        {
            if (itemData == null || itemData.slotController == null || ItemInfo.instance.childItem != null)
            {
                return BulkKind.None;
            }

            Item item = itemData.item;
            if (item?.Actions == null || index < 0 || index >= item.Actions.Length)
            {
                return BulkKind.None;
            }

            if (!(item.Actions[index] is ItemConsumable consumable))
            {
                return BulkKind.None;
            }

            if (consumable.Name == BreakActionName && item.Stackable > 1 && itemData.amount > 1)
            {
                return BulkKind.BreakAll;
            }

            if (consumable.Type == ItemConsumable.ConsumableType.Slaughter && item.ID != PlayerCatItemId)
            {
                return BulkKind.SlaughterAll;
            }

            return BulkKind.None;
        }

        /// <summary>Returns true when the click was handled and vanilla must be skipped.</summary>
        public static bool TryHandleUse(ItemInfo info, int index)
        {
            ItemData itemData = info.ItemData;
            bool confirming = IsPending(itemData, index);
            if (!ShiftHeld && !confirming)
            {
                return false;
            }

            switch (GetKind(itemData, index))
            {
                case BulkKind.BreakAll:
                    if (!ShiftHeld)
                    {
                        return false;
                    }
                    RepeatOnStack(itemData, itemData.item.Actions[index]);
                    Refresh(info);
                    return true;

                case BulkKind.SlaughterAll:
                    List<ItemData> targets = FindSameItemInContainer(itemData);
                    if (confirming)
                    {
                        ClearPending();
                        InvokeOnEach(targets, index);
                        Refresh(info);
                        return true;
                    }
                    if (targets.Count <= 1)
                    {
                        // Nothing to bulk: plain vanilla slaughter of this one animal
                        return false;
                    }
                    SetPending(itemData, index, targets.Count);
                    return true;

                default:
                    ClearPending();
                    return false;
            }
        }

        private static void RepeatOnStack(ItemData itemData, Item.ItemAction action)
        {
            SlotController slot = itemData.slotController;
            int itemId = slot.itemStack.itemId;
            int count = slot.itemStack.itemAmount;

            for (int i = 0; i < count; i++)
            {
                // Stop if the stack is gone or was replaced (e.g. by spawned shards)
                if (itemData == null || slot.itemStack.itemId != itemId || slot.itemStack.itemAmount <= 0)
                {
                    break;
                }
                if (!action.CanUse(itemData, out _))
                {
                    break;
                }
                itemData.item.InvokeAction(itemData, action);
            }
        }

        private static void InvokeOnEach(List<ItemData> targets, int index)
        {
            if (targets.Count == 0)
            {
                return;
            }

            int itemId = targets[0].slotController.itemStack.itemId;
            foreach (ItemData target in targets)
            {
                if (target == null || target.slotController == null || target.slotController.itemStack.itemId != itemId)
                {
                    continue;
                }

                Item.ItemAction action = target.item.Actions[index];
                // Stop when a required tool runs out (e.g. the axe breaks)
                if (!action.CanUse(target, out _))
                {
                    break;
                }
                target.item.InvokeAction(target, action);
            }
        }

        private static List<ItemData> FindSameItemInContainer(ItemData itemData)
        {
            var result = new List<ItemData>();
            SlotController origin = itemData.slotController;
            int itemId = origin.itemStack.itemId;
            SlotController[] slots = origin.Siblings ?? new[] { origin };

            foreach (SlotController slot in slots)
            {
                if (slot == null || slot.itemStack.itemId != itemId || slot.itemStack.itemAmount <= 0)
                {
                    continue;
                }
                // Actions need the item's UI object; slots without one are skipped
                ItemData data = slot.slot != null ? slot.slot.GetItemItemData() : null;
                if (data != null)
                {
                    result.Add(data);
                }
            }
            return result;
        }

        private static void Refresh(ItemInfo info)
        {
            ItemData itemData = info.ItemData;
            if (itemData == null || itemData.amount <= 0)
            {
                return;
            }
            info.itemInfoPanel.UpdateItemInfoSlot(itemData);
            info.itemInfoPanel.ClearStatInfo();
            info.UpdateInfo(itemData);
        }

        private static bool IsPending(ItemData itemData, int index)
        {
            return _pendingSlot != null
                && itemData != null
                && itemData.slotController == _pendingSlot
                && _pendingSlot.itemStack.itemId == _pendingItemId
                && _pendingIndex == index
                && Time.unscaledTime < _pendingUntil;
        }

        private static void SetPending(ItemData itemData, int index, int count)
        {
            _pendingSlot = itemData.slotController;
            _pendingItemId = itemData.slotController.itemStack.itemId;
            _pendingIndex = index;
            _pendingCount = count;
            _pendingUntil = Time.unscaledTime + ConfirmTimeout;
        }

        private static void ClearPending()
        {
            _pendingSlot = null;
        }

        /// <summary>Called every frame (LateUpdate) to show bulk labels on the action buttons.</summary>
        public static void UpdateButtonLabels()
        {
            ItemInfo info = ItemInfo.instance;
            if (info == null || info.itemInfoPanel == null || !info.itemInfoPanel.isActiveAndEnabled)
            {
                return;
            }

            ItemData itemData = info.ItemData;
            for (int index = 0; index < LabelOverridden.Length; index++)
            {
                TextMeshProUGUI text = index == 0
                    ? PrimaryButtonText(info.itemInfoPanel)
                    : SecondaryButtonText(info.itemInfoPanel);
                if (text == null)
                {
                    continue;
                }

                string label = GetBulkLabel(itemData, index);
                if (label != null)
                {
                    if (text.text != label)
                    {
                        text.text = label;
                    }
                    LabelOverridden[index] = true;
                }
                else if (LabelOverridden[index])
                {
                    LabelOverridden[index] = false;
                    RestoreLabel(text, itemData, index);
                }
            }
        }

        private static string GetBulkLabel(ItemData itemData, int index)
        {
            if (IsPending(itemData, index))
            {
                return $"Slaughter {_pendingCount}?";
            }
            if (!ShiftHeld)
            {
                return null;
            }

            switch (GetKind(itemData, index))
            {
                case BulkKind.BreakAll:
                    return "Break all";
                case BulkKind.SlaughterAll:
                    return FindSameItemInContainer(itemData).Count > 1 ? "Slaughter all" : null;
                default:
                    return null;
            }
        }

        private static void RestoreLabel(TextMeshProUGUI text, ItemData itemData, int index)
        {
            Item.ItemAction[] actions = itemData?.item?.Actions;
            if (actions != null && index < actions.Length)
            {
                text.text = actions[index].Name;
            }
        }
    }

    [HarmonyPatch(typeof(ItemInfo), nameof(ItemInfo.UseItem), typeof(int), typeof(bool))]
    internal static class UseItemBulkPatch
    {
        private static bool Prefix(ItemInfo __instance, int index, bool secondaryButton)
        {
            // Mirror vanilla guards; let vanilla handle (and reject) these cases
            if (__instance.ItemData == null || __instance.cooldown || TradePanel.instance.activeTrade != null)
            {
                return true;
            }
            ItemInfoPanel panel = __instance.itemInfoPanel;
            if (secondaryButton ? !panel.SecondaryUseButtonEnabled : !panel.UseButtonEnabled)
            {
                return true;
            }

            return !BulkActions.TryHandleUse(__instance, index);
        }
    }
}
