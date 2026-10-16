using Consumable;
using NUnit.Framework;
using UnityEngine;

namespace ShotgunAndRun.Core.Tests
{
    [TestFixture]
    public class ConsumableStorageTests
    {
        [Test]
        public void EnsureConsumableStorageCapacity_InitializesWithDefaultSlots()
        {
            ProgressData progress = new ProgressData();
            Assert.That(progress.ConsumableStorage, Is.Empty);

            progress.EnsureConsumableStorageCapacity();

            Assert.That(progress.ConsumableStorage.Count, Is.EqualTo(ProgressData.DefaultConsumableStorageSlotCount));
            for (int i = 0; i < progress.ConsumableStorage.Count; i++)
            {
                Assert.That(progress.ConsumableStorage[i].ConsumableTypeName, Is.Empty);
                Assert.That(progress.ConsumableStorage[i].Amount, Is.EqualTo(0));
            }
        }

        [Test]
        public void Normalize_PreservesExistingConsumableStorageAndExpands()
        {
            ProgressData progress = new ProgressData();
            progress.ConsumableStorage.Add(new QuickSlotRecord("SmallKit", 2));
            progress.ConsumableStorage.Add(new QuickSlotRecord("Adrenaline", 1));

            progress.Normalize();

            Assert.That(progress.ConsumableStorage.Count, Is.EqualTo(ProgressData.DefaultConsumableStorageSlotCount));
            Assert.That(progress.ConsumableStorage[0].ConsumableTypeName, Is.EqualTo("SmallKit"));
            Assert.That(progress.ConsumableStorage[0].Amount, Is.EqualTo(2));
            Assert.That(progress.ConsumableStorage[1].ConsumableTypeName, Is.EqualTo("Adrenaline"));
            Assert.That(progress.ConsumableStorage[1].Amount, Is.EqualTo(1));
            Assert.That(progress.ConsumableStorage[2].ConsumableTypeName, Is.Empty);
        }

        [Test]
        public void ConsumableCatalog_FindByName_CaseInsensitive()
        {
            ConsumableCatalog catalog = ScriptableObject.CreateInstance<ConsumableCatalog>();
            ConsumableData testItem = ScriptableObject.CreateInstance<ConsumableData>();
            testItem.name = "SmallKit";

            // reflection to set _consumableDatas
            var field = typeof(ConsumableCatalog).GetField("_consumableDatas", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(catalog, new System.Collections.Generic.List<ConsumableData> { testItem });

            ConsumableData foundUpper = catalog.FindConsumableDataByName("SmallKit");
            ConsumableData foundLower = catalog.FindConsumableDataByName("smallkit");

            Assert.That(foundUpper, Is.SameAs(testItem));
            Assert.That(foundLower, Is.SameAs(testItem));
        }

        [Test]
        public void ProgressData_QuickSlots_CanSaveAndRestoreViaSaveSystem()
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "QuickSlotSaveTest_" + System.Guid.NewGuid());
            SaveSystem saveSys = new SaveSystem(System.IO.Path.Combine(dir, "save.json"));

            try
            {
                ProgressData data = new ProgressData();
                data.QuickSlots.Add(new QuickSlotRecord("SmallKit", 3));
                data.QuickSlots.Add(new QuickSlotRecord(string.Empty, 0));
                data.QuickSlots.Add(new QuickSlotRecord("Adrenaline", 1));

                saveSys.Save(data);

                ProgressData loaded = saveSys.Load();
                Assert.That(loaded.QuickSlots, Is.Not.Null);
                Assert.That(loaded.QuickSlots.Count, Is.EqualTo(3));
                Assert.That(loaded.QuickSlots[0].ConsumableTypeName, Is.EqualTo("SmallKit"));
                Assert.That(loaded.QuickSlots[0].Amount, Is.EqualTo(3));
                Assert.That(loaded.QuickSlots[1].ConsumableTypeName, Is.Empty);
                Assert.That(loaded.QuickSlots[1].Amount, Is.EqualTo(0));
                Assert.That(loaded.QuickSlots[2].ConsumableTypeName, Is.EqualTo("Adrenaline"));
                Assert.That(loaded.QuickSlots[2].Amount, Is.EqualTo(1));
            }
            finally
            {
                if (System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.Delete(dir, true);
                }
            }
        }
    }
}
