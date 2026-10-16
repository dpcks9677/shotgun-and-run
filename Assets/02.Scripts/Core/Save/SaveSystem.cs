using System;
using System.Collections.Generic;
using System.IO;
using InventorySystem;
using ShotgunAndRun.Quest;
using UnityEngine;

namespace ShotgunAndRun.Core
{
    // ProgressData를 JSON 파일로 저장하고 불러옵니다.
    public sealed class SaveSystem
    {
        public const string DefaultFileName = "save.json";

        public SaveSystem(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("Save file path is empty.", nameof(filePath));
            }

            FilePath = filePath;
        }

        public string FilePath
        {
            get;
        }
        public bool HasSave => File.Exists(FilePath);

        // true인 동안 Save는 파일을 쓰지 않습니다. 컷씬 도중의 중간 상태가 저장되지 않게 합니다.
        public bool IsHeld
        {
            get;
            set;
        }

        public static SaveSystem CreateDefault()
        {
            return new SaveSystem(Path.Combine(Application.persistentDataPath, DefaultFileName));
        }

        // 저장 파일이 없거나 읽을 수 없으면 새 진행 데이터를 반환합니다.
        public ProgressData Load()
        {
            ProgressData data = new ProgressData();
            if (!HasSave)
            {
                return data;
            }

            try
            {
                string json = File.ReadAllText(FilePath);

                // 기본값 객체 위에 덮어쓰므로, 저장 파일에 없는 새 필드는 기본값을 유지합니다.
                JsonUtility.FromJsonOverwrite(json, data);

                if (data.SaveVersion < 2)
                {
                    MigrateVersion1Storage(json, data);
                }

                if (data.SaveVersion == 3)
                {
                    MigrateVersion3Quests(json, data);
                }

                data.Normalize();
                data.SaveVersion = ProgressData.CurrentSaveVersion;
                return data;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to load save file. Starting with new progress. {exception.Message}");
                return new ProgressData();
            }
        }

        // 임시 파일에 먼저 쓴 뒤 교체하여, 쓰는 도중 종료되어도 기존 저장을 보존합니다.
        public void Save(ProgressData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (IsHeld)
            {
                return;
            }

            data.Normalize();
            data.SaveVersion = ProgressData.CurrentSaveVersion;
            string directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string tempPath = FilePath + ".tmp";
            File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));

            if (File.Exists(FilePath))
            {
                File.Replace(tempPath, FilePath, null);
            }
            else
            {
                File.Move(tempPath, FilePath);
            }
        }

        public void Delete()
        {
            if (HasSave)
            {
                File.Delete(FilePath);
            }
        }

        private static void MigrateVersion1Storage(
            string json,
            ProgressData data)
        {
            LegacyStorageData legacyData =
                JsonUtility.FromJson<LegacyStorageData>(json);

            data.Storage ??= new List<StoredItemAmount>();
            data.Storage.Clear();
            if (legacyData?.Storage == null)
            {
                return;
            }

            foreach (LegacyMaterialAmount legacyItem in legacyData.Storage)
            {
                if (MaterialItemIds.TryGetItemId(
                        legacyItem.Type,
                        out string itemId))
                {
                    data.AddToStorage(itemId, legacyItem.Amount);
                }
            }
        }

        private static void MigrateVersion3Quests(string json, ProgressData data)
        {
            LegacyQuestSaveData legacyData = JsonUtility.FromJson<LegacyQuestSaveData>(json);
            LegacyQuestProgress legacy = legacyData?.Quests;
            if (legacy == null)
            {
                return;
            }

            data.Quests ??= new QuestProgress();
            data.Quests.ImportLegacyLists(
                legacy.AcceptedQuestIds,
                legacy.ReadyQuestIds,
                legacy.CompletedQuestIds,
                legacy.PendingStampQuestIds,
                legacy.ZoneVisitedQuestIds);
        }

        [Serializable]
        private sealed class LegacyQuestSaveData
        {
            public LegacyQuestProgress Quests;
        }

        // 세이브 버전 3의 퀘스트 진행 (상태별 ID 목록)
        [Serializable]
        private sealed class LegacyQuestProgress
        {
            public List<string> AcceptedQuestIds;
            public List<string> ReadyQuestIds;
            public List<string> CompletedQuestIds;
            public List<string> PendingStampQuestIds;
            public List<string> ZoneVisitedQuestIds;
        }

        [Serializable]
        private sealed class LegacyStorageData
        {
            public List<LegacyMaterialAmount> Storage;
        }

        [Serializable]
        private struct LegacyMaterialAmount
        {
            public MaterialType Type;
            public int Amount;
        }
    }
}
