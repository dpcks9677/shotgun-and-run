using System.Collections;
using ShotgunAndRun.Combat;
using UnityEngine;

namespace ShotgunAndRun.Player
{
    public class PlayerVFXFeedback : MonoBehaviour
    {
        [Header("Particle")]
        // [SerializeField] private ParticleSystem _damageEffect;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private GameObject _blood;
        [SerializeField] private DashPool _dsahPooolPrefab;
        [SerializeField] private DashPool _dashPool;

        // [SerializeField] private ParticleSystem _deathEffect;

        [SerializeField] private float _ghostSpawnInterval = 0.02f; // 잔상 소환 간격

        private void Awake()
        {
            _dashPool = FindFirstObjectByType<DashPool>();

            if (_dashPool == null && _dsahPooolPrefab != null)
            {
                _dashPool = Instantiate(_dsahPooolPrefab);
            }

            _spriteRenderer = GetComponentInChildren<PlayerBody>(true).SpriteRenderer;

            if (_spriteRenderer == null)
            {
                Debug.LogError($"_spriteRenderer가 초기화되지 않았습니다.");
            }

            if (_dashPool == null)
            {
                Debug.LogError($"_dashPool이 초기화되지 않았습니다.");
            }
        }

        public void PlayDamage(DamageInfo damageInfo)
        {
            GameObject bloodObject = Instantiate(
                _blood,
                transform.position,
                Quaternion.identity);

            Blood blood = bloodObject.GetComponent<Blood>();

            if (blood != null)
            {
                blood.Initialize(damageInfo.HitDirection);
            }
        }

        public void PlayDash(Vector2 direction, bool weaponUsable)
        {
            if (_dashPool == null || _spriteRenderer == null)
            {
                return;
            }

            DashDustVFX dash = _dashPool.AcquireDash();
            if (dash != null)
            {
                dash.transform.position = transform.position;
                dash.gameObject.SetActive(true);
                dash.Play(direction);
            }

            if (Player.PlayerInstance == null || Player.PlayerInstance.RuntimeStats == null)
            {
                Debug.LogError("플레이어 런타임 스탯이 준비되기 전에 대시 잔상을 생성하려고 했습니다.", this);
                return;
            }

            StartCoroutine(SpawnGhost(
                Player.PlayerInstance.RuntimeStats.DashDuration,
                weaponUsable));
        }

        private IEnumerator SpawnGhost(float duration, bool weaponUsable)
        {
            float elapsed = 0f;
            WaitForSeconds wait = new WaitForSeconds(_ghostSpawnInterval);

            while (elapsed < duration)
            {
                Ghost ghost = _dashPool.AcquireGhost();
                if (ghost == null)
                {
                    Debug.LogError("_dashPool에서 ghost 프리팹을 가져오지 못했습니다.");
                    yield break;
                }

                ghost.transform.position = transform.position;

                ghost.Initialize(duration, weaponUsable);
                ghost.gameObject.SetActive(true);

                Transform ghostTransform = ghost.GetComponent<Transform>();
                SpriteRenderer playerRenderer = ghostTransform != null
                    ? ghostTransform.GetComponent<SpriteRenderer>()
                    : null;

                if (playerRenderer != null)
                {
                    playerRenderer.flipX = _spriteRenderer.flipX;
                }

                yield return wait;
                elapsed += _ghostSpawnInterval;
            }
        }

        public void PlayDeath()
        {
        }
    }
}
