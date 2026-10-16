using System.Collections.Generic;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 튜토리얼처럼 HUD를 단계별로 하나씩 열어 줘야 할 때 쓰는 UI 가림막입니다. HideDuringCutscene 대신 붙입니다.
    /// 키(예: Health, Dash, Ammo, QuickSlot)로 구분하며, 열려 있어도 HUD 숨김이 켜진 컷씬 동안에는 숨깁니다.
    /// SetActive 대신 CanvasGroup으로 가려서, 대상 UI의 이벤트 구독이 끊기지 않게 합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class HudGate : MonoBehaviour
    {
        private static readonly List<HudGate> Gates = new List<HudGate>();
        private static readonly Dictionary<string, bool> Overrides = new Dictionary<string, bool>();

        [Tooltip("HUD 키 (예: Health, Dash, Ammo, QuickSlot)")]
        [SerializeField] private string _key;

        [Tooltip("씬이 시작될 때 열려 있는지. 코드에서 키로 지정한 값이 있으면 그 값이 우선합니다.")]
        [SerializeField] private bool _visibleAtStart = true;

        [Tooltip("다시 나타날 때 페이드인 시간(초)")]
        [SerializeField] private float _fadeInDuration = 0.3f;

        private CanvasGroup _canvasGroup;
        private bool _visible;

        public string Key => _key;

        /// <summary>
        /// 키가 같은 HUD를 모두 열거나 닫습니다. 아직 켜지지 않은 HUD에도 나중에 적용됩니다.
        /// </summary>
        public static void Set(string key, bool visible)
        {
            Overrides[key] = visible;
            foreach (var gate in Gates)
            {
                if (gate._key == key)
                {
                    gate._visible = visible;
                }
            }
        }

        /// <summary>
        /// 지정해 둔 값을 모두 지워 각 HUD가 시작 값으로 돌아가게 합니다. (튜토리얼을 새로 시작할 때)
        /// </summary>
        public static void ResetAll()
        {
            Overrides.Clear();
            foreach (var gate in Gates)
            {
                gate._visible = gate._visibleAtStart;
            }
        }

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            _visible = Overrides.TryGetValue(_key ?? string.Empty, out bool value) ? value : _visibleAtStart;
            Gates.Add(this);
        }

        private void OnDisable()
        {
            Gates.Remove(this);
        }

        private void LateUpdate()
        {
            bool show = _visible && !CutsceneDirector.IsHudHidden;
            // 숨길 때는 바로, 나타날 때는 서서히
            float alpha = show ? Mathf.MoveTowards(_canvasGroup.alpha, 1f, Time.unscaledDeltaTime / _fadeInDuration) : 0f;
            if (!Mathf.Approximately(_canvasGroup.alpha, alpha))
            {
                _canvasGroup.alpha = alpha;
                _canvasGroup.interactable = show;
                _canvasGroup.blocksRaycasts = show;
            }
        }
    }
}
