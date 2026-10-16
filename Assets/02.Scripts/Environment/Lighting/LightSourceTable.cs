using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ShotgunAndRun.Environment
{
    // 빛을 내는 타일 목록 (촛불 · 화로 · 수정 · 영혼병 등)과 타일마다 만들 빛 설정.
    // TileLightEmitter가 방 타일맵에서 이 표에 있는 타일을 찾아 그 자리에 2D 빛을 만든다.
    // 빛 모양은 픽셀 계단 링 스프라이트(LightRing_*)라 반경마다 스프라이트가 따로 있다.
    [CreateAssetMenu(menuName = "ShotgunAndRun/Environment/Light Source Table", fileName = "LightSourceTable")]
    public sealed class LightSourceTable : ScriptableObject
    {
        public enum Effect
        {
            None,
            Flicker,    // 불꽃: 불규칙하게 흔들림
            Pulse       // 수정 · 영혼병: 일정한 주기로 서서히 밝아졌다 어두워짐
        }

        [Serializable]
        public sealed class Entry
        {
            [Tooltip("이 설정을 쓰는 타일들")]
            public List<TileBase> Tiles = new();
            [Tooltip("타일 칸 가운데에서 빛 중심까지 (월드 단위, 1/16 단위로 맞춘다)")]
            public Vector2 Offset;
            [Tooltip("빛 모양 (픽셀 계단 링). 반경은 스프라이트 크기로 정해진다")]
            public Sprite Ring;
            public Color Color = Color.white;
            [Tooltip("주변을 밝히는 빛 (Multiply 블렌드). 전역 어둡기를 이만큼 되돌린다")]
            [Min(0f)] public float LightIntensity = 0.3f;
            [Tooltip("빛 번짐 (Additive 블렌드). 색이 바닥 · 벽 위에 더해진다")]
            [Min(0f)] public float GlowIntensity = 0.15f;
            public Effect Animation = Effect.None;
            [Tooltip("Pulse 한 주기 (초) · Flicker 변화 빠르기 (초)")]
            [Min(0.05f)] public float Period = 1f;
            [Tooltip("세기가 흔들리는 폭 (0 ~ 1, 기준 세기에 대한 비율)")]
            [Range(0f, 1f)] public float Amount = 0.2f;
        }

        [SerializeField] private List<Entry> _entries = new();

        private Dictionary<TileBase, Entry> _lookup;

        public IReadOnlyList<Entry> Entries => _entries;

        public bool TryGet(TileBase tile, out Entry entry)
        {
            if (_lookup == null) BuildLookup();
            return _lookup.TryGetValue(tile, out entry);
        }

        private void OnValidate() => _lookup = null;

        private void BuildLookup()
        {
            _lookup = new Dictionary<TileBase, Entry>();
            foreach (var entry in _entries)
            {
                foreach (var tile in entry.Tiles)
                {
                    if (tile != null) _lookup[tile] = entry;
                }
            }
        }
    }
}
