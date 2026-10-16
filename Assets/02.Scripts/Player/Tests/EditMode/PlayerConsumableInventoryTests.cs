using System.Collections.Generic;
using Consumable;
using NUnit.Framework;
using UnityEngine;

namespace ShotgunAndRun.Player.Tests
{
    [TestFixture]
    public class PlayerConsumableInventoryTests
    {
        [Test]
        public void RestoreFromSnapshots_ClearsEmptySlotsProperly()
        {
            GameObject go = new GameObject("TestPlayerConsumableInventory");
            var inv = go.AddComponent<PlayerConsumableInventory>();
            inv.InitializeSlots();

            ConsumableCatalog catalog = ScriptableObject.CreateInstance<ConsumableCatalog>();
            ConsumableData kitData = ScriptableObject.CreateInstance<ConsumableData>();
            kitData.name = "SmallKit";
            var typeField = typeof(ConsumableData).GetField("_consumableType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (typeField != null)
            {
                typeField.SetValue(kitData, ConsumableType.SmallKit);
            }

            var catListField = typeof(ConsumableCatalog).GetField("_consumableDatas", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            catListField.SetValue(catalog, new List<ConsumableData> { kitData });

            var catalogField = typeof(PlayerConsumableInventory).GetField("_consumableCatalog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            catalogField.SetValue(inv, catalog);

            // 슬롯 0, 1번에 아이템 설정
            inv.SetSlot(0, kitData, 2);
            inv.SetSlot(1, kitData, 1);
            Assert.That(inv.GetSlot(0).IsEmpty, Is.False);
            Assert.That(inv.GetSlot(1).IsEmpty, Is.False);

            // 빈 스냅샷으로 복원 (플레이어 사망 후 쉘터 진입 시뮬레이션)
            inv.RestoreFromSnapshots(new List<(string, int)>());

            // 모든 슬롯이 빈 슬롯으로 초기화되었는지 검증
            Assert.That(inv.GetSlot(0).IsEmpty, Is.True);
            Assert.That(inv.GetSlot(1).IsEmpty, Is.True);
            Assert.That(inv.GetSlot(2).IsEmpty, Is.True);

            // ClearAll 메서드 동작 검증
            inv.SetSlot(0, kitData, 3);
            Assert.That(inv.GetSlot(0).IsEmpty, Is.False);
            inv.ClearAll();
            Assert.That(inv.GetSlot(0).IsEmpty, Is.True);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(kitData);
        }

        [Test]
        public void RestoreFromSnapshots_PopulatesItemsCorrectly()
        {
            GameObject go = new GameObject("TestPlayerConsumableInventory");
            var inv = go.AddComponent<PlayerConsumableInventory>();
            inv.InitializeSlots();

            ConsumableCatalog catalog = ScriptableObject.CreateInstance<ConsumableCatalog>();
            ConsumableData kitData = ScriptableObject.CreateInstance<ConsumableData>();
            kitData.name = "SmallKit";
            var typeField = typeof(ConsumableData).GetField("_consumableType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (typeField != null)
            {
                typeField.SetValue(kitData, ConsumableType.SmallKit);
            }

            var catListField = typeof(ConsumableCatalog).GetField("_consumableDatas", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            catListField.SetValue(catalog, new List<ConsumableData> { kitData });

            var catalogField = typeof(PlayerConsumableInventory).GetField("_consumableCatalog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            catalogField.SetValue(inv, catalog);

            var snapshots = new List<(string, int)>
            {
                ("SmallKit", 3),
                (string.Empty, 0),
                (string.Empty, 0)
            };

            inv.RestoreFromSnapshots(snapshots);

            Assert.That(inv.GetSlot(0).IsEmpty, Is.False);
            Assert.That(inv.GetSlot(0).Amount, Is.EqualTo(3));
            Assert.That(inv.GetSlot(1).IsEmpty, Is.True);
            Assert.That(inv.GetSlot(2).IsEmpty, Is.True);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(kitData);
        }
    }
}
