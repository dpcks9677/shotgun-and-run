namespace InventorySystem
{
    public interface IItemInventory
    {
        public int GetItemCount(ItemData itemData);

        public bool HasItem(ItemData itemData, int amount);

        public bool TryRemoveItem(ItemData itemData, int amount);
    }
}
