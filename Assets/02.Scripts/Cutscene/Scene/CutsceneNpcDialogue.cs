using System;
using System.Collections.Generic;
using System.Linq;
using ShotgunAndRun.Core;
using ShotgunAndRun.NPC;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// NPC와 상호작용(E)하면 대화 대신 컷씬을 재생합니다. NpcInteractable의 Dialogue 칸에 연결합니다.
    /// 컷씬 후보 목록에서 위에서부터 재생 조건이 맞는 첫 컷씬 하나를 재생하고, 재생할 수 있는 컷씬이 없으면 대체 대화(Fallback)를 재생합니다.
    /// 재생할 컷씬이 있는 동안에는 머리 위 퀘스트 마커를 대화 말풍선과 같은 자리에 켜고, 컷씬이 재생되는 동안에는 숨깁니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CutsceneNpcDialogue : MonoBehaviour, INpcDialogue
    {
        [Tooltip("상호작용하면 재생할 컷씬 후보. 위에서부터 재생 조건이 맞는 첫 컷씬 하나를 재생")]
        [SerializeField] private List<CutsceneAsset> _cutscenes = new List<CutsceneAsset>();

        [Tooltip("컷씬을 끝까지 본 뒤 NPC에 연결된 UI(OnInteract)를 열지 여부")]
        [SerializeField] private bool _openUiAfterCutscene;

        [Tooltip("컷씬을 재생할 수 없을 때 대신 재생할 대화 (INpcDialogue 구현 컴포넌트, 예: NpcDialogueBridge)")]
        [SerializeField] private MonoBehaviour _fallbackDialogue;

        [Tooltip("재생할 컷씬이 있을 때 켜는 머리 위 퀘스트 마커 (비워 두면 표시 안 함). 위치는 대화 말풍선 자리로 자동으로 맞춘다")]
        [SerializeField] private GameObject _questMarker;

        private Action _onFinished;
        private CutsceneAsset _playing;

        public bool IsPlaying =>
            (_playing != null && CutsceneDirector.Current != null && CutsceneDirector.Current.Playing == _playing) ||
            (Fallback != null && Fallback.IsPlaying);

        private INpcDialogue Fallback => _fallbackDialogue as INpcDialogue;

        private void OnValidate()
        {
            if (_fallbackDialogue != null && (_fallbackDialogue is not INpcDialogue || _fallbackDialogue == this))
            {
                Debug.LogWarning($"{name}: Fallback Dialogue에는 이 컴포넌트가 아닌 INpcDialogue 구현 컴포넌트만 연결할 수 있습니다.", this);
                _fallbackDialogue = null;
            }
        }

        private void OnEnable()
        {
            CutsceneDirector.CutsceneStarted += HandleAnyCutsceneStarted;
            CutsceneDirector.CutsceneFinished += HandleAnyCutsceneFinished;
            RefreshMarker();
        }

        private void Start()
        {
            RefreshMarker();
        }

        public void Play(NpcInteractable speaker, Action onFinished)
        {
            var director = CutsceneDirector.Current;
            if (_cutscenes == null || _cutscenes.Count == 0 || director == null)
            {
                Debug.LogError($"[CutsceneNpcDialogue] '{name}'에 컷씬이 비어 있거나 씬에 CutsceneDirector가 없습니다.", this);
                return;
            }

            foreach (var cutscene in _cutscenes)
            {
                if (cutscene == null || !director.CanPlay(cutscene)) continue;

                if (director.Play(cutscene))
                {
                    _playing = cutscene;
                    _onFinished = onFinished;
                    CutsceneDirector.CutsceneFinished += HandleCutsceneFinished;
                    return;
                }
                break;
            }

            Fallback?.Play(speaker, onFinished);
        }

        /// <summary>재생할 컷씬이 있으면 퀘스트 마커를 켜고, 없거나 컷씬이 재생 중이면 끕니다.</summary>
        public void RefreshMarker()
        {
            if (_questMarker == null) return;

            bool active = false;
            var director = CutsceneDirector.Current;
            var quests = GameManager.Instance != null ? GameManager.Instance.Progress?.Quests : null;
            if (director != null && quests != null && _cutscenes != null && !CutsceneDirector.IsPlaying)
            {
                foreach (var cutscene in _cutscenes)
                {
                    // Director에 등록되지 않은 컷씬은 E를 눌러도 재생되지 않으므로 마커도 켜지 않는다
                    if (cutscene != null && director.Cutscenes.Contains(cutscene) && director.CanPlay(cutscene))
                    {
                        active = true;
                        break;
                    }
                }
            }

            if (active && director.Bubbles != null)
            {
                // 대화 중 감정 말풍선과 같은 자리 (같은 머리 위치 규칙 · 간격)
                var actor = GetComponentInParent<CutsceneActor>();
                _questMarker.transform.position = director.Bubbles.GetBubblePosition(actor != null ? actor.BubbleTarget : transform);
            }

            _questMarker.SetActive(active);
        }

        private void HandleCutsceneFinished(CutsceneAsset asset, bool completed)
        {
            if (asset != _playing) return;

            CutsceneDirector.CutsceneFinished -= HandleCutsceneFinished;
            var onFinished = _onFinished;
            _onFinished = null;
            _playing = null;

            if (completed && _openUiAfterCutscene)
            {
                onFinished?.Invoke();
            }
        }

        // 대화(컷씬)가 시작되면 말풍선과 겹치지 않게 마커를 숨긴다
        private void HandleAnyCutsceneStarted(CutsceneAsset asset)
        {
            if (_questMarker != null) _questMarker.SetActive(false);
        }

        // 어떤 컷씬이 끝나도 완료 플래그가 바뀌었을 수 있으니 마커를 다시 계산한다 (Director가 완료 플래그를 저장한 뒤 호출됨)
        private void HandleAnyCutsceneFinished(CutsceneAsset asset, bool completed)
        {
            RefreshMarker();
        }

        private void OnDisable()
        {
            CutsceneDirector.CutsceneFinished -= HandleCutsceneFinished;
            CutsceneDirector.CutsceneStarted -= HandleAnyCutsceneStarted;
            CutsceneDirector.CutsceneFinished -= HandleAnyCutsceneFinished;
            _onFinished = null;
            _playing = null;
        }
    }
}
