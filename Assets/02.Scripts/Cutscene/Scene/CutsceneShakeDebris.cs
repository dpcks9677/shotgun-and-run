using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 컷씬에서 카메라가 흔들릴 때마다 씬에 미리 찍어 둔 지점에 먼지와 잔해를 떨어뜨립니다.
    /// 흔들림 세기(power)가 클수록 많이 떨어지고, 세기가 Heavy Power 이상이면 입구 지점(Focus Root)에 먼지 뭉치가 먼저 터집니다.
    /// 위에서 보는 시점이라 낙하는 "접지 그림자가 커지다가 돌이 회전 없이 똑바로 떨어져 튀는" 방식으로 그립니다(픽셀 그래픽이라 회전 · 반전 없음). 카메라에 보이는 지점만 고릅니다.
    /// 지점은 Points Root · Focus Root의 자식 Transform이며, 옮기거나 늘리면 그대로 반영됩니다.
    /// </summary>
    public sealed class CutsceneShakeDebris : MonoBehaviour
    {
        [Tooltip("이 컷씬이 재생 중일 때의 흔들림에만 반응합니다. 비우면 어떤 흔들림에도 반응하지 않습니다.")]
        [SerializeField] private CutsceneAsset _cutscene;

        [Header("스프라이트 (TutorialCollapseDebris 시트)")]
        [SerializeField] private Sprite[] _rocks;
        [SerializeField] private Sprite _shadow;
        [Tooltip("번지며 옅어지는 먼지 프레임 (순서대로 재생)")]
        [SerializeField] private Sprite[] _dustFrames;

        [Header("큰 낙하 잔해 (출발 구간 바로 뒤쪽)")]
        [Tooltip("큰 돌덩이 · 기둥 토막 (CollapseBig_1~3)")]
        [SerializeField] private Sprite[] _bigRocks;
        [Tooltip("출발 지점 뒤쪽 지점들. 이 지점이 카메라에 보일 때 흔들림마다 큰 잔해가 연달아 떨어집니다.")]
        [SerializeField] private Transform _behindRoot;
        [SerializeField, Min(0)] private int _behindCount = 6;
        [Tooltip("흔들림 시작 후 이 시간 안에 큰 잔해가 차례로 떨어집니다.")]
        [SerializeField, Min(0.1f)] private float _behindSeconds = 1.6f;
        [SerializeField] private Vector2 _bigDropHeight = new Vector2(7f, 11f);

        [Header("추격 낙하: 컷씬 시작(첫 흔들림)과 함께 복도 뒤편부터 큰 잔해 더미를 한 번에 하나씩 정해진 자리 · 정해진 시각에 떨어뜨림 (충돌 없음)")]
        [Tooltip("큰 더미 스프라이트(CollapseDebris_A · B). 하나씩 번갈아 떨어집니다.")]
        [SerializeField] private Sprite[] _chaseHeaps;
        [Tooltip("착지 지점들. 자식 Transform이 위에서부터(복도 뒤편 → 입구 쪽) 순서대로 한 개씩 떨어집니다. 옮기거나 늘리면 그대로 반영됩니다.")]
        [SerializeField] private Transform _chaseRoot;
        [Tooltip("컷씬의 첫 흔들림(= 씬 시작)에 추격 낙하를 시작합니다.")]
        [SerializeField] private bool _chaseOnFirstShake = true;
        [Tooltip("더미가 땅에 닿는 순간 플레이어 뒤로 이만큼(칸) 떨어진 자리가 되게 떨어뜨립니다. 작을수록 아슬아슬하고, 0 이하면 플레이어를 덮칩니다.")]
        [SerializeField, Range(0.5f, 6f)] private float _chaseLandGap = 2f;
        [Tooltip("플레이어가 지나치지 않아도(멈춰 있을 때 등) 이 시간(초)이 지나면 떨어뜨립니다.")]
        [SerializeField, Min(0.5f)] private float _chasePassTimeout = 6f;
        [Tooltip("첫 더미가 떨어지기 시작하기까지 시간(초)")]
        [SerializeField, Min(0f)] private float _chaseFirstDelay = 0f;
        [Tooltip("더미 사이 시간(초)")]
        [SerializeField, Min(0.05f)] private float _chaseInterval = 0.4f;
        [Tooltip("더미가 떨어지기 시작하는 높이(착지 지점 위 칸 수)")]
        [SerializeField, Min(1f)] private float _heapDropHeight = 11f;
        [SerializeField] private string _heapLayer = "Environment";
        [SerializeField] private int _heapOrder = 0;

        [Header("복도를 막는 큰 잔해 더미 (진동과 함께 떨어져 길을 막음)")]
        [Tooltip("출발 지점 바로 뒤. 시작할 때 꺼 둡니다. 첫 흔들림 때 카메라에 보이면 떨어집니다.")]
        [SerializeField] private GameObject _startPlug;
        [Tooltip("복도 입구. 시작할 때 꺼 둡니다. 컷씬 신호(CollapseDebris)가 DropEntrancePlug를 부르면 떨어집니다.")]
        [SerializeField] private GameObject _entrancePlug;
        [SerializeField, Min(0f)] private float _plugDelay = 0.35f;
        [SerializeField, Min(1f)] private float _plugDropHeight = 9f;
        [SerializeField, Min(1f)] private float _plugGravity = 60f;

        [Header("소리 (비워 두면 소리 없이 진행)")]
        [Tooltip("작은 돌이 바닥에 떨어질 때 (여러 개면 무작위)")]
        [SerializeField] private AudioClip[] _rockLandSfx;
        [Tooltip("큰 돌덩이 · 기둥 토막이 떨어질 때. 비우면 작은 돌 소리를 크게 씁니다.")]
        [SerializeField] private AudioClip _bigRockLandSfx;
        [Tooltip("복도를 따라 쌓이는 큰 잔해 더미가 떨어질 때")]
        [SerializeField] private AudioClip _heapLandSfx;
        [Tooltip("복도를 막는 더미가 떨어질 때")]
        [SerializeField] private AudioClip _plugLandSfx;
        [Tooltip("흔들림이 시작될 때 깔리는 낮은 울림 · 부스러기 소리 (세기가 클수록 크게)")]
        [SerializeField] private AudioClip _rumbleSfx;
        [Tooltip("큰 흔들림(세기가 Heavy Power 이상)에서 울림에 더해 깔리는 무너지는 소리. 폭발음이 아니라 돌과 기둥이 와르르 쏟아지는 소리를 넣습니다.")]
        [SerializeField] private AudioClip _heavyCollapseSfx;
        [SerializeField, Range(0f, 1f)] private float _sfxVolume = 1f;
        [Tooltip("돌 착지음 사이의 최소 간격(초). 한꺼번에 떨어져도 소리가 뭉개지지 않게 합니다.")]
        [SerializeField, Min(0f)] private float _rockSfxGap = 0.07f;

        [Header("지점 (자식 Transform)")]
        [Tooltip("복도 · 방 곳곳. 카메라에 보이는 지점만 쓰입니다.")]
        [SerializeField] private Transform _pointsRoot;
        [Tooltip("복도 입구 주변. 큰 흔들림에서 먼지 · 잔해가 더 몰립니다.")]
        [SerializeField] private Transform _focusRoot;
        [SerializeField, Min(0f)] private float _jitter = 0.7f;

        [Header("세기 (흔들림 세기 power 기준)")]
        [Tooltip("이 세기 이상이면 큰 흔들림: 입구 먼지 뭉치 + 입구 쪽 낙하 집중")]
        [SerializeField, Min(0f)] private float _heavyPower = 0.25f;
        [Tooltip("초당 잔해 수 = 기본 + 배율 × power²")]
        [SerializeField, Min(0f)] private float _rubbleBase = 1f;
        [SerializeField, Min(0f)] private float _rubbleScale = 50f;
        [Tooltip("초당 먼지 수 = 기본 + 배율 × power")]
        [SerializeField, Min(0f)] private float _dustBase = 1.5f;
        [SerializeField, Min(0f)] private float _dustScale = 12f;
        [Tooltip("큰 흔들림 시작 때 입구에서 터지는 먼지 뭉치 수")]
        [SerializeField, Min(0)] private int _heavyBurst = 5;
        [Tooltip("큰 흔들림에서 입구 지점을 고를 확률")]
        [SerializeField, Range(0f, 1f)] private float _focusChance = 0.5f;

        [Header("낙하")]
        [SerializeField] private Vector2 _dropHeight = new Vector2(4f, 8f);
        [SerializeField, Min(1f)] private float _gravity = 40f;
        [Tooltip("바닥에 놓인 잔해 최대 수. 넘으면 오래된 것부터 사라집니다.")]
        [SerializeField, Min(1)] private int _maxResting = 40;

        [Header("그리기 순서")]
        [SerializeField] private string _restLayer = "GroundProps";
        [SerializeField] private int _restOrder = 5;
        [SerializeField] private string _fallLayer = "Foreground";
        [SerializeField] private int _fallOrder = 0;
        [SerializeField] private string _dustLayer = "Foreground";
        [SerializeField] private int _dustOrder = 1;

        private const float PixelsPerUnit = 16f;
        private readonly List<Transform> _points = new List<Transform>();
        private readonly List<Transform> _focus = new List<Transform>();
        private readonly List<Transform> _behind = new List<Transform>();
        private readonly List<(GameObject rock, GameObject shadow)> _resting = new List<(GameObject, GameObject)>();
        private Camera _camera;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private bool _startPlugDropped;
        private bool _chaseStarted;
        private float _lastRockSfx;
        private float _playerSpeed;
        private Vector2 _lastPlayerPos;
        private bool _hasLastPlayerPos;
        private Vector3 _startPlugHome;
        private Vector3 _entrancePlugHome;

        private void Awake()
        {
            Gather(_pointsRoot, _points);
            Gather(_focusRoot, _focus);
            Gather(_behindRoot, _behind);

            if (_startPlug != null) _startPlugHome = _startPlug.transform.position;
            if (_entrancePlug != null) _entrancePlugHome = _entrancePlug.transform.position;
        }

        private void OnEnable()
        {
            CutsceneCamera.Shaken += OnShaken;
        }

        private void OnDisable()
        {
            CutsceneCamera.Shaken -= OnShaken;
        }

        private void OnShaken(float power, float duration)
        {
            if (power <= 0f || duration <= 0f)
            {
                return;
            }

            if (_cutscene == null || CutsceneDirector.Current == null || CutsceneDirector.Current.Playing != _cutscene)
            {
                return;
            }

            // 세기에 따라 울림의 크기를 정한다 (작은 흔들림은 은은하게, 큰 붕괴는 크게)
            PlaySfx(_rumbleSfx, Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(power / 0.4f)));
            if (power >= _heavyPower)
            {
                PlaySfx(_heavyCollapseSfx);
            }

            StartCoroutine(Run(power, duration));
            StartCoroutine(RunBehind());

            if (_chaseOnFirstShake && !_chaseStarted && _chaseRoot != null && _chaseHeaps != null && _chaseHeaps.Length > 0)
            {
                _chaseStarted = true;
                StartCoroutine(Chase());
            }

            if (_startPlug != null && !_startPlugDropped && IsVisible(_startPlug.transform.position))
            {
                _startPlugDropped = true;
                StartCoroutine(DropPlug(_startPlug, _plugDelay));
            }
        }

        private IEnumerator Chase()
        {
            var spots = new List<Transform>();
            Gather(_chaseRoot, spots);
            if (_chaseFirstDelay > 0f)
            {
                yield return new WaitForSeconds(_chaseFirstDelay);
            }

            _playerSpeed = 0f;
            _hasLastPlayerPos = false;
            TrackPlayerSpeed();

            // 지점은 복도 뒤편 → 입구 순서다. 입구 쪽 끝 지점을 "출구"로 보고, 플레이어가 각 지점을 지나친 뒤에 그 자리에 떨어뜨린다
            Transform exit = spots.Count > 0 ? spots[spots.Count - 1] : null;
            for (int i = 0; i < spots.Count; i++)
            {
                if (spots[i] == null)
                {
                    continue;
                }

                float giveUpAt = Time.time + _chasePassTimeout;
                while (!ShouldDropHeap(spots[i], exit) && Time.time < giveUpAt)
                {
                    TrackPlayerSpeed();
                    yield return null;
                }

                StartCoroutine(FallHeap(spots[i].position, _chaseHeaps[i % _chaseHeaps.Length], i % 2 == 1));
                float nextAt = Time.time + _chaseInterval;
                while (Time.time < nextAt)
                {
                    TrackPlayerSpeed();
                    yield return null;
                }
            }
        }

        // 더미가 땅에 닿을 때(낙하 시간 뒤) 플레이어가 그 자리에서 _chaseLandGap만큼 앞서 달리고 있을 시점이면 떨어뜨린다.
        // 플레이어의 지금 속도로 낙하 시간 동안 갈 거리를 미리 계산하므로 속도가 달라져도 항상 발뒤꿈치 가까이에 떨어진다
        private bool ShouldDropHeap(Transform spot, Transform exit)
        {
            var player = ShotgunAndRun.Player.Player.PlayerInstance;
            if (player == null || exit == null)
            {
                return true;
            }

            Vector2 exitPoint = exit.position;
            float spotToExit = Vector2.Distance(spot.position, exitPoint);
            float playerToExit = Vector2.Distance(player.transform.position, exitPoint);
            float fallTime = Mathf.Sqrt(2f * _heapDropHeight / _plugGravity);
            float playerAtLanding = playerToExit - _playerSpeed * fallTime;
            return playerAtLanding <= spotToExit - _chaseLandGap;
        }

        // 프레임마다 플레이어가 얼마나 빨리 달리는지 잰다 (조금씩 섞어 흔들림을 줄인다)
        private void TrackPlayerSpeed()
        {
            var player = ShotgunAndRun.Player.Player.PlayerInstance;
            if (player == null)
            {
                return;
            }

            Vector2 now = player.transform.position;
            if (_hasLastPlayerPos && Time.deltaTime > 0f)
            {
                float measured = Vector2.Distance(now, _lastPlayerPos) / Time.deltaTime;
                _playerSpeed = Mathf.Lerp(_playerSpeed, measured, 0.3f);
            }

            _lastPlayerPos = now;
            _hasLastPlayerPos = true;
        }

        private void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (clip == null)
            {
                return;
            }

            var director = CutsceneDirector.Current;
            var source = director != null ? director.SfxSource : null;
            if (source != null)
            {
                source.PlayOneShot(clip, volume * _sfxVolume);
            }
            else
            {
                AudioSource.PlayClipAtPoint(clip, Camera.main != null ? Camera.main.transform.position : transform.position, volume * _sfxVolume);
            }
        }

        // 돌 착지음은 너무 촘촘하면 뭉개지므로 간격을 둔다
        private void PlayRockLand(float volume)
        {
            if (_rockLandSfx == null || _rockLandSfx.Length == 0 || Time.time - _lastRockSfx < _rockSfxGap)
            {
                return;
            }

            _lastRockSfx = Time.time;
            PlaySfx(_rockLandSfx[Random.Range(0, _rockLandSfx.Length)], volume);
        }

        private IEnumerator FallHeap(Vector3 target, Sprite sprite, bool flip)
        {
            var shadow = NewRenderer("HeapShadow", _shadow, _heapLayer, _heapOrder - 1, target + new Vector3(0f, -1.4f, 0f));
            // 낙하 중에도 캐릭터보다 아래 레이어(Environment)에 그려 플레이어를 가리지 않음
            var heap = NewRenderer("ChaseDebris", sprite, _heapLayer, _heapOrder + 5, target);
            heap.flipX = flip;
            float height = _heapDropHeight;
            float fallTime = Mathf.Sqrt(2f * height / _plugGravity);
            float t = 0f;
            while (t < fallTime)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / fallTime);
                heap.transform.position = target + new Vector3(0f, Mathf.Max(0f, height - 0.5f * _plugGravity * t * t), 0f);
                if (shadow != null)
                {
                    shadow.transform.localScale = new Vector3(6.4f, 3.2f, 1f) * Mathf.Lerp(0.4f, 1f, p);
                    shadow.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.1f, 1f, p * p));
                }

                yield return null;
            }

            // 착지: 바닥 소품 순서로 내리고 폭 전체로 먼지. 충돌은 없음(막는 건 입구 더미 하나)
            heap.transform.position = target;
            heap.sortingLayerName = _heapLayer;
            heap.sortingOrder = _heapOrder;
            heap.gameObject.name = "ChaseDebrisLanded";
            if (shadow != null)
            {
                Destroy(shadow.gameObject);
            }

            LandingDust(target);
            PlaySfx(_heapLandSfx);
        }

        private void LandingDust(Vector3 at)
        {
            for (int i = 0; i < 7; i++)
            {
                float x = Mathf.Lerp(-2.7f, 2.7f, i / 6f) + Random.Range(-0.3f, 0.3f);
                StartCoroutine(Puff(at + new Vector3(x, Random.Range(-1.3f, -0.6f), 0f), Random.Range(2.0f, 3.0f), Random.Range(0f, 0.2f), 1.8f));
            }
        }

        /// <summary>복도 입구 더미를 떨어뜨립니다. 컷씬 신호(CollapseDebris)의 이벤트에 연결합니다.</summary>
        public void DropEntrancePlug()
        {
            if (_entrancePlug != null)
            {
                StartCoroutine(DropPlug(_entrancePlug, 0.15f));
            }
        }

        private IEnumerator DropPlug(GameObject plug, float delay)
        {
            Transform tr = plug.transform;
            Vector3 at = tr.position;
            var col = plug.GetComponent<Collider2D>();
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (col != null)
            {
                col.enabled = false;
            }

            tr.position = at + new Vector3(0f, _plugDropHeight, 0f);
            plug.SetActive(true);
            var shadow = NewRenderer("PlugShadow", _shadow, _restLayer, _restOrder - 1, at + new Vector3(0f, -0.1f, 0f));
            float fallTime = Mathf.Sqrt(2f * _plugDropHeight / _plugGravity);
            float t = 0f;
            while (t < fallTime)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / fallTime);
                tr.position = at + new Vector3(0f, Mathf.Max(0f, _plugDropHeight - 0.5f * _plugGravity * t * t), 0f);
                if (shadow != null)
                {
                    shadow.transform.localScale = new Vector3(6.4f, 3.2f, 1f) * Mathf.Lerp(0.4f, 1f, p);
                    shadow.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.1f, 1f, p * p));
                }

                yield return null;
            }

            // 착지: 더미 앞쪽 폭 전체로 먼지가 터지고 길이 막힘
            tr.position = at;
            if (col != null)
            {
                col.enabled = true;
            }

            LandingDust(at);
            PlaySfx(_plugLandSfx);
            if (shadow != null)
            {
                Destroy(shadow.gameObject);
            }
        }

        /// <summary>
        /// (디버그용) 연출로 생긴 것을 모두 지우고 붕괴 전 상태로 되돌립니다. 진행 중인 낙하는 멈춥니다.
        /// </summary>
        public void ResetState()
        {
            StopAllCoroutines();

            foreach (var spawned in _spawned)
            {
                if (spawned != null)
                {
                    Destroy(spawned);
                }
            }

            _spawned.Clear();
            _resting.Clear();
            _startPlugDropped = false;
            _chaseStarted = false;

            HidePlug(_startPlug, _startPlugHome);
            HidePlug(_entrancePlug, _entrancePlugHome);
        }

        /// <summary>
        /// (디버그용) 붕괴가 모두 끝난 상태로 즉시 만듭니다. 입구를 막는 더미와 복도 뒤편 잔해 더미가 놓인 채로 시작합니다.
        /// collapsed가 false면 ResetState와 같습니다.
        /// </summary>
        public void SetCollapsed(bool collapsed)
        {
            ResetState();
            if (!collapsed)
            {
                return;
            }

            _startPlugDropped = true;
            _chaseStarted = true;
            ShowPlug(_startPlug, _startPlugHome);
            ShowPlug(_entrancePlug, _entrancePlugHome);

            if (_chaseRoot == null || _chaseHeaps == null || _chaseHeaps.Length == 0)
            {
                return;
            }

            var spots = new List<Transform>();
            Gather(_chaseRoot, spots);
            for (int i = 0; i < spots.Count; i++)
            {
                if (spots[i] == null)
                {
                    continue;
                }

                var heap = NewRenderer("ChaseDebrisLanded", _chaseHeaps[i % _chaseHeaps.Length], _heapLayer, _heapOrder, spots[i].position);
                if (heap != null)
                {
                    heap.flipX = i % 2 == 1;
                }
            }
        }

        private static void HidePlug(GameObject plug, Vector3 home)
        {
            if (plug == null)
            {
                return;
            }

            plug.transform.position = home;
            plug.SetActive(false);
        }

        private static void ShowPlug(GameObject plug, Vector3 home)
        {
            if (plug == null)
            {
                return;
            }

            plug.transform.position = home;
            plug.SetActive(true);
            var col = plug.GetComponent<Collider2D>();
            if (col != null)
            {
                col.enabled = true;
            }
        }

        private bool IsVisible(Vector3 world)
        {
            if (_camera == null)
            {
                _camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            }

            if (_camera == null)
            {
                return true;
            }

            Vector3 v = _camera.WorldToViewportPoint(world);
            return v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
        }

        private IEnumerator Run(float power, float duration)
        {
            bool heavy = power >= _heavyPower;
            float elapsed = 0f;
            float rubbleAcc = 0f;
            float dustAcc = 0f;

            if (heavy)
            {
                for (int i = 0; i < _heavyBurst; i++)
                {
                    if (TryPick(_focus, out Vector3 at))
                    {
                        StartCoroutine(Puff(at, Random.Range(2.2f, 3.2f), Random.Range(0f, 0.35f), 1.8f));
                    }
                }
            }

            while (elapsed < duration)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                float taper = Mathf.Lerp(1f, 0.25f, elapsed / duration);
                rubbleAcc += (_rubbleBase + _rubbleScale * power * power) * taper * dt;
                dustAcc += (_dustBase + _dustScale * power) * taper * dt;

                while (rubbleAcc >= 1f)
                {
                    rubbleAcc -= 1f;
                    if (TryPickTarget(heavy, out Vector3 at))
                    {
                        StartCoroutine(Fall(at, _rocks, _dropHeight, 1f));
                    }
                }

                while (dustAcc >= 1f)
                {
                    dustAcc -= 1f;
                    if (TryPickTarget(heavy, out Vector3 at))
                    {
                        StartCoroutine(Puff(at, Random.Range(1.0f, heavy ? 2.2f : 1.4f), 0f, heavy ? 1.3f : 1f));
                    }
                }

                yield return null;
            }
        }

        private IEnumerator RunBehind()
        {
            if (_bigRocks == null || _bigRocks.Length == 0)
            {
                yield break;
            }

            for (int i = 0; i < _behindCount; i++)
            {
                yield return new WaitForSeconds(Random.Range(0f, 2f * _behindSeconds / _behindCount));
                if (TryPick(_behind, out Vector3 at))
                {
                    StartCoroutine(Fall(at, _bigRocks, _bigDropHeight, 2f));
                }
            }
        }

        private bool TryPickTarget(bool heavy, out Vector3 at)
        {
            if (heavy && Random.value < _focusChance && TryPick(_focus, out at))
            {
                return true;
            }

            return TryPick(_points, out at);
        }

        private bool TryPick(List<Transform> list, out Vector3 at)
        {
            at = default;
            if (_camera == null)
            {
                _camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            }

            var visible = new List<Transform>();
            foreach (var p in list)
            {
                if (p == null)
                {
                    continue;
                }

                if (_camera == null)
                {
                    visible.Add(p);
                    continue;
                }

                Vector3 v = _camera.WorldToViewportPoint(p.position);
                if (v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f)
                {
                    visible.Add(p);
                }
            }

            if (visible.Count == 0)
            {
                return false;
            }

            Vector2 jitter = Random.insideUnitCircle * _jitter;
            at = visible[Random.Range(0, visible.Count)].position + new Vector3(jitter.x, jitter.y * 0.6f, 0f);
            return true;
        }

        private static void Gather(Transform root, List<Transform> into)
        {
            into.Clear();
            if (root == null)
            {
                return;
            }

            foreach (Transform child in root)
            {
                into.Add(child);
            }
        }

        private IEnumerator Fall(Vector3 target, Sprite[] pool, Vector2 drop, float puffScale)
        {
            if (pool == null || pool.Length == 0)
            {
                yield break;
            }

            Sprite sprite = pool[Random.Range(0, pool.Length)];
            float width = sprite.rect.width / PixelsPerUnit;

            var shadowRenderer = NewRenderer("RubbleShadow", _shadow, _restLayer, _restOrder - 1, target);
            var rockRenderer = NewRenderer("FallingRubble", sprite, _fallLayer, _fallOrder, target);
            float shadowScale = Mathf.Max(0.6f, width * 1.1f);

            float height = Random.Range(drop.x, drop.y);
            float fallTime = Mathf.Sqrt(2f * height / _gravity);
            float t = 0f;
            while (t < fallTime)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / fallTime);
                float y = height - 0.5f * _gravity * t * t;
                rockRenderer.transform.position = target + new Vector3(0f, Mathf.Max(0f, y), 0f);
                if (shadowRenderer != null)
                {
                    shadowRenderer.transform.localScale = Vector3.one * (shadowScale * Mathf.Lerp(0.35f, 1f, p));
                    shadowRenderer.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.15f, 1f, p * p));
                }

                yield return null;
            }

            // 착지: 먼지 한 덩이 + 작게 한 번 튀고 자리 잡음. 큰 덩이는 묵직한 소리
            if (puffScale > 1f && _bigRockLandSfx != null)
            {
                PlaySfx(_bigRockLandSfx, 0.8f);
            }
            else
            {
                PlayRockLand(puffScale > 1f ? 1f : 0.55f);
            }

            StartCoroutine(Puff(target, Mathf.Clamp(width * 0.7f, 0.6f, 1.2f) * puffScale, 0f, 0.8f * puffScale));
            float hop = Random.Range(0.18f, 0.32f);
            float hopTime = 0.16f;
            t = 0f;
            while (t < hopTime)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / hopTime);
                rockRenderer.transform.position = target + new Vector3(0f, 4f * hop * p * (1f - p), 0f);
                yield return null;
            }

            rockRenderer.transform.position = target;
            rockRenderer.sortingLayerName = _restLayer;
            rockRenderer.sortingOrder = _restOrder;
            rockRenderer.gameObject.name = "RestingRubble";
            Register(rockRenderer.gameObject, shadowRenderer != null ? shadowRenderer.gameObject : null);
        }

        private IEnumerator Puff(Vector3 at, float scale, float delay, float seconds)
        {
            if (_dustFrames == null || _dustFrames.Length == 0)
            {
                yield break;
            }

            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            var puff = NewRenderer("Dust", _dustFrames[0], _dustLayer, _dustOrder, at);
            puff.flipX = Random.value < 0.5f;
            Transform tr = puff.transform;
            tr.localScale = Vector3.one * scale;
            float life = 0.9f * seconds;
            float rise = Random.Range(0.4f, 0.9f) * scale;
            float t = 0f;
            while (t < life)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / life);
                puff.sprite = _dustFrames[Mathf.Min(_dustFrames.Length - 1, (int)(p * _dustFrames.Length))];
                tr.position = at + new Vector3(0f, rise * p, 0f);
                yield return null;
            }

            Destroy(puff.gameObject);
        }

        private SpriteRenderer NewRenderer(string objectName, Sprite sprite, string layer, int order, Vector3 at)
        {
            if (sprite == null)
            {
                return null;
            }

            var go = new GameObject(objectName);
            _spawned.RemoveAll(o => o == null);
            _spawned.Add(go);
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = layer;
            sr.sortingOrder = order;
            return sr;
        }

        private void Register(GameObject rock, GameObject shadow)
        {
            _resting.Add((rock, shadow));
            while (_resting.Count > _maxResting)
            {
                var oldest = _resting[0];
                _resting.RemoveAt(0);
                if (oldest.rock != null)
                {
                    Destroy(oldest.rock);
                }

                if (oldest.shadow != null)
                {
                    Destroy(oldest.shadow);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            DrawPoints(_pointsRoot, new Color(0.3f, 0.85f, 1f, 0.9f));
            DrawPoints(_focusRoot, new Color(1f, 0.6f, 0.1f, 0.9f));
            DrawPoints(_behindRoot, new Color(1f, 0.3f, 0.3f, 0.9f));
        }

        private void DrawPoints(Transform root, Color color)
        {
            if (root == null)
            {
                return;
            }

            Gizmos.color = color;
            foreach (Transform child in root)
            {
                Gizmos.DrawWireSphere(child.position, _jitter);
                Gizmos.DrawSphere(child.position, 0.12f);
            }
        }
    }
}
