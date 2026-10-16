using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace ShotgunAndRun.Player
{
    // 샷건 산탄 범위를 흰색 도트 호로 표시합니다. (행동 진행 막대와 같은 16 PPU 도트 스타일)
    // 호의 중심은 총구, 반지름은 총구에서 커서까지의 거리, 각도는 총열 레벨로 정해지는 산탄각입니다.
    // 호 양 끝에는 중심 쪽을 향한 짧은 눈금을 붙입니다.
    // 픽셀은 총구 중심의 로컬 격자에서 PixelArc로 계산해 런타임 텍스처에 찍고, 호 모양이 1px 이상 바뀔 때만 다시 그립니다.
    // 오브젝트는 회전하지 않고 매 프레임 총구를 따라 옮기기만 하므로, 이동 중에도 도트 모양이 그대로 유지됩니다.
    // PlayerCameraFollow가 카메라와 조준점을 먼저 갱신한 뒤 실행합니다.
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpreadArcIndicator : MonoBehaviour
    {
        [Header("Player References")]
        [SerializeField] private PlayerAim _aim;
        [SerializeField] private PlayerWeapon _weapon;

        [Header("Pixel Layout")]
        [SerializeField, Min(1f)] private float _pixelsPerUnit = 16f;
        [SerializeField, Min(0)] private int _tickPixels = 3;
        [SerializeField, Min(0f)] private float _minRadiusPixels = 8f;
        [SerializeField, Min(0.1f)] private float _redrawThresholdPixels = 1f;
        [SerializeField] private Color32 _color = new(255, 255, 255, 255);

        [Header("No Ammo Feedback")]
        [SerializeField] private Color _noAmmoColor = Color.red;
        [SerializeField, Min(0.01f)] private float _noAmmoFeedbackDuration = 0.3f;
        [SerializeField, Min(0f)] private float _noAmmoShakeAmplitude = 0.4f;
        [SerializeField, Min(0f)] private float _noAmmoShakeFrequency = 48f;

        // 텍스처는 필요할 때만 이 단위로 키웁니다.
        private const int TextureBlock = 32;

        // 로컬 격자에서 총구가 놓이는 위치 (픽셀 (0, 0)의 한가운데)
        private static readonly Vector2 LocalCenter = new(0.5f, 0.5f);

        private readonly List<Vector2Int> _pixels = new();
        private readonly List<Vector2Int> _scratch = new();
        private readonly List<int> _drawnIndices = new();

        private SpriteRenderer _renderer;
        private Texture2D _texture;
        private Sprite _sprite;
        private Color _defaultRendererColor;
        private Coroutine _noAmmoFeedbackRoutine;
        private bool _isNoAmmoFeedback;
        private float _noAmmoFeedbackElapsed;
        private bool _hasShape;
        private float _drawnRadius;
        private float _drawnAimAngle;
        private float _drawnHalfAngle;
        private Vector2Int _drawnMin;

        // 다시 그린 횟수 (검증용)
        public int RedrawCount
        {
            get;
            private set;
        }

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _defaultRendererColor = _renderer.color;
            _renderer.enabled = false;
        }

        public void PlayNoAmmoFeedback()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (_noAmmoFeedbackRoutine != null)
            {
                StopCoroutine(_noAmmoFeedbackRoutine);
            }

            _noAmmoFeedbackRoutine = StartCoroutine(NoAmmoFeedbackRoutine());
        }

        private IEnumerator NoAmmoFeedbackRoutine()
        {
            _isNoAmmoFeedback = true;
            _noAmmoFeedbackElapsed = 0f;

            while (_noAmmoFeedbackElapsed < _noAmmoFeedbackDuration)
            {
                _noAmmoFeedbackElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _isNoAmmoFeedback = false;
            _noAmmoFeedbackElapsed = 0f;
            _noAmmoFeedbackRoutine = null;
        }

        private void Start()
        {
            Player player = GetComponentInParent<Player>();
            if (player == null)
            {
                player = Player.PlayerInstance;
            }

            if (player != null)
            {
                _aim ??= player.Aim;
                _weapon ??= player.GetComponent<PlayerWeapon>();
            }

            if (_aim == null || _weapon == null || _weapon.FirePoint == null)
            {
                Debug.LogError("SpreadArcIndicator reference is missing.", this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            if (_noAmmoFeedbackRoutine != null)
            {
                StopCoroutine(_noAmmoFeedbackRoutine);
                _noAmmoFeedbackRoutine = null;
            }

            _isNoAmmoFeedback = false;
            _noAmmoFeedbackElapsed = 0f;

            if (_renderer != null)
            {
                _renderer.color = _defaultRendererColor;
            }

            Hide();
        }

        private void OnDestroy()
        {
            if (_sprite != null)
            {
                Destroy(_sprite);
            }

            if (_texture != null)
            {
                Destroy(_texture);
            }
        }

        private void LateUpdate()
        {
            if (_renderer == null)
            {
                return;
            }

            _renderer.color = _isNoAmmoFeedback
                ? _noAmmoColor
                : _defaultRendererColor;

            if (_aim == null || _weapon == null || _weapon.FirePoint == null)
            {
                return;
            }

            Vector2 firePoint = _weapon.FirePoint.position;
            Vector2 toCursor = _aim.AimPoint - firePoint;
            float radius = toCursor.magnitude * _pixelsPerUnit;

            if (radius < _minRadiusPixels)
            {
                Hide();
                return;
            }

            // 발사와 스프라이트가 사용하는 최종 총열 방향을 그대로 씁니다.
            Vector2 fireDirection = _aim.WeaponAimDirection;
            float aimAngle = Mathf.Atan2(fireDirection.y, fireDirection.x) * Mathf.Rad2Deg;
            float halfAngle = _weapon.SpreadHalfAngle;

            // 호 모양(반지름 · 방향 · 산탄각)이 1px 이상 바뀔 때만 다시 그립니다.
            // 커서를 가만히 두고 이동하면 총구 → 커서 벡터가 그대로이므로 다시 그리지 않습니다.
            // (1px 기준이 반올림 경계에서 생기는 깜빡임도 막아 줍니다.)
            bool shapeChanged =
                !_hasShape ||
                !Mathf.Approximately(halfAngle, _drawnHalfAngle) ||
                PixelArc.MaxShift(_drawnRadius, _drawnAimAngle, radius, aimAngle, halfAngle) >= _redrawThresholdPixels;

            if (shapeChanged && Redraw(radius, aimAngle, halfAngle))
            {
                _drawnRadius = radius;
                _drawnAimAngle = aimAngle;
                _drawnHalfAngle = halfAngle;
                _hasShape = true;
            }

            if (!_hasShape)
            {
                Hide();
                return;
            }

            // 매 프레임 총구를 따라 옮기기만 합니다. 픽셀 격자에 스냅하지 않으므로,
            // 카메라와 조준점은 PlayerCameraFollow에서 먼저 갱신되므로,
            // 여기서는 현재 총구 위치만 따라갑니다.
            Vector2 origin = firePoint + (_drawnMin - LocalCenter) / _pixelsPerUnit;
            float shakeX = 0f;

            if (_isNoAmmoFeedback)
            {
                float progress = Mathf.Clamp01(
                    _noAmmoFeedbackElapsed / _noAmmoFeedbackDuration);
                float damping = 1f - progress;
                shakeX = Mathf.Sin(
                             _noAmmoFeedbackElapsed * _noAmmoShakeFrequency * Mathf.PI * 2f)
                         * _noAmmoShakeAmplitude
                         * damping;
            }

            transform.SetPositionAndRotation(
                new Vector3(origin.x + shakeX, origin.y, transform.position.z),
                Quaternion.identity);

            _renderer.enabled = true;
        }

        // 호 픽셀은 총구를 중심으로 한 로컬 격자에서 계산합니다.
        // 월드 격자에서 계산하면 이동할 때마다 총구의 소수점 위치가 바뀌어 픽셀 모양이 달라집니다(자글거림).
        private bool Redraw(float radius, float aimAngle, float halfAngle)
        {
            PixelArc.Build(
                LocalCenter,
                radius,
                aimAngle,
                halfAngle,
                _tickPixels,
                _pixels,
                _scratch);

            if (_pixels.Count == 0)
            {
                return false;
            }

            Vector2Int min = _pixels[0];
            Vector2Int max = _pixels[0];

            foreach (Vector2Int pixel in _pixels)
            {
                min = Vector2Int.Min(min, pixel);
                max = Vector2Int.Max(max, pixel);
            }

            EnsureTexture(max.x - min.x + 1, max.y - min.y + 1);

            NativeArray<Color32> data = _texture.GetPixelData<Color32>(0);
            int width = _texture.width;

            foreach (int index in _drawnIndices)
            {
                data[index] = default;
            }

            _drawnIndices.Clear();

            foreach (Vector2Int pixel in _pixels)
            {
                int index = (pixel.y - min.y) * width + (pixel.x - min.x);
                data[index] = _color;
                _drawnIndices.Add(index);
            }

            _texture.Apply(false);
            _drawnMin = min;
            RedrawCount++;
            return true;
        }

        private void EnsureTexture(int width, int height)
        {
            if (_texture != null && _texture.width >= width && _texture.height >= height)
            {
                return;
            }

            int newWidth = RoundUpToBlock(Mathf.Max(width, _texture != null ? _texture.width : 0));
            int newHeight = RoundUpToBlock(Mathf.Max(height, _texture != null ? _texture.height : 0));

            if (_sprite != null)
            {
                Destroy(_sprite);
            }

            if (_texture != null)
            {
                Destroy(_texture);
            }

            _texture = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false)
            {
                name = "SpreadArc",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            NativeArray<Color32> data = _texture.GetPixelData<Color32>(0);
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = default;
            }

            _drawnIndices.Clear();

            _sprite = Sprite.Create(
                _texture,
                new Rect(0f, 0f, newWidth, newHeight),
                Vector2.zero,
                _pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            _sprite.name = "SpreadArc";
            _renderer.sprite = _sprite;
        }

        private void Hide()
        {
            if (_renderer != null)
            {
                _renderer.enabled = false;
            }
        }

        private static int RoundUpToBlock(int value)
        {
            return Mathf.Max(TextureBlock, (value + TextureBlock - 1) / TextureBlock * TextureBlock);
        }
    }
}
