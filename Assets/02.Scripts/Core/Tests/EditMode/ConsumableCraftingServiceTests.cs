using System.Collections.Generic;
using Consumable;
using InventorySystem;
using NUnit.Framework;
using UnityEngine;

namespace ShotgunAndRun.Core.Tests
{
    [TestFixture]
    public class ConsumableCraftingServiceTests
    {
        private class StubRecipeProvider : IConsumableCraftingRecipeProvider
        {
            private readonly Dictionary<ConsumableData, ConsumableCraftingCost> _recipes = new();

            public void RegisterRecipe(ConsumableData data, MaterialCost[] costs)
            {
                _recipes[data] = new ConsumableCraftingCost(data, costs);
            }

            public IReadOnlyList<ConsumableCraftingCost> GetAllRecipes()
            {
                return new List<ConsumableCraftingCost>(_recipes.Values);
            }

            public bool TryGetRecipe(ConsumableData data, out ConsumableCraftingCost recipe)
            {
                if (data != null && _recipes.TryGetValue(data, out recipe))
                {
                    return true;
                }

                recipe = default;
                return false;
            }

            public bool TryGetRecipe(ConsumableType type, out ConsumableCraftingCost recipe)
            {
                foreach (var pair in _recipes)
                {
                    if (pair.Key != null && pair.Key.ConsumableType == type)
                    {
                        recipe = pair.Value;
                        return true;
                    }
                }

                recipe = default;
                return false;
            }
        }

        private class StubStorage : IItemInventory
        {
            private readonly Dictionary<ItemData, int> _items = new();

            public void SetItemCount(ItemData item, int count)
            {
                _items[item] = count;
            }

            public int GetItemCount(ItemData itemData)
            {
                return itemData != null && _items.TryGetValue(itemData, out int count) ? count : 0;
            }

            public bool HasItem(ItemData itemData, int amount)
            {
                return GetItemCount(itemData) >= amount;
            }

            public bool TryRemoveItem(ItemData itemData, int amount)
            {
                if (!HasItem(itemData, amount))
                {
                    return false;
                }

                _items[itemData] -= amount;
                return true;
            }
        }

        private class StubConsumableInventory : IConsumableInventory
        {
            public int SlotCount => 3;
            public bool IsFull
            {
                get;
                set;
            }
            public List<ConsumableData> GrantedConsumables
            {
                get;
            } = new();

            public bool CanAddConsumable(ConsumableData data, int amount = 1)
            {
                return !IsFull;
            }

            public bool TryAddConsumable(ConsumableData data, int amount = 1)
            {
                if (IsFull)
                {
                    return false;
                }

                GrantedConsumables.Add(data);
                return true;
            }
        }

        private StubRecipeProvider _recipeProvider;
        private StubStorage _storage;
        private StubConsumableInventory _consumableInventory;
        private ConsumableCraftingService _service;

        private ConsumableData _smallKitData;
        private ItemData _clothMaterial;

        [SetUp]
        public void SetUp()
        {
            _recipeProvider = new StubRecipeProvider();
            _storage = new StubStorage();
            _consumableInventory = new StubConsumableInventory();
            _service = new ConsumableCraftingService(_recipeProvider, _storage, _consumableInventory);

            _smallKitData = ScriptableObject.CreateInstance<ConsumableData>();
            _clothMaterial = ScriptableObject.CreateInstance<ItemData>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_smallKitData != null) Object.DestroyImmediate(_smallKitData);
            if (_clothMaterial != null) Object.DestroyImmediate(_clothMaterial);
        }

        [Test]
        public void CanCraft_WhenTargetIsNull_ReturnsInvalidTarget()
        {
            bool canCraft = _service.CanCraft(null, out CraftFailureReason reason);

            Assert.IsFalse(canCraft);
            Assert.AreEqual(CraftFailureReason.InvalidTarget, reason);
        }

        [Test]
        public void CanCraft_WhenRecipeDoesNotExist_ReturnsRecipeNotFound()
        {
            bool canCraft = _service.CanCraft(_smallKitData, out CraftFailureReason reason);

            Assert.IsFalse(canCraft);
            Assert.AreEqual(CraftFailureReason.RecipeNotFound, reason);
        }

        [Test]
        public void CanCraft_WhenInventoryIsFull_ReturnsInventoryFull()
        {
            _recipeProvider.RegisterRecipe(_smallKitData, new[] { new MaterialCost(_clothMaterial, 3) });
            _storage.SetItemCount(_clothMaterial, 10);
            _consumableInventory.IsFull = true;

            bool canCraft = _service.CanCraft(_smallKitData, out CraftFailureReason reason);

            Assert.IsFalse(canCraft);
            Assert.AreEqual(CraftFailureReason.InventoryFull, reason);
        }

        [Test]
        public void CanCraft_WhenMaterialsInsufficient_ReturnsInsufficientMaterials()
        {
            _recipeProvider.RegisterRecipe(_smallKitData, new[] { new MaterialCost(_clothMaterial, 3) });
            _storage.SetItemCount(_clothMaterial, 2);

            bool canCraft = _service.CanCraft(_smallKitData, out CraftFailureReason reason);

            Assert.IsFalse(canCraft);
            Assert.AreEqual(CraftFailureReason.InsufficientMaterials, reason);
        }

        [Test]
        public void TryCraft_WhenRequirementsMet_DeductsMaterialsAndGrantsConsumable()
        {
            _recipeProvider.RegisterRecipe(_smallKitData, new[] { new MaterialCost(_clothMaterial, 3) });
            _storage.SetItemCount(_clothMaterial, 5);

            bool success = _service.TryCraft(_smallKitData, out CraftFailureReason reason);

            Assert.IsTrue(success);
            Assert.AreEqual(CraftFailureReason.None, reason);
            Assert.AreEqual(2, _storage.GetItemCount(_clothMaterial)); // 5 - 3 = 2
            Assert.AreEqual(1, _consumableInventory.GrantedConsumables.Count);
            Assert.AreEqual(_smallKitData, _consumableInventory.GrantedConsumables[0]);
        }
    }
}
