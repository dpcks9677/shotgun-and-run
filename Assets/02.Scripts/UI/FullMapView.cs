using System;
using ShotgunAndRun.Core;
using ShotgunAndRun.Quest;
using UnityEngine;
using UnityEngine.UI;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// Tab 지도 그림입니다. 완료한 탐사 퀘스트의 존만 공개한 지도를 합성하고,
    /// 플레이어가 있는 도형 가운데에 붉은 점을 찍습니다.
    /// 재료 텍스처는 Docs/tools/map/map_doodle.py export로 만들며, 모두 Read/Write 허용 · 압축 없음이어야 합니다.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class FullMapView : MonoBehaviour
    {
        [Serializable]
        private class MapMeta
        {
            public int originX;     // 칸 좌표 → 앵커 텍스처 좌표의 원점
            public int originY;
            public string[] zones;  // 존 번호 순서: 공개 마스크 채널 · 안개 탈출구 R값(1부터)
            public Color32 fogColor;
        }

        // 4x4 Bayer 디더 (map_doodle.py와 같은 무늬)
        private static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        [SerializeField] private Texture2D _full;
        [Tooltip("존 4개씩 R·G·B·A 채널에 담긴 공개 마스크 (map-reveal-0, 1, 2 순서)")]
        [SerializeField] private Texture2D[] _reveals;
        [SerializeField] private Texture2D _fogExits;
        [SerializeField] private Texture2D _anchors;
        [SerializeField] private TextAsset _meta;
        [Tooltip("흰색 점 스프라이트. 색은 Image.color로, 미니맵 플레이어 아이콘(MinimapIcon)과 같은 값을 씁니다.")]
        [SerializeField] private RectTransform _playerDot;
        [Tooltip("지도 배율을 1배로 잡는 화면 높이")]
        [SerializeField] private float _referenceHeight = 1080f;
        [Tooltip("기준 화면 높이에서의 지도 배율")]
        [SerializeField] private float _baseScale = 1.5f;

        // 도트 지도가 비정수 배율에서 디더 무늬가 깨지지 않도록 배율을 이 단위로 맞춥니다.
        private const float ScaleStep = 0.5f;

        private MapMeta _data;
        private Canvas _canvas;
        private Color32[] _anchorPixels;
        private Texture2D _result;
        private Transform _player;
        private int _revealedZones = -1;

        private void Awake()
        {
            _data = JsonUtility.FromJson<MapMeta>(_meta.text);
            _canvas = GetComponentInParent<Canvas>().rootCanvas;
            _anchorPixels = _anchors.GetPixels32();
            _result = new Texture2D(_full.width, _full.height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };

            RawImage image = GetComponent<RawImage>();
            image.texture = _result;
            image.SetNativeSize();
        }

        private void OnEnable()
        {
            UpdateScale();

            int revealed = GetRevealedZones();
            if (revealed != _revealedZones)
            {
                _revealedZones = revealed;
                Compose(revealed);
            }
        }

        private void Update()
        {
            UpdatePlayerDot();
        }

        private void OnDestroy()
        {
            Destroy(_result);
        }

        // 화면 높이에 비례한 배율을 단위에 맞추고, 화면 안에 들어가게 줄입니다. (캔버스는 고정 픽셀 크기)
        private void UpdateScale()
        {
            float scale = GetPixelScale(new Vector2(Screen.width, Screen.height), new Vector2(_full.width, _full.height));
            transform.localScale = Vector3.one * (scale / _canvas.scaleFactor);
        }

        // 화면 픽셀 기준 배율: 화면 높이에 비례한 값을 ScaleStep 단위로 맞추되, 지도가 화면에 다 들어오게 합니다.
        private float GetPixelScale(Vector2 screen, Vector2 map)
        {
            float scale = Mathf.Round(_baseScale * screen.y / _referenceHeight / ScaleStep) * ScaleStep;
            float fit = Mathf.Min(screen.x / map.x, screen.y / map.y);
            return Mathf.Min(scale, Mathf.Floor(fit / ScaleStep) * ScaleStep);
        }

        // 완료(보고)한 탐사 퀘스트의 존과 지도 공개 플래그가 켜진 존을 비트로 모읍니다.
        private int GetRevealedZones()
        {
            GameManager manager = GameManager.Instance;
            int revealed = 0;
            if (manager == null || manager.Quests == null)
            {
                return revealed;
            }

            foreach (QuestDefinitionSO quest in manager.Quests.GetQuests(QuestState.Completed))
            {
                int zone = Array.IndexOf(_data.zones, quest.ZoneA);
                if (zone >= 0 && quest.Type is QuestType.MainStory or QuestType.EarlyExploration)
                {
                    revealed |= 1 << zone;
                }
            }

            for (int zone = 0; zone < _data.zones.Length; zone++)
            {
                if (manager.Progress.Quests.HasFlag(QuestFlags.MapReveal(_data.zones[zone])))
                {
                    revealed |= 1 << zone;
                }
            }

            return revealed;
        }

        // 공개한 존의 마스크를 더해 디더로 자른 곳에 지도를, 나머지에는 안개 위 탈출구를 그립니다.
        private void Compose(int revealed)
        {
            int width = _full.width;
            int height = _full.height;
            Color32[] full = _full.GetPixels32();
            Color32[] fog = _fogExits.GetPixels32();
            Color32[][] masks = new Color32[_reveals.Length][];
            for (int i = 0; i < masks.Length; i++)
            {
                masks[i] = (revealed >> (i * 4) & 0xF) != 0 ? _reveals[i].GetPixels32() : null;
            }

            Color32[] pixels = new Color32[full.Length];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    float coverage = 0f;
                    for (int zone = 0; zone < _data.zones.Length; zone++)
                    {
                        if ((revealed >> zone & 1) != 0)
                        {
                            coverage += GetChannel(masks[zone / 4][i], zone % 4) / 255f;
                        }
                    }

                    // 텍스처 행은 아래부터라 위에서 센 행으로 뒤집어 디더 무늬를 맞춥니다.
                    float threshold = (Bayer[((height - 1 - y) & 3) * 4 + (x & 3)] * 2 + 1) / 32f;
                    if (coverage > threshold)
                    {
                        pixels[i] = new Color32(full[i].r, full[i].g, full[i].b, 255);
                    }
                    else if (fog[i].a != 0 && (revealed >> (fog[i].r - 1) & 1) == 0)
                    {
                        pixels[i] = _data.fogColor;
                    }
                }
            }

            _result.SetPixels32(pixels);
            _result.Apply();
        }

        private static byte GetChannel(Color32 color, int channel)
        {
            return channel switch
            {
                0 => color.r,
                1 => color.g,
                2 => color.b,
                _ => color.a
            };
        }

        // 플레이어가 있는 칸의 표시 지점(R·G = x, B·A = y 픽셀)에 점을 옮깁니다. 소속 칸이 없으면 숨깁니다.
        private void UpdatePlayerDot()
        {
            if (_player == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                _player = player != null ? player.transform : null;
            }

            Color32 anchor = default;
            if (_player != null)
            {
                int x = Mathf.FloorToInt(_player.position.x) - _data.originX;
                int y = Mathf.FloorToInt(_player.position.y) - _data.originY;
                if ((uint)x < _anchors.width && (uint)y < _anchors.height)
                {
                    anchor = _anchorPixels[y * _anchors.width + x];
                }
            }

            Vector2 position = new Vector2(anchor.r | anchor.g << 8, anchor.b | anchor.a << 8);
            _playerDot.gameObject.SetActive(position != Vector2.zero);
            _playerDot.anchoredPosition = position;
        }
    }
}
