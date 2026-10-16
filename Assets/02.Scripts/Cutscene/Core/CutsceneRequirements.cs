using System.Collections.Generic;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 컷씬이 재생에 필요로 하는 인물 · 마커 · 신호 · 에셋 목록입니다.
    /// CutsceneAsset.DeclareRequirements에서 채우며, 재생 전 사전 검사와 에디터 검증이 이 목록을 사용합니다.
    /// </summary>
    public sealed class CutsceneRequirements
    {
        private readonly List<CharacterProfileSO> _actors = new List<CharacterProfileSO>();
        private readonly List<string> _markers = new List<string>();
        private readonly List<string> _signals = new List<string>();
        private readonly List<string> _crowds = new List<string>();
        private readonly List<DialogueDataSO> _dialogues = new List<DialogueDataSO>();
        private readonly List<string> _errors = new List<string>();

        public IReadOnlyList<CharacterProfileSO> Actors => _actors;
        public IReadOnlyList<string> Markers => _markers;
        public IReadOnlyList<string> Signals => _signals;
        public IReadOnlyList<string> Crowds => _crowds;
        public IReadOnlyList<DialogueDataSO> Dialogues => _dialogues;

        /// <summary>
        /// 적 소환 명령을 쓰는지 (씬에 CutsceneEnemySpawner가 있어야 함)
        /// </summary>
        public bool NeedsEnemySpawner
        {
            get;
            private set;
        }

        /// <summary>
        /// 에셋 자체의 문제 (비어 있는 필드 등). 씬과 관계없이 발견되는 오류입니다.
        /// </summary>
        public IReadOnlyList<string> Errors => _errors;

        /// <summary>
        /// 등장인물을 선언합니다. fieldName에는 nameof(필드)를 넘겨 비어 있을 때 어느 칸인지 알 수 있게 합니다.
        /// </summary>
        public void Actor(CharacterProfileSO profile, string fieldName)
        {
            if (profile == null)
            {
                _errors.Add($"등장인물 칸 '{fieldName}'이 비어 있습니다.");
                return;
            }

            AddUnique(_actors, profile);
        }

        /// <summary>
        /// 씬에 있어야 하는 마커 키를 선언합니다.
        /// </summary>
        public void Marker(params string[] keys)
        {
            foreach (string key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    _errors.Add("비어 있는 마커 키가 선언되었습니다.");
                    continue;
                }

                AddUnique(_markers, key);
            }
        }

        /// <summary>
        /// 씬에 있어야 하는 연출용 무리(CutsceneCrowd) 키를 선언합니다.
        /// </summary>
        public void Crowd(params string[] keys)
        {
            foreach (string key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    _errors.Add("비어 있는 무리 키가 선언되었습니다.");
                    continue;
                }

                AddUnique(_crowds, key);
            }
        }

        /// <summary>
        /// 적 소환 명령(SpawnEnemyLine 등)을 쓴다고 선언합니다. 씬에 CutsceneEnemySpawner가 있어야 합니다.
        /// </summary>
        public void EnemySpawner()
        {
            NeedsEnemySpawner = true;
        }

        /// <summary>
        /// 씬에 있어야 하는 신호(CutsceneSignal) 키를 선언합니다.
        /// </summary>
        public void Signal(params string[] keys)
        {
            foreach (string key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    _errors.Add("비어 있는 신호 키가 선언되었습니다.");
                    continue;
                }

                AddUnique(_signals, key);
            }
        }

        /// <summary>
        /// 컷씬에서 출력할 대사를 선언합니다. 대사별 말풍선 화자와 신호 키도 함께 필요 목록에 추가됩니다.
        /// </summary>
        public void Dialogue(DialogueDataSO dialogue, string fieldName)
        {
            if (dialogue == null)
            {
                _errors.Add($"대사 칸 '{fieldName}'이 비어 있습니다.");
                return;
            }

            if (!AddUnique(_dialogues, dialogue)) return;

            if (dialogue.PauseGame)
            {
                _errors.Add(
                    $"대사 '{dialogue.name}'의 Pause Game이 켜져 있습니다. 컷씬 연출은 게임 시간으로 움직이므로 컷씬 대사는 Pause Game을 꺼야 합니다.");
            }

            var lines = dialogue.Lines;
            for (int i = 0; i < lines.Count; i++)
            {
                DialogueLine line = lines[i];
                if (line.Bubble != EmotionType.None)
                {
                    if (line.Speaker == null)
                    {
                        _errors.Add(
                            $"대사 '{dialogue.name}'의 {i + 1}번째 줄에 말풍선({line.Bubble})이 있지만 화자 프로필(Speaker)이 비어 있습니다.");
                    }
                    else
                    {
                        AddUnique(_actors, line.Speaker);
                    }
                }

                if (!string.IsNullOrWhiteSpace(line.Signal))
                {
                    AddUnique(_signals, line.Signal);
                }
            }
        }

        /// <summary>
        /// 그 밖에 반드시 채워져 있어야 하는 에셋 칸(효과음 · 애니메이션 클립 등)을 선언합니다.
        /// </summary>
        public void Asset(Object asset, string fieldName)
        {
            if (asset == null)
            {
                _errors.Add($"에셋 칸 '{fieldName}'이 비어 있습니다.");
            }
        }

        /// <summary>
        /// 컷씬별 추가 조건을 검사합니다. condition이 false면 message를 오류로 남깁니다. (예: 대사에 필요한 선택지 ID가 있는지)
        /// </summary>
        public void Check(bool condition, string message)
        {
            if (!condition)
            {
                _errors.Add(message);
            }
        }

        internal void AddError(string message)
        {
            _errors.Add(message);
        }

        private static bool AddUnique<T>(List<T> list, T item)
        {
            if (list.Contains(item)) return false;
            list.Add(item);
            return true;
        }
    }
}
