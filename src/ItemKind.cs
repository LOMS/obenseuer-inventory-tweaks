using System;
using OS.Items;

namespace InventoryTweaks
{
    /// <summary>
    /// "Same kind of item" for the move-similar features: the item ID plus, for liquid
    /// containers, the liquid inside. Empty containers only match empty containers,
    /// filled ones only match the same container with the same liquid.
    /// </summary>
    internal readonly struct ItemKind : IEquatable<ItemKind>
    {
        private const int NoLiquid = -1;

        public readonly int ItemId;
        public readonly int LiquidId;

        private ItemKind(int itemId, int liquidId)
        {
            ItemId = itemId;
            LiquidId = liquidId;
        }

        public static ItemKind Of(ItemStack stack)
        {
            ItemLiquidData liquid = stack.Meta != null ? ItemMetaUtilities.GetMetaOfType<ItemLiquidData>(stack.Meta) : null;
            bool hasLiquid = liquid?.liquidItem != null
                && ItemLiquidData.IsGreaterThan(liquid.GetLiquidAmount(stack.itemAmount), 0f);
            return new ItemKind(stack.itemId, hasLiquid ? liquid.liquidItem.ID : NoLiquid);
        }

        /// <summary>True for a slot holding at least one item.</summary>
        public static bool HasItem(SlotController slot)
        {
            return slot != null && slot.itemStack.itemId != -1 && slot.itemStack.itemAmount > 0;
        }

        public bool Equals(ItemKind other) => ItemId == other.ItemId && LiquidId == other.LiquidId;

        public override bool Equals(object obj) => obj is ItemKind other && Equals(other);

        public override int GetHashCode() => ItemId * 397 ^ LiquidId;

        public static bool operator ==(ItemKind a, ItemKind b) => a.Equals(b);

        public static bool operator !=(ItemKind a, ItemKind b) => !a.Equals(b);
    }
}
