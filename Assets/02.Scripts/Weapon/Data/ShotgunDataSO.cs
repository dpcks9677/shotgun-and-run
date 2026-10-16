using UnityEngine;
using UnityEngine.Serialization;

namespace ShotgunAndRun.Weapon
{
    [CreateAssetMenu(fileName = "ShotgunDataSO", menuName = "ShotgunAndRun/Weapon/Shotgun Data")]
    public class ShotgunDataSO : ScriptableObject
    {
        [FormerlySerializedAs("pelletDamage")]
        [SerializeField, Min(1f)]
        public float PelletDamage = 50f; // 총알 데미지

        [FormerlySerializedAs("pelletCount")]
        [SerializeField, Min(0)]
        public int PelletCount = 5; // 산탄 수

        [FormerlySerializedAs("spreadAngle")]
        [SerializeField, Min(0f)]
        public float SpreadAngle = 36f; // 확산각 (전체, 도)

        [FormerlySerializedAs("fireInterval")]
        [SerializeField, Min(0f)]
        public float FireInterval = 0.8f; // 사격 속도

        [FormerlySerializedAs("magazineSize")]
        [SerializeField, Min(0)]
        public int MagazineSize = 6; // 탄창수

        [FormerlySerializedAs("reloadDuration")]
        [SerializeField, Min(0f)]
        public float ReloadDuration = 1.5f; // 장전 속도

        [FormerlySerializedAs("recoilDistance")]
        [SerializeField, Min(0f)]
        public float RecoilDistance = 0.4f; // 반동 거리

        [FormerlySerializedAs("projectileSpeed")]
        [SerializeField, Min(0f)]
        public float ProjectileSpeed = 18f; // 총알 속도

        [FormerlySerializedAs("maxRange")]
        [SerializeField, Min(0f)]
        public float MaxRange = 12f; // 총알 최대 사거리

        [FormerlySerializedAs("criticalChance")]
        [SerializeField, Min(0f)]
        public float CriticalChance = 0f; // 크리티컬 확률
    }
}
