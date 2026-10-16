using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShotgunAndRun.Quest
{
    // 세이브에 저장하는 퀘스트 상태. 잠김 · 수주 가능은 선행 조건으로 계산하므로 저장하지 않습니다.
    public enum QuestStatus
    {
        NotAccepted,
        Accepted,
        ReadyToTurnIn,  // 탈출 판정은 통과했고 보고만 남음. 수주 칸을 차지합니다.
        Completed
    }

    // 퀘스트 1개의 저장 기록입니다. 세이브 파일을 손으로 고치기 쉽도록 상태를 문자열로 저장합니다.
    [Serializable]
    public sealed class QuestRecord
    {
        public string Id;
        public string Status = nameof(QuestStatus.NotAccepted);

        // 수주한 순서 (게시판 카드 순서). 수주 중이 아니면 0
        public int AcceptedOrder;

        // 완료 처리됐지만 게시판에서 완료 도장을 아직 보여주지 않음. 게시판을 닫을 때 끕니다.
        public bool StampPending;

        // 해금 조건을 채운 뒤 관련 존을 방문하고 탈출까지 마침 (구역 정화 해금, game-spec 9.3)
        public bool ZoneVisited;

        public QuestStatus GetStatus()
        {
            return Enum.TryParse(Status, out QuestStatus status) && Enum.IsDefined(typeof(QuestStatus), status)
                ? status
                : QuestStatus.NotAccepted;
        }
    }

    // 세이브 파일에 저장되는 퀘스트 진행입니다. ProgressData가 필드로 가지고 있습니다.
    // 퀘스트마다 기록 하나(ID → 상태)를 두어 수주 · 완료가 동시에 켜지는 모순이 생기지 않게 합니다.
    // JsonUtility는 Dictionary를 저장하지 못하므로 목록으로 저장하고, 조회는 ID 표로 합니다.
    // 수주 · 완료 처리는 QuestService를 통해서만 바꿉니다. (SetStatus는 세이브 변환 · 디버그 · 테스트용)
    [Serializable]
    public sealed class QuestProgress
    {
        public List<QuestRecord> Records = new List<QuestRecord>();

        // 런의 결과와 상관없이 유지되는 영구 플래그 (랜덤 스폰 해금, 기믹 가동 등)
        public List<string> Flags = new List<string>();

        [NonSerialized] private Dictionary<string, QuestRecord> _recordsById;
        [NonSerialized] private List<QuestRecord> _indexedRecords;
        [NonSerialized] private int _indexedCount;

        public QuestStatus GetStatus(string questId)
        {
            return TryGetRecord(questId, out QuestRecord record) ? record.GetStatus() : QuestStatus.NotAccepted;
        }

        // 완료 가능 퀘스트도 수주 칸을 차지하므로 수주 중으로 봅니다.
        public bool IsAccepted(string questId)
        {
            QuestStatus status = GetStatus(questId);
            return status == QuestStatus.Accepted || status == QuestStatus.ReadyToTurnIn;
        }

        public bool IsCompleted(string questId)
        {
            return GetStatus(questId) == QuestStatus.Completed;
        }

        public bool IsReady(string questId)
        {
            return GetStatus(questId) == QuestStatus.ReadyToTurnIn;
        }

        public bool IsStampPending(string questId)
        {
            return TryGetRecord(questId, out QuestRecord record) && record.StampPending;
        }

        public bool IsZoneVisited(string questId)
        {
            return TryGetRecord(questId, out QuestRecord record) && record.ZoneVisited;
        }

        // 수주 중(완료 가능 포함)인 퀘스트 ID를 수주한 순서대로 반환합니다.
        public List<string> GetAcceptedIds()
        {
            List<QuestRecord> accepted = Records.FindAll(record => IsAcceptedStatus(record.GetStatus()));
            accepted.Sort((a, b) => a.AcceptedOrder.CompareTo(b.AcceptedOrder));
            return accepted.ConvertAll(record => record.Id);
        }

        public List<string> GetPendingStampIds()
        {
            return Records.FindAll(record => record.StampPending).ConvertAll(record => record.Id);
        }

        public bool HasFlag(string flag)
        {
            return !string.IsNullOrEmpty(flag) && Flags.Contains(flag);
        }

        // 기믹 가동처럼 퀘스트 완료와 별개로 바로 저장해야 하는 플래그도 여기에 켭니다.
        // 새로 켜졌으면 true
        public bool AddFlag(string flag)
        {
            if (string.IsNullOrWhiteSpace(flag) || Flags.Contains(flag))
            {
                return false;
            }

            Flags.Add(flag);
            return true;
        }

        // 세이브 변환 · 디버그 · 테스트용으로 상태를 직접 정합니다. 게임 진행 중에는 QuestService를 통해 바꿉니다.
        public void SetStatus(string questId, QuestStatus status)
        {
            QuestRecord record = GetOrCreateRecord(questId);
            if (record == null)
            {
                return;
            }

            bool wasAccepted = IsAcceptedStatus(record.GetStatus());
            record.Status = status.ToString();
            if (!IsAcceptedStatus(status))
            {
                record.AcceptedOrder = 0;
            }
            else if (!wasAccepted || record.AcceptedOrder <= 0)
            {
                record.AcceptedOrder = NextAcceptedOrder();
            }
        }

        // 세이브 버전 3의 상태별 ID 목록을 기록으로 옮깁니다. 여러 목록에 있으면 완료 > 완료 가능 > 수주 순으로 하나만 남깁니다.
        public void ImportLegacyLists(
            IEnumerable<string> accepted,
            IEnumerable<string> ready,
            IEnumerable<string> completed,
            IEnumerable<string> pendingStamps,
            IEnumerable<string> zoneVisited)
        {
            foreach (string questId in accepted ?? Array.Empty<string>())
            {
                if (GetStatus(questId) == QuestStatus.NotAccepted) SetStatus(questId, QuestStatus.Accepted);
            }

            // 완료 가능은 수주 목록에도 있어야 했다
            foreach (string questId in ready ?? Array.Empty<string>())
            {
                if (GetStatus(questId) == QuestStatus.Accepted) SetStatus(questId, QuestStatus.ReadyToTurnIn);
            }

            foreach (string questId in completed ?? Array.Empty<string>())
            {
                SetStatus(questId, QuestStatus.Completed);
            }

            foreach (string questId in pendingStamps ?? Array.Empty<string>())
            {
                if (IsCompleted(questId) && TryGetRecord(questId, out QuestRecord record)) record.StampPending = true;
            }

            foreach (string questId in zoneVisited ?? Array.Empty<string>())
            {
                MarkZoneVisited(questId);
            }
        }

        // 카탈로그의 퀘스트마다 기록을 만들고 카탈로그 순서로 정렬합니다. 세이브 파일에서 모든 퀘스트가 보이도록 합니다.
        // 카탈로그에 없는 기록(삭제된 퀘스트 등)은 지우지 않고 뒤에 둡니다.
        public void EnsureRecords(IEnumerable<string> questIds)
        {
            List<QuestRecord> ordered = new List<QuestRecord>();
            HashSet<string> added = new HashSet<string>(StringComparer.Ordinal);
            foreach (string questId in questIds)
            {
                if (string.IsNullOrWhiteSpace(questId) || !added.Add(questId))
                {
                    continue;
                }

                ordered.Add(GetOrCreateRecord(questId));
            }

            foreach (QuestRecord record in Records)
            {
                if (!added.Contains(record.Id))
                {
                    Debug.LogWarning($"[QuestProgress] 카탈로그에 없는 퀘스트 기록입니다: '{record.Id}'");
                    ordered.Add(record);
                }
            }

            Records = ordered;
        }

        // 중복 · 빈 기록, 알 수 없는 상태 문자열, 상태와 맞지 않는 값을 정리합니다. (불러온 직후 · 저장 직전)
        public void Normalize()
        {
            List<QuestRecord> records = new List<QuestRecord>();
            Dictionary<string, QuestRecord> byId = new Dictionary<string, QuestRecord>(StringComparer.Ordinal);
            foreach (QuestRecord record in Records ?? new List<QuestRecord>())
            {
                if (record == null || string.IsNullOrWhiteSpace(record.Id))
                {
                    continue;
                }

                if (!Enum.TryParse(record.Status, out QuestStatus parsed) || !Enum.IsDefined(typeof(QuestStatus), parsed))
                {
                    Debug.LogWarning($"[QuestProgress] '{record.Id}'의 상태 '{record.Status}'를 알 수 없어 NotAccepted로 바꿉니다.");
                }

                record.Status = record.GetStatus().ToString();

                // 같은 ID가 여러 번 있으면 더 진행된 상태 하나만 남긴다 (완료 > 완료 가능 > 수주 > 미수주)
                if (byId.TryGetValue(record.Id, out QuestRecord existing))
                {
                    if (record.GetStatus() > existing.GetStatus())
                    {
                        existing.Status = record.Status;
                        existing.AcceptedOrder = record.AcceptedOrder;
                    }

                    existing.StampPending |= record.StampPending;
                    existing.ZoneVisited |= record.ZoneVisited;
                    continue;
                }

                byId.Add(record.Id, record);
                records.Add(record);
            }

            Records = records;

            foreach (QuestRecord record in Records)
            {
                QuestStatus status = record.GetStatus();

                // 도장 대기는 완료한 퀘스트만 (손상된 세이브가 진행 중 퀘스트를 도장 카드로 띄우지 않게)
                if (status != QuestStatus.Completed)
                {
                    record.StampPending = false;
                }

                if (!IsAcceptedStatus(status))
                {
                    record.AcceptedOrder = 0;
                }
            }

            // 수주 순번이 없거나 겹치면 기존 순서를 지켜 1부터 다시 매긴다
            List<QuestRecord> accepted = Records.FindAll(record => IsAcceptedStatus(record.GetStatus()));
            accepted.Sort((a, b) =>
            {
                int byOrder = OrderKey(a).CompareTo(OrderKey(b));
                return byOrder != 0 ? byOrder : Records.IndexOf(a).CompareTo(Records.IndexOf(b));
            });
            for (int i = 0; i < accepted.Count; i++)
            {
                accepted[i].AcceptedOrder = i + 1;
            }

            List<string> flags = new List<string>();
            foreach (string flag in Flags ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(flag) && !flags.Contains(flag))
                {
                    flags.Add(flag);
                }
            }

            Flags = flags;
            _indexedRecords = null;
        }

        // 새로 수주했으면 true
        internal bool Accept(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId) || GetStatus(questId) != QuestStatus.NotAccepted)
            {
                return false;
            }

            SetStatus(questId, QuestStatus.Accepted);
            return true;
        }

        // 수주(완료 가능 포함)를 취소했으면 true
        internal bool Abandon(string questId)
        {
            if (!IsAccepted(questId))
            {
                return false;
            }

            SetStatus(questId, QuestStatus.NotAccepted);
            return true;
        }

        // 수주 중인 퀘스트를 완료 가능으로 표시합니다. 새로 표시됐으면 true
        internal bool MarkReady(string questId)
        {
            if (GetStatus(questId) != QuestStatus.Accepted)
            {
                return false;
            }

            SetStatus(questId, QuestStatus.ReadyToTurnIn);
            return true;
        }

        // 보고: 완료로 바꾸고 완료 도장을 대기시킵니다.
        internal void Complete(string questId)
        {
            QuestRecord record = GetOrCreateRecord(questId);
            if (record == null)
            {
                return;
            }

            SetStatus(questId, QuestStatus.Completed);
            record.StampPending = true;
        }

        // 대기 중인 도장이 있었으면 true
        internal bool ClearPendingStamps()
        {
            bool cleared = false;
            foreach (QuestRecord record in Records)
            {
                cleared |= record.StampPending;
                record.StampPending = false;
            }

            return cleared;
        }

        internal void MarkZoneVisited(string questId)
        {
            QuestRecord record = GetOrCreateRecord(questId);
            if (record != null)
            {
                record.ZoneVisited = true;
            }
        }

        private bool TryGetRecord(string questId, out QuestRecord record)
        {
            record = null;
            if (string.IsNullOrEmpty(questId))
            {
                return false;
            }

            // FromJsonOverwrite나 EnsureRecords로 목록이 바뀌면 ID 표를 다시 만든다
            if (_recordsById == null || !ReferenceEquals(_indexedRecords, Records) || _indexedCount != Records.Count)
            {
                RebuildIndex();
            }

            return _recordsById.TryGetValue(questId, out record);
        }

        private QuestRecord GetOrCreateRecord(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId))
            {
                return null;
            }

            if (TryGetRecord(questId, out QuestRecord record))
            {
                return record;
            }

            record = new QuestRecord { Id = questId };
            Records.Add(record);
            _recordsById[questId] = record;
            _indexedCount = Records.Count;
            return record;
        }

        private void RebuildIndex()
        {
            Records ??= new List<QuestRecord>();
            _recordsById = new Dictionary<string, QuestRecord>(StringComparer.Ordinal);
            foreach (QuestRecord record in Records)
            {
                if (record != null && !string.IsNullOrEmpty(record.Id) && !_recordsById.ContainsKey(record.Id))
                {
                    _recordsById.Add(record.Id, record);
                }
            }

            _indexedRecords = Records;
            _indexedCount = Records.Count;
        }

        private int NextAcceptedOrder()
        {
            int max = 0;
            foreach (QuestRecord record in Records)
            {
                max = Math.Max(max, record.AcceptedOrder);
            }

            return max + 1;
        }

        // 순번이 없는(0) 수주 기록은 기존 순번 있는 기록 뒤로 보낸다
        private static int OrderKey(QuestRecord record)
        {
            return record.AcceptedOrder > 0 ? record.AcceptedOrder : int.MaxValue;
        }

        private static bool IsAcceptedStatus(QuestStatus status)
        {
            return status == QuestStatus.Accepted || status == QuestStatus.ReadyToTurnIn;
        }
    }
}
