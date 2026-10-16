using UnityEngine;
using PlayerComponent = ShotgunAndRun.Player.Player;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 씬에서 컷씬을 시작시키는 계기입니다. 플레이어가 구역에 들어오면 재생하거나, 이벤트에서 Play()를 호출합니다.
    /// 재생 조건(플래그)은 Director가 검사하므로, 이미 본 컷씬은 다시 재생되지 않습니다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class CutsceneTrigger : MonoBehaviour
    {
        [Tooltip("재생할 컷씬 (씬의 CutsceneDirector에 등록되어 있어야 합니다)")]
        [SerializeField] private CutsceneAsset _cutscene;

        public CutsceneAsset Cutscene => _cutscene;

        [Tooltip("플레이어가 구역(트리거 콜라이더)에 들어오면 재생합니다. 끄면 Play() 호출로만 재생합니다.")]
        [SerializeField] private bool _playOnEnter = true;

        [Tooltip("한 번 재생을 시작하면 이 트리거를 다시 쓰지 않습니다.")]
        [SerializeField] private bool _onlyOnce = true;

        private bool _used;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_playOnEnter || !enabled) return;
            if (other.GetComponentInParent<PlayerComponent>() == null) return;

            Play();
        }

        public void Play()
        {
            if (_onlyOnce && _used) return;

            if (_cutscene == null)
            {
                Debug.LogError($"[CutsceneTrigger] '{name}'에 컷씬이 비어 있습니다.", this);
                return;
            }

            var director = CutsceneDirector.Current;
            if (director == null)
            {
                Debug.LogError($"[CutsceneTrigger] 씬에 CutsceneDirector가 없어 '{_cutscene.Id}'를 재생할 수 없습니다.", this);
                return;
            }

            if (!director.CanPlay(_cutscene)) return;

            if (director.Play(_cutscene))
            {
                _used = true;
            }
        }
    }
}
