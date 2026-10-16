using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 컷씬 하나의 데이터와 연출 코드입니다. 컷씬마다 이 클래스를 상속한 클래스 하나와 그 에셋 하나를 만듭니다.
    /// 씬에 묶이지 않으므로 어느 씬에서든 재생할 수 있고, 씬과는 인물 프로필 · 마커 키 · 신호 키로만 연결됩니다.
    /// 에셋은 모든 재생이 함께 쓰고 에디터에서는 Play 모드가 끝나도 값이 남으므로, 재생 중 상태를 필드에 저장하지 마세요.
    /// </summary>
    public abstract class CutsceneAsset : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("컷씬 고유 ID. 기획 문서 · 에셋 이름과 같게 씁니다. 예: CS_PROLOGUE_SHELTER")]
        [SerializeField] private string _id;

        [Header("재생 조건 (영구 플래그)")]
        [Tooltip("이 플래그를 모두 가지고 있어야 재생합니다.")]
        [SerializeField] private List<string> _requiredFlags = new List<string>();

        [Tooltip("이 플래그 중 하나라도 가지고 있으면 재생하지 않습니다. 보통 자기 완료 플래그를 넣습니다.")]
        [SerializeField] private List<string> _blockedFlags = new List<string>();

        [Tooltip("끝까지 재생했을 때 추가하고 저장할 플래그")]
        [SerializeField] private List<string> _completionFlags = new List<string>();

        [Tooltip("런 중이어도 완료 플래그를 보류하지 않고 바로 저장합니다. 처음 한 번만 보여 주는 _SEEN 플래그용")]
        [SerializeField] private bool _saveCompletionFlagsImmediately;

        [Header("재생 방식")]
        [Tooltip("씬 시작 시 조건이 맞으면 자동으로 재생합니다.")]
        [SerializeField] private bool _autoPlay;

        [Tooltip("자동 재생 후보가 여럿이면 값이 큰 컷씬 하나만 재생합니다.")]
        [SerializeField] private int _priority;

        [Header("재생 중 처리")]
        [Tooltip("플레이어 조작을 막습니다.")]
        [SerializeField] private bool _lockPlayer = true;

        [Tooltip("HideDuringCutscene이 붙은 HUD를 숨깁니다.")]
        [SerializeField] private bool _hideHud = true;

        [Tooltip("런 타이머(경과 시간 · 습격)를 멈추고 플레이어를 무적으로 만듭니다.")]
        [SerializeField] private bool _pauseGameplay = true;

        [Header("디버그")]
        [Tooltip("디버그 재생 시 플레이어를 미리 이동시킬 마커 또는 구역 키 (비워 두면 기본값 사용)")]
        [SerializeField] private string _debugSpawnMarker;

        public string Id => _id;
        public IReadOnlyList<string> RequiredFlags => _requiredFlags;
        public IReadOnlyList<string> BlockedFlags => _blockedFlags;
        public IReadOnlyList<string> CompletionFlags => _completionFlags;
        public bool SaveCompletionFlagsImmediately => _saveCompletionFlagsImmediately;
        public bool AutoPlay => _autoPlay;
        public int Priority => _priority;
        public bool LockPlayer => _lockPlayer;
        public bool HideHud => _hideHud;
        public bool PauseGameplay => _pauseGameplay;

        /// <summary>
        /// 디버그 실행 시 플레이어를 사전 이동시킬 마커나 구역 키입니다.
        /// </summary>
        public string DebugSpawnMarker => !string.IsNullOrEmpty(_debugSpawnMarker) ? _debugSpawnMarker : DefaultDebugSpawnMarker;

        /// <summary>
        /// 하위 컷씬에서 고유의 디버그 스폰 위치 기본값을 지정합니다.
        /// </summary>
        protected virtual string DefaultDebugSpawnMarker => null;

        /// <summary>
        /// 디버그 텔레포트 위치에 더하는 보정값. 마커가 가구 · 기둥 위에 있어 플레이어가 가려질 때 비켜 세웁니다.
        /// </summary>
        public Vector2 DebugSpawnOffset => DefaultDebugSpawnOffset;

        protected virtual Vector2 DefaultDebugSpawnOffset => Vector2.zero;

        /// <summary>
        /// 자동 재생으로 고르면 첫 화면이 그려지기 전에 플레이어를 숨깁니다. 플레이어가 나타나는 연출(순간이동 도착 등)용입니다.
        /// 숨긴 플레이어를 다시 보이게 하는 것은 컷씬이 맡습니다.
        /// </summary>
        public virtual bool HidesPlayerBeforeStart => false;

        /// <summary>
        /// 이 컷씬이 필요로 하는 인물 · 마커 · 신호 · 에셋 목록을 모읍니다.
        /// </summary>
        public CutsceneRequirements CollectRequirements()
        {
            var requirements = new CutsceneRequirements();
            if (string.IsNullOrWhiteSpace(_id))
            {
                requirements.AddError("컷씬 ID가 비어 있습니다.");
            }

            DeclareRequirements(requirements);
            return requirements;
        }

        internal IEnumerator CreateSequence(CutsceneContext context)
        {
            return Play(context);
        }

        /// <summary>
        /// 필요한 인물 · 마커 · 신호 · 대사 · 에셋을 선언합니다. 선언하지 않은 것은 연출 코드에서 쓸 수 없습니다.
        /// </summary>
        protected abstract void DeclareRequirements(CutsceneRequirements requirements);

        /// <summary>
        /// 연출 순서를 씁니다. CutsceneContext의 명령만 사용하세요.
        /// </summary>
        protected abstract IEnumerator Play(CutsceneContext c);
    }
}
