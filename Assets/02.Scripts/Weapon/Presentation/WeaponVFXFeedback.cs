using UnityEngine;

namespace ShotgunAndRun.Weapon
{
    public class WeaponVFXFeedback : MonoBehaviour
    {
        [Header("Muzzle Flash")]
        [SerializeField] private Transform _muzzle;
        [SerializeField] private MuzzleFlash _muzzleFlashPrefab;

        [Tooltip("섬광을 무기 그림과 같은 방향으로 뒤집기 위해 참조한다. 비워 두면 같은 오브젝트에서 찾는다.")]
        [SerializeField] private SpriteRenderer _weaponSprite;

        private MuzzleFlash _muzzleFlash;

        private void Awake()
        {
            if (_weaponSprite == null)
            {
                _weaponSprite = GetComponent<SpriteRenderer>();
            }
        }

        public void PlayMuzzleFlash(bool isCritical)
        {
            if (_muzzle == null || _muzzleFlashPrefab == null)
            {
                return;
            }

            // 총구 아래에 하나만 만들어 두고 발사할 때마다 다시 재생한다.
            if (_muzzleFlash == null)
            {
                _muzzleFlash = Instantiate(_muzzleFlashPrefab, _muzzle);
                _muzzleFlash.transform.localPosition = Vector3.zero;
                _muzzleFlash.transform.localRotation = Quaternion.identity;
            }

            bool flipY = _weaponSprite != null && _weaponSprite.flipY;
            _muzzleFlash.Play(isCritical, flipY);
        }
    }
}
