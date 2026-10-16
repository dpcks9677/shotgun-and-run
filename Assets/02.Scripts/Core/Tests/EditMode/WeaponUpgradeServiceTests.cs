using System.Collections.Generic;
using InventorySystem;
using NUnit.Framework;
using ShotgunAndRun.Weapon;
using UnityEditor;

namespace ShotgunAndRun.Core.Tests
{
    public sealed class WeaponUpgradeServiceTests
    {
        private const string CatalogPath =
            "Assets/07.Datas/WeaponUpgradeCatalog.asset";

        private WeaponUpgradeCatalogSO _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<WeaponUpgradeCatalogSO>(
                CatalogPath);
            Assert.That(_catalog, Is.Not.Null);
        }

        [Test]
        public void TryUpgrade_WithEnoughStoredItems_ConsumesCostAndLevelsUp()
        {
            WeaponUpgradeState state = new WeaponUpgradeState();
            TestItemInventory inventory = new TestItemInventory();
            WeaponUpgradeService service = new WeaponUpgradeService(
                state,
                _catalog,
                inventory);

            Assert.That(
                service.TryGetNextUpgradeCosts(
                    WeaponPartsType.Barrel,
                    out MaterialAmount[] costs),
                Is.True);
            Assert.That(costs, Has.Length.EqualTo(3));
            AssertCost(costs, MaterialItemIds.IronIngot, 3);
            AssertCost(costs, MaterialItemIds.DwarvenMechanism, 2);
            AssertCost(costs, MaterialItemIds.TrollOil, 2);

            foreach (MaterialAmount cost in costs)
            {
                inventory.Add(cost.Item, cost.Amount);
            }

            Assert.That(
                service.TryUpgrade(WeaponPartsType.Barrel, isShelter: true),
                Is.True);
            Assert.That(state.BarrelLevel, Is.EqualTo(2));

            foreach (MaterialAmount cost in costs)
            {
                Assert.That(inventory.GetItemCount(cost.Item), Is.Zero);
            }
        }

        [Test]
        public void TryUpgrade_WithoutEnoughStoredItems_ChangesNothing()
        {
            WeaponUpgradeState state = new WeaponUpgradeState();
            TestItemInventory inventory = new TestItemInventory();
            WeaponUpgradeService service = new WeaponUpgradeService(
                state,
                _catalog,
                inventory);

            Assert.That(
                service.TryGetNextUpgradeCosts(
                    WeaponPartsType.Barrel,
                    out MaterialAmount[] costs),
                Is.True);

            MaterialAmount firstCost = costs[0];
            inventory.Add(firstCost.Item, firstCost.Amount);

            Assert.That(
                service.TryUpgrade(WeaponPartsType.Barrel, isShelter: true),
                Is.False);
            Assert.That(state.BarrelLevel, Is.EqualTo(1));
            Assert.That(
                inventory.GetItemCount(firstCost.Item),
                Is.EqualTo(firstCost.Amount));
        }

        private static void AssertCost(
            IEnumerable<MaterialAmount> costs,
            string itemId,
            int amount)
        {
            foreach (MaterialAmount cost in costs)
            {
                if (cost.Item.Id == itemId)
                {
                    Assert.That(cost.Amount, Is.EqualTo(amount));
                    return;
                }
            }

            Assert.Fail($"강화 비용에 '{itemId}' 아이템이 없습니다.");
        }

        private sealed class TestItemInventory : IItemInventory
        {
            private readonly Dictionary<ItemData, int> _amounts = new();

            public void Add(ItemData itemData, int amount)
            {
                _amounts.TryGetValue(itemData, out int currentAmount);
                _amounts[itemData] = currentAmount + amount;
            }

            public int GetItemCount(ItemData itemData)
            {
                return itemData != null &&
                       _amounts.TryGetValue(itemData, out int amount)
                    ? amount
                    : 0;
            }

            public bool HasItem(ItemData itemData, int amount)
            {
                return amount > 0 && GetItemCount(itemData) >= amount;
            }

            public bool TryRemoveItem(ItemData itemData, int amount)
            {
                if (!HasItem(itemData, amount))
                {
                    return false;
                }

                _amounts[itemData] -= amount;
                return true;
            }
        }
    }
}
