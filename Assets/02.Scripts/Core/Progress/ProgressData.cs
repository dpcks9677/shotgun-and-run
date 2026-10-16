using System;
using System.Collections.Generic;
using InventorySystem;
using ShotgunAndRun.Quest;
using ShotgunAndRun.Weapon;

namespace ShotgunAndRun.Core
{
    // 쉘터에서 유지되는 진행 데이터이자 세이브 파일에 저장되는 데이터입니다.
    // 런 중 데이터(타이머, 가방, 런 XP)는 RunSession에 두고 여기에 넣지 않습니다.
    // JsonUtility로 저장하므로 Dictionary 대신 List를 사용합니다.
    [Serializable]
    public sealed class ProgressData
    {
        // 3: v1.5 퀘스트 체인(QuestIndex · QuestConditionMet)을 Quests로 교체. 폐기된 체인이라 옮기지 않습니다.
        // 4: 퀘스트 상태별 ID 목록을 퀘스트별 기록(Quests.Records)으로 교체. SaveSystem이 버전 3 목록을 옮깁니다.
        public const int CurrentSaveVersion = 4;

        public int SaveVersion = CurrentSaveVersion;

        public WeaponUpgradeState WeaponParts = new WeaponUpgradeState();

        // ScriptableObject 참조 대신 ItemData.Id를 저장합니다.
        public List<StoredItemAmount> Storage = new List<StoredItemAmount>();

        public int PlayerLevel = 1;
        public int PlayerXp;

        // 퀘스트 수주 · 완료와 영구 플래그 (game-spec 9.3)
        public QuestProgress Quests = new QuestProgress();

        // 씬 전환 시 유지되는 소모품 퀵슬롯 데이터 (소모품 타입명, 수량)
        public List<QuickSlotRecord> QuickSlots = new List<QuickSlotRecord>();

        public const int DefaultConsumableStorageSlotCount = 10;

        // 쉘터 창고에 보관되는 소모품 슬롯 데이터 (인덱스 = 슬롯 번호)
        public List<QuickSlotRecord> ConsumableStorage = new List<QuickSlotRecord>();

        public void EnsureConsumableStorageCapacity(int capacity = DefaultConsumableStorageSlotCount)
        {
            ConsumableStorage ??= new List<QuickSlotRecord>();
            while (ConsumableStorage.Count < capacity)
            {
                ConsumableStorage.Add(new QuickSlotRecord(string.Empty, 0));
            }
        }

        public int GetStoredAmount(string itemId)
        {
            int index = FindStorageIndex(itemId);
            return index >= 0 ? Storage[index].Amount : 0;
        }

        public int GetStoredAmount(MaterialType materialType)
        {
            return GetStoredAmount(MaterialItemIds.GetItemId(materialType));
        }

        public void AddToStorage(string itemId, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            {
                return;
            }

            int index = FindStorageIndex(itemId);
            if (index < 0)
            {
                Storage.Add(new StoredItemAmount(itemId, amount));
                return;
            }

            StoredItemAmount stored = Storage[index];
            stored.Amount += amount;
            Storage[index] = stored;
        }

        public void AddToStorage(MaterialType materialType, int amount)
        {
            AddToStorage(MaterialItemIds.GetItemId(materialType), amount);
        }

        public bool TryRemoveFromStorage(string itemId, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            {
                return false;
            }

            int index = FindStorageIndex(itemId);
            if (index < 0 || Storage[index].Amount < amount)
            {
                return false;
            }

            StoredItemAmount stored = Storage[index];
            stored.Amount -= amount;

            if (stored.Amount <= 0)
            {
                Storage.RemoveAt(index);
            }
            else
            {
                Storage[index] = stored;
            }

            return true;
        }

        public bool TryRemoveFromStorage(
            MaterialType materialType,
            int amount)
        {
            return TryRemoveFromStorage(
                MaterialItemIds.GetItemId(materialType),
                amount);
        }

        public void Normalize()
        {
            WeaponParts ??= new WeaponUpgradeState();
            Storage ??= new List<StoredItemAmount>();
            Quests ??= new QuestProgress();
            Quests.Normalize();
            QuickSlots ??= new List<QuickSlotRecord>();
            ConsumableStorage ??= new List<QuickSlotRecord>();
            while (ConsumableStorage.Count < DefaultConsumableStorageSlotCount)
            {
                ConsumableStorage.Add(new QuickSlotRecord(string.Empty, 0));
            }

            Dictionary<string, int> normalizedAmounts =
                new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (StoredItemAmount stored in Storage)
            {
                if (string.IsNullOrWhiteSpace(stored.ItemId) || stored.Amount <= 0)
                {
                    continue;
                }

                normalizedAmounts.TryGetValue(stored.ItemId, out int amount);
                normalizedAmounts[stored.ItemId] = amount + stored.Amount;
            }

            Storage.Clear();
            foreach (KeyValuePair<string, int> pair in normalizedAmounts)
            {
                Storage.Add(new StoredItemAmount(pair.Key, pair.Value));
            }
        }

        private int FindStorageIndex(string itemId)
        {
            for (int i = 0; i < Storage.Count; i++)
            {
                if (string.Equals(
                        Storage[i].ItemId,
                        itemId,
                        StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }

    [Serializable]
    public struct QuickSlotRecord
    {
        public string ConsumableTypeName;
        public int Amount;

        public QuickSlotRecord(string consumableTypeName, int amount)
        {
            ConsumableTypeName = consumableTypeName;
            Amount = amount;
        }
    }
}
