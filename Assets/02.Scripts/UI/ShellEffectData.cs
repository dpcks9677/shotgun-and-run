using System;
using UnityEngine;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// 탄창 HUD 탄환의 크리티컬 표시(스프라이트 + 스파크)와 발사 연출(오버레이 + 흔들림) 데이터입니다.
    /// 스프라이트는 CritShell.png · ShellFire.png 시트이고, 모양과 타이밍은 Docs/tools/spritegen/ui/ 의 crit_shell.py · shell_fire.py 시안 기준입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "ShellEffectData", menuName = "ShotgunAndRun/UI/Shell Effect Data")]
    public class ShellEffectData : ScriptableObject
    {
        [Serializable]
        public struct FireFrame
        {
            public Sprite Sprite;

            [Tooltip("재생 속도 배율을 적용하기 전 프레임 길이 (초)")]
            public float Duration;

            [Tooltip("탄과 오버레이가 함께 움직이는 양 (탄 스프라이트 픽셀 단위, 화면 기준)")]
            public Vector2 Shake;

            [Tooltip("이 프레임부터 탄이 빈 탄피 색으로 바뀝니다")]
            public bool Empty;
        }

        [Header("Critical Shell")]
        [Tooltip("마지막 크리티컬 탄환 스프라이트 (반전 전 모양)")]
        public Sprite CriticalShell;

        [Tooltip("마지막 크리티컬 탄환 위에서 번쩍이는 흰 스파크 (반전 전 모양)")]
        public Sprite[] Sparks;

        [Tooltip("스파크가 쉬는 시간의 범위 (초)")]
        public Vector2 SparkInterval = new(0.3f, 1.4f);

        [Min(0f)]
        public float SparkDuration = 0.06f;

        [Tooltip("한 번에 겹쳐 번쩍이는 스파크 개수의 최대값")]
        [Range(1, 3)]
        public int MaxSparks = 3;

        [Header("Fire")]
        [Tooltip("탄을 쓸 때 재생하는 오버레이 프레임 (반전된 탄 기준으로 그려져 있음)")]
        public FireFrame[] FireFrames;

        [Tooltip("1보다 크면 시안 GIF보다 빠르게 재생합니다")]
        [Min(0.1f)]
        public float FirePlaybackSpeed = 4f;
    }
}
