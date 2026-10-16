using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace ShotgunAndRun.Weapon
{
    [Serializable]
    public struct UpgradeStepCost
    {
        [FormerlySerializedAs("A")]
        [SerializeField, Min(0)] private int _mainAmount;

        [FormerlySerializedAs("B")]
        [SerializeField, Min(0)] private int _sub1Amount;

        [FormerlySerializedAs("C")]
        [SerializeField, Min(0)] private int _sub2Amount;

        [FormerlySerializedAs("D")]
        [SerializeField, Min(0)] private int _sub3Amount;

        [FormerlySerializedAs("Core")]
        [SerializeField, Min(0)] private int _coreAmount;

        public int MainAmount => _mainAmount;
        public int Sub1Amount => _sub1Amount;
        public int Sub2Amount => _sub2Amount;
        public int Sub3Amount => _sub3Amount;
        public int CoreAmount => _coreAmount;
    }
}
