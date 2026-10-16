using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace ShotgunAndRun.Environment
{
    // 방 프리팹 루트에 붙인다. 자식 타일맵에서 LightSourceTable에 있는 타일을 찾아 그 칸마다 2D 빛 두 개를 만든다.
    //  - 밝힘 빛 (Multiply 블렌드, Light Blend Style 0): 전역 빛(Global Light 2D)으로 어두워진 주변을 되살린다
    //  - 번짐 빛 (Additive 블렌드, Light Blend Style 1): 광원 색을 바닥 · 벽 위에 더한다. GlowIntensity가 0이면 만들지 않는다
    // 빛 오브젝트는 저장하지 않는다(DontSave). 켜질 때마다 타일맵에서 다시 만들고, 에디터에서 타일을 바꾸면 다시 만든다.
    // 그래서 프리팹 · 씬에 빛이 저장되지 않아 병합 충돌이 없고, 타일을 옮기면 빛도 따라간다.
    // 빛은 씬 최상위 __Lights 하나 아래 방 이름 그룹에 모은다 (방 프리팹 자식에 넣으면 인스턴스 오버라이드로 잡힘).
    // 깜빡임 · 맥동은 플레이 중에만 12fps로 세기를 바꾼다 (픽셀 애니메이션과 같은 박자).
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class TileLightEmitter : MonoBehaviour
    {
        private const int MultiplyStyle = 0;
        private const int AdditiveStyle = 1;
        private const float StepFps = 12f;
        private const string SharedRootName = "__Lights";
        private const HideFlags LightFlags = HideFlags.DontSave | HideFlags.NotEditable;

        [SerializeField] private LightSourceTable _table;

        private sealed class Source
        {
            public LightSourceTable.Entry Entry;
            public Light2D Light;
            public Light2D Glow;
            public float Phase;
        }

        private readonly List<Source> _sources = new();
        private GameObject _root;   // 이 방의 그룹 (__Lights/<방 이름>)
        private bool _dirty;

        public int SourceCount => _sources.Count;

        private void OnEnable()
        {
            Tilemap.tilemapTileChanged += OnTileChanged;
            Rebuild();
        }

        private void OnDisable()
        {
            Tilemap.tilemapTileChanged -= OnTileChanged;
            Clear();
        }

        private void Update()
        {
            if (_dirty) Rebuild();
            if (Application.isPlaying) Sample(Time.time);
        }

        // 시간 t(초)의 깜빡임 · 맥동 세기를 적용한다. 에디터 캡처에서도 부른다.
        public void Sample(float t)
        {
            float step = Mathf.Floor(t * StepFps) / StepFps;
            foreach (var source in _sources)
            {
                float k = Factor(source.Entry, step + source.Phase);
                source.Light.intensity = source.Entry.LightIntensity * k;
                if (source.Glow != null) source.Glow.intensity = source.Entry.GlowIntensity * k;
            }
        }

        private static float Factor(LightSourceTable.Entry entry, float t)
        {
            switch (entry.Animation)
            {
                case LightSourceTable.Effect.Flicker:
                    // 느린 흔들림 + 빠른 떨림
                    float slow = Mathf.PerlinNoise(t / entry.Period, 0.37f);
                    float fast = Mathf.PerlinNoise(t / (entry.Period * 0.31f), 5.1f);
                    return 1f + entry.Amount * (2f * (0.65f * slow + 0.35f * fast) - 1f);
                case LightSourceTable.Effect.Pulse:
                    return 1f - entry.Amount * 0.5f * (1f + Mathf.Cos(t / entry.Period * Mathf.PI * 2f));
                default:
                    return 1f;
            }
        }

        public void Rebuild()
        {
            _dirty = false;
            Clear();
            if (_table == null) return;

            var scene = gameObject.scene;
            // 씬을 불러오는 중(플레이 종료 뒤 다시 열 때 등)에는 최상위 목록을 못 읽으므로 다음 Update로 미룬다
            if (scene.IsValid() && !scene.isLoaded)
            {
                _dirty = true;
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
                return;
            }

            _root = new GameObject(name) { hideFlags = LightFlags };
            if (scene.IsValid()) _root.transform.SetParent(SharedRoot(scene).transform, false);

            foreach (var tilemap in GetComponentsInChildren<Tilemap>(true))
            {
                if (!tilemap.gameObject.activeInHierarchy) continue;
                var bounds = tilemap.cellBounds;
                var tiles = tilemap.GetTilesBlock(bounds);
                for (int i = 0; i < tiles.Length; i++)
                {
                    if (tiles[i] == null || !_table.TryGet(tiles[i], out var entry) || entry.Ring == null) continue;
                    var cell = new Vector3Int(bounds.xMin + i % bounds.size.x, bounds.yMin + i / bounds.size.x % bounds.size.y, bounds.zMin);
                    // 좌우 반전 · 회전으로 놓인 타일은 빛 중심도 같이 돌린다
                    var offset = tilemap.GetTransformMatrix(cell).MultiplyVector(entry.Offset);
                    var position = tilemap.GetCellCenterWorld(cell) + offset;
                    _sources.Add(new Source
                    {
                        Entry = entry,
                        Light = CreateLight(entry, position, MultiplyStyle, entry.LightIntensity),
                        Glow = entry.GlowIntensity > 0f ? CreateLight(entry, position, AdditiveStyle, entry.GlowIntensity) : null,
                        Phase = Hash(cell) * 17f
                    });
                }
            }
#if UNITY_EDITOR
            // 스프라이트 빛 메시는 Light2D.LateUpdate에서 만들어진다. 편집 모드에서는 화면 갱신이 있어야 돌아서 한 번 돌려 둔다.
            if (!Application.isPlaying) UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        private Light2D CreateLight(LightSourceTable.Entry entry, Vector3 position, int style, float intensity)
        {
            var go = new GameObject(style == MultiplyStyle ? "Light" : "Glow") { hideFlags = HideFlags.DontSave | HideFlags.NotEditable };
            // Light2D.lightType 설정자는 바꾸기 전 종류로 메시를 만든다 → 꺼 둔 채 설정하고 켜야 처음부터 스프라이트 빛 메시가 된다
            go.SetActive(false);
            go.transform.SetParent(_root.transform, false);
            go.transform.position = position;
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Sprite;
            light.lightCookieSprite = entry.Ring;
            light.blendStyleIndex = style;
            light.color = entry.Color;
            light.intensity = intensity;
            light.shadowsEnabled = false;
            light.volumeIntensityEnabled = false;            // 볼륨 빛(스프라이트 위에 덮어 그리는 안개)은 끈다 — 소품을 가린다
            light.volumeIntensity = 0f;
            go.SetActive(true);
            return light;
        }

        // 씬마다 하나. 스크립트를 다시 컴파일하면 static 값이 지워지므로 캐시하지 않고 씬 최상위에서 찾는다.
        private static GameObject SharedRoot(Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == SharedRootName && (go.hideFlags & HideFlags.DontSave) != 0) return go;

            var root = new GameObject(SharedRootName) { hideFlags = LightFlags };
            SceneManager.MoveGameObjectToScene(root, scene);
            return root;
        }

        private void Clear()
        {
            _sources.Clear();
            if (_root == null) return;
            var parent = _root.transform.parent;
            if (Application.isPlaying)
            {
                Destroy(_root);
            }
            else
            {
                DestroyImmediate(_root);
                // 편집 모드에서 마지막 그룹이 빠지면 빈 부모도 지운다 (플레이 중에는 남아도 저장되지 않음)
                if (parent != null && parent.childCount == 0) DestroyImmediate(parent.gameObject);
            }
            _root = null;
        }

        private void OnTileChanged(Tilemap tilemap, Tilemap.SyncTile[] tiles)
        {
            if (tilemap != null && tilemap.transform.IsChildOf(transform)) _dirty = true;
        }

        private static float Hash(Vector3Int cell)
        {
            uint h = (uint)(cell.x * 73856093) ^ (uint)(cell.y * 19349663);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (h & 0xffff) / 65535f;
        }
    }
}
