using System.Collections.Generic;
using UnityEngine;

namespace ShotgunAndRun.Weapon
{
    public sealed class ProjectTilePool : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private HitscanPellet _pelletPrefab;
        [SerializeField] private BulletTracer _tracerPrefab;

        [Header("Root")]
        [SerializeField] private Transform _pelletRoot;

        [Header("Impact")]
        [SerializeField] private PelletImpact _wallImpactPrefab;
        [SerializeField] private PelletImpact _enemyImpactPrefab;

        [Tooltip("착탄 효과와 궤적을 둘 부모. 비워 두면 씬 최상위에 만든다. 플레이어를 따라 움직이거나 총과 함께 회전하면 안 되므로 무기 아래에 두지 않는다.")]
        [SerializeField] private Transform _impactRoot;

        [Header("Capacity")]
        [SerializeField, Min(0)] private int _prewarmPellets = 16;
        [SerializeField, Min(0)] private int _prewarmTracers = 16;
        [SerializeField, Min(1)] private int _maxPellets = 64;
        [SerializeField, Min(1)] private int _maxTracers = 64;
        [SerializeField, Min(0)] private int _prewarmImpactsPerKind = 8;
        [SerializeField, Min(1)] private int _maxImpactsPerKind = 32;

        private readonly List<HitscanPellet> _pellets = new();
        private readonly List<BulletTracer> _tracers = new();
        private readonly List<PelletImpact> _wallImpacts = new();
        private readonly List<PelletImpact> _enemyImpacts = new();
        private bool _ownsImpactRoot;

        private void Awake()
        {
            _pelletRoot ??= transform;

            // _prewarm 사이즈만큼 prefab을 Instantiate 함
            // 이후 부족하면 _max 사이즈까지는 새로 생성
            PrewarmPellets();
            PrewarmTracers();
            PrewarmImpacts(_wallImpactPrefab, _wallImpacts);
            PrewarmImpacts(_enemyImpactPrefab, _enemyImpacts);
        }

        private void OnDestroy()
        {
            // 직접 만든 씬 최상위 루트는 풀과 함께 정리한다.
            if (_ownsImpactRoot && _impactRoot != null)
            {
                Destroy(_impactRoot.gameObject);
            }
        }

        public bool FireHitscan(
            Vector2 origin,
            Vector2 direction,
            Transform shooter,
            ShotContext context,
            out bool hitEnemy)
        {
            hitEnemy = false;
            HitscanPellet pellet = AcquirePellet();

            if (pellet == null)
            {
                Debug.LogWarning("사용 가능한 HitscanPellet이 없습니다.", this);
                return false;
            }

            pellet.gameObject.SetActive(true);
            pellet.Fire(this, origin, direction, shooter, context, out hitEnemy);

            return true;
        }

        internal void SpawnTracer(Vector2 start, Vector2 end, PelletImpactKind impactKind, bool isCritical)
        {
            BulletTracer tracer = AcquireTracer();

            if (tracer == null)
            {
                Debug.LogWarning("사용 가능한 BulletTracer가 없습니다.", this);
                return;
            }

            tracer.Play(start, end, impactKind, isCritical);
            tracer.gameObject.SetActive(true);
        }

        // 착탄 효과는 연출일 뿐이라 풀이 가득 차면 경고 없이 건너뛴다.
        internal void SpawnImpact(PelletImpactKind kind, Vector2 position)
        {
            PelletImpact impact = kind switch
            {
                PelletImpactKind.Wall => AcquireImpact(_wallImpactPrefab, _wallImpacts),
                PelletImpactKind.Enemy => AcquireImpact(_enemyImpactPrefab, _enemyImpacts),
                _ => null
            };

            if (impact == null)
            {
                return;
            }

            impact.Play(position);
            impact.gameObject.SetActive(true);
        }

        internal void Release(HitscanPellet pellet)
        {
            if (pellet == null || !pellet.gameObject.activeSelf)
            {
                return;
            }

            pellet.ResetState();
            pellet.gameObject.SetActive(false);
        }

        internal void Release(BulletTracer tracer)
        {
            if (tracer == null || !tracer.gameObject.activeSelf)
            {
                return;
            }

            tracer.ResetState();
            tracer.gameObject.SetActive(false);
        }

        internal void Release(PelletImpact impact)
        {
            if (impact == null || !impact.gameObject.activeSelf)
            {
                return;
            }

            impact.ResetState();
            impact.gameObject.SetActive(false);
        }

        private HitscanPellet AcquirePellet()
        {
            for (int i = 0; i < _pellets.Count; i++)
            {
                if (!_pellets[i].gameObject.activeSelf)
                {
                    return _pellets[i];
                }
            }

            // _풀의 크기가 max 사이즈를 넘어가면 null을 반환
            // 아니면 새로 생성
            if (_pellets.Count >= _maxPellets)
            {
                return null;
            }

            return CreatePellet();
        }

        private HitscanPellet CreatePellet()
        {
            HitscanPellet pellet = Instantiate(_pelletPrefab, _pelletRoot);
            pellet.gameObject.SetActive(false);
            _pellets.Add(pellet);

            return pellet;
        }

        private BulletTracer AcquireTracer()
        {
            for (int i = 0; i < _tracers.Count; i++)
            {
                if (!_tracers[i].gameObject.activeSelf)
                {
                    return _tracers[i];
                }
            }

            if (_tracers.Count >= _maxTracers)
            {
                return null;
            }

            return CreateTracer();
        }

        private BulletTracer CreateTracer()
        {
            BulletTracer tracer = Instantiate(_tracerPrefab, EffectRoot());
            tracer.Bind(this);
            tracer.gameObject.SetActive(false);
            _tracers.Add(tracer);

            return tracer;
        }

        private PelletImpact AcquireImpact(PelletImpact prefab, List<PelletImpact> impacts)
        {
            if (prefab == null)
            {
                return null;
            }

            for (int i = 0; i < impacts.Count; i++)
            {
                if (!impacts[i].gameObject.activeSelf)
                {
                    return impacts[i];
                }
            }

            if (impacts.Count >= _maxImpactsPerKind)
            {
                return null;
            }

            return CreateImpact(prefab, impacts);
        }

        // 궤적 · 착탄 효과는 월드 좌표에 그대로 남아야 하므로 씬 최상위 루트에 둔다.
        private Transform EffectRoot()
        {
            if (_impactRoot == null)
            {
                _impactRoot = new GameObject($"{name}_Effects").transform;
                _ownsImpactRoot = true;
            }

            return _impactRoot;
        }

        private PelletImpact CreateImpact(PelletImpact prefab, List<PelletImpact> impacts)
        {
            PelletImpact impact = Instantiate(prefab, EffectRoot());
            impact.Bind(this);
            impact.gameObject.SetActive(false);
            impacts.Add(impact);

            return impact;
        }

        // Acquire*는 방금 만든 비활성 객체를 다시 돌려주므로 prewarm은 직접 생성합니다.
        private void PrewarmPellets()
        {
            while (_pellets.Count < _prewarmPellets && _pellets.Count < _maxPellets)
            {
                CreatePellet();
            }
        }

        private void PrewarmTracers()
        {
            while (_tracers.Count < _prewarmTracers && _tracers.Count < _maxTracers)
            {
                CreateTracer();
            }
        }

        private void PrewarmImpacts(PelletImpact prefab, List<PelletImpact> impacts)
        {
            if (prefab == null)
            {
                return;
            }

            while (impacts.Count < _prewarmImpactsPerKind && impacts.Count < _maxImpactsPerKind)
            {
                CreateImpact(prefab, impacts);
            }
        }
    }
}
