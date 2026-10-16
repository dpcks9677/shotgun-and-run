using System;
using InventorySystem;
using UnityEngine;
using UnityEngine.Serialization;

namespace ShotgunAndRun.Weapon
{
    // 파츠 강화에 필요한 재료들
    [Serializable]
    public class WeaponPartsRecipe
    {
        [FormerlySerializedAs("Parts")]
        [SerializeField]
        private WeaponPartsType _parts;

        [FormerlySerializedAs("Main")]
        [FormerlySerializedAs("MaterialA")]
        [SerializeField]
        private ItemData _main;

        [FormerlySerializedAs("Sub1")]
        [FormerlySerializedAs("MaterialB")]
        [SerializeField]
        private ItemData _sub1;

        [FormerlySerializedAs("Sub2")]
        [FormerlySerializedAs("MaterialC")]
        [SerializeField]
        private ItemData _sub2;

        [FormerlySerializedAs("Sub3")]
        [FormerlySerializedAs("MaterialD")]
        [SerializeField]
        private ItemData _sub3;

        public WeaponPartsType Parts => _parts;
        public ItemData Main => _main;
        public ItemData Sub1 => _sub1;
        public ItemData Sub2 => _sub2;
        public ItemData Sub3 => _sub3;
    }
}
