using Consumable;
using UnityEngine;

public class ConsumableSlot
{
    [SerializeField] private ConsumableData _consumableData;
    [SerializeField] private int _quantity;
    private bool _isSelected;

    public ConsumableData ConsumableData => _consumableData;
    public int Quantity => _quantity;
    public bool IsSelected => _isSelected;

    public ConsumableSlot()
    {
        _consumableData = null;
        _isSelected = false;
        _quantity = 0;
    }

    public void Select()
    {
        _isSelected = true;
    }

    public void Deselect()
    {
        _isSelected = false;
    }

    public void SetConsumable(ConsumableData consumableData, int quantity)
    {
        _consumableData = consumableData;
        _quantity = Mathf.Clamp(quantity, 0, consumableData != null ? consumableData.MaxStack : 0);

        if (_quantity <= 0)
        {
            Clear();
        }
    }

    public int AddQuantity(int amount)
    {
        if (_consumableData == null || amount <= 0) return 0;

        _quantity = Mathf.Clamp(_quantity + amount, 0, _consumableData.MaxStack);

        int spaceLeft = _consumableData.MaxStack - _quantity;
        int added = Mathf.Min(spaceLeft, amount);
        _quantity += added;

        return amount - added;
    }

    public int RemoveQuantity(int amount)
    {
        if (_consumableData == null || amount <= 0) return 0;

        int removed = Mathf.Min(_quantity, amount);
        _quantity -= removed;

        if (_quantity <= 0)
        {
            Clear();
        }

        return removed;
    }

    public ConsumableData SetSelected()
    {
        _isSelected = true;
        return _consumableData;
    }

    public void SetDeselected()
    {
        _isSelected = false;
    }

    public void Clear()
    {
        _consumableData = null;
        _quantity = 0;
    }
}
