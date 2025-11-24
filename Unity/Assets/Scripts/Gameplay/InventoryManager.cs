using System;
using System.Collections.Generic;
using Adventure.ScriptableObjects;
using UnityEngine;

namespace Adventure.Gameplay
{
    /// <summary>
    /// Client-side inventory model with stack-aware add/remove helpers so the UI can stay simple.
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        [SerializeField]
        private int maxSlots = 10;

        [SerializeField]
        private List<InventorySlot> slots = new();

        public event Action<InventorySlot>? SlotChanged;

        private void Awake()
        {
            EnsureSlotCapacity();
        }

        public IReadOnlyList<InventorySlot> Slots => slots;

        public bool TryAddItem(ItemDefinition item, int quantity)
        {
            if (item == null || quantity <= 0)
            {
                return false;
            }

            EnsureSlotCapacity();
            int remaining = quantity;

            // Fill existing stacks first
            for (int i = 0; i < slots.Count && remaining > 0; i++)
            {
                if (slots[i].Item != item)
                {
                    continue;
                }

                int stackable = Mathf.Min(item.MaxStackSize - slots[i].Quantity, remaining);
                if (stackable <= 0)
                {
                    continue;
                }

                slots[i] = slots[i].WithQuantity(slots[i].Quantity + stackable);
                remaining -= stackable;
                SlotChanged?.Invoke(slots[i]);
            }

            // Use empty slots if needed
            for (int i = 0; i < slots.Count && remaining > 0; i++)
            {
                if (!slots[i].IsEmpty)
                {
                    continue;
                }

                int toAssign = Mathf.Min(item.MaxStackSize, remaining);
                slots[i] = new InventorySlot(item, toAssign);
                remaining -= toAssign;
                SlotChanged?.Invoke(slots[i]);
            }

            return remaining == 0;
        }

        public bool TryConsumeItem(int slotIndex, bool inCombat)
        {
            if (!IsValidSlot(slotIndex))
            {
                return false;
            }

            var slot = slots[slotIndex];
            if (slot.IsEmpty || !slot.Item.Consumable)
            {
                return false;
            }

            if (!IsUsageAllowed(slot.Item, inCombat))
            {
                Debug.LogWarning($"Cannot use {slot.Item.DisplayName} in this context.");
                return false;
            }

            int newQuantity = slot.Quantity - 1;
            slots[slotIndex] = newQuantity > 0 ? slot.WithQuantity(newQuantity) : InventorySlot.Empty;
            SlotChanged?.Invoke(slots[slotIndex]);
            return true;
        }

        public bool TryRemoveItem(string itemId, int quantity)
        {
            if (quantity <= 0)
            {
                return false;
            }

            int remaining = quantity;
            for (int i = 0; i < slots.Count && remaining > 0; i++)
            {
                var slot = slots[i];
                if (slot.IsEmpty || slot.Item.Id != itemId)
                {
                    continue;
                }

                int consumed = Mathf.Min(slot.Quantity, remaining);
                remaining -= consumed;
                int newQuantity = slot.Quantity - consumed;
                slots[i] = newQuantity > 0 ? slot.WithQuantity(newQuantity) : InventorySlot.Empty;
                SlotChanged?.Invoke(slots[i]);
            }

            return remaining == 0;
        }

        private static bool IsUsageAllowed(ItemDefinition item, bool inCombat)
        {
            return item.UsageContext switch
            {
                ItemUsageContext.Anywhere => true,
                ItemUsageContext.InCombatOnly => inCombat,
                ItemUsageContext.OutOfCombatOnly => !inCombat,
                _ => true
            };
        }

        private bool IsValidSlot(int index)
        {
            return index >= 0 && index < slots.Count;
        }

        private void EnsureSlotCapacity()
        {
            while (slots.Count < maxSlots)
            {
                slots.Add(InventorySlot.Empty);
            }
        }
    }

    [Serializable]
    public struct InventorySlot
    {
        public ItemDefinition Item;
        public int Quantity;

        public InventorySlot(ItemDefinition item, int quantity)
        {
            Item = item;
            Quantity = quantity;
        }

        public bool IsEmpty => Item == null || Quantity <= 0;

        public InventorySlot WithQuantity(int newQuantity)
        {
            return new InventorySlot(Item, newQuantity);
        }

        public static InventorySlot Empty => new(null!, 0);
    }
}
