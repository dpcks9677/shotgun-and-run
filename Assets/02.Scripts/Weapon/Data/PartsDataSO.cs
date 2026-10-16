using UnityEngine;

namespace ShotgunAndRun.Weapon
{
    [CreateAssetMenu(fileName = "PartsData", menuName = "ShotgunAndRun/Weapon/PartsDataSO")]
    public class PartsDataSO : ScriptableObject
    {
        [Header("Parts Info")]
        [SerializeField] private string _id;
        [SerializeField] private string _partsName;
        [SerializeField] private Sprite _icon;
        [SerializeField] private WeaponPartsType _partsType;
        [SerializeField] private string[] _stepUpgradeDescription;

        public string Id => _id;
        public string PartsName => _partsName;
        public Sprite Icon => _icon;
        public WeaponPartsType PartsType => _partsType;
        public string[] StepUpgradeDescription => _stepUpgradeDescription;
    }
}
