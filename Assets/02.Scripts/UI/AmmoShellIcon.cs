using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// 탄창 HUD 탄환 한 칸입니다. PlayerAmmoUI가 칸을 만들 때 붙여서 쓰며, 칸 자체는 가로로 반전해 보여줍니다.
    /// 마지막 크리티컬 탄환의 스프라이트와 스파크, 탄을 쓸 때의 발사 연출(오버레이 + 흔들림)을 맡습니다.
    /// </summary>
    public class AmmoShellIcon : MonoBehaviour
    {
        private static readonly Vector3 FlippedScale = new(-1f, 1f, 1f);

        private Image _body;
        private RectTransform _rect;
        private Sprite _normalSprite;
        private ShellEffectData _data;
        private Image _fireOverlay;
        private Image[] _sparkOverlays;
        private float _pixelScale;
        private bool _critical;
        private Vector2 _basePosition;
        private Coroutine _fireRoutine;
        private Coroutine _sparkRoutine;

        public bool IsFiring => _fireRoutine != null;

        // 반전된 칸의 기본 스케일. 팝 애니메이션처럼 스케일을 되돌리는 코드가 반전을 잃지 않게 이 값을 씁니다.
        public Vector3 RestScale => FlippedScale;

        public void Initialize(Image body, ShellEffectData data)
        {
            _body = body;
            _data = data;
            _rect = body.rectTransform;
            _normalSprite = body.sprite;
            _pixelScale = _rect.rect.width / _normalSprite.rect.width;

            transform.localScale = FlippedScale;

            // 발사 오버레이는 반전된 탄 기준으로 그려져 있어서 부모의 반전을 다시 뒤집고, 스파크는 부모를 따라 반전됩니다.
            _fireOverlay = CreateOverlay("FireOverlay", FlippedScale);
            _sparkOverlays = new Image[_data.MaxSparks];
            for (int i = 0; i < _sparkOverlays.Length; i++)
            {
                _sparkOverlays[i] = CreateOverlay("SparkOverlay" + i, Vector3.one);
            }
        }

        private void OnEnable()
        {
            if (_data != null && _critical && !IsFiring)
            {
                ApplyCritical();
            }
        }

        private void OnDisable()
        {
            if (IsFiring)
            {
                StopFire();
            }

            _sparkRoutine = null;
            HideSparks();
        }

        public void SetCritical(bool critical)
        {
            _critical = critical;

            // 발사 연출 중에는 모양을 바꾸지 않고, 끝난 뒤에 반영합니다.
            if (!IsFiring)
            {
                ApplyCritical();
            }
        }

        public void PlayFire(Color emptyColor)
        {
            StopFire();
            StopSparks();
            _basePosition = _rect.anchoredPosition;
            _fireRoutine = StartCoroutine(FireRoutine(emptyColor));
        }

        // 연출 도중 같은 칸이 다시 장전되면 끊고 원래 모습으로 돌아갑니다. 색은 호출한 쪽이 정합니다.
        public void CancelFire()
        {
            if (!IsFiring)
            {
                return;
            }

            StopFire();
            ApplyCritical();
        }

        private void ApplyCritical()
        {
            _body.sprite = _critical ? _data.CriticalShell : _normalSprite;

            if (_critical && isActiveAndEnabled)
            {
                _sparkRoutine ??= StartCoroutine(SparkRoutine());
            }
            else
            {
                StopSparks();
            }
        }

        // 경과 시간으로 프레임을 고르기 때문에, 재생이 빨라서 프레임 길이가 렌더 프레임보다 짧아도 전체 길이가 정확하고 짧은 프레임은 건너뜁니다.
        private IEnumerator FireRoutine(Color emptyColor)
        {
            ShellEffectData.FireFrame[] frames = _data.FireFrames;
            _fireOverlay.enabled = true;

            float elapsed = 0f;
            int shown = -1;

            while (true)
            {
                int index = FrameIndexAt(elapsed);
                if (index >= frames.Length)
                {
                    break;
                }

                if (index != shown)
                {
                    shown = index;
                    SetOverlaySprite(_fireOverlay, frames[index].Sprite);
                    _rect.anchoredPosition = _basePosition + frames[index].Shake * _pixelScale;

                    if (frames[index].Empty)
                    {
                        _body.color = emptyColor;
                    }
                }

                yield return null;
                elapsed += Time.unscaledDeltaTime * _data.FirePlaybackSpeed;
            }

            _body.color = emptyColor;
            StopFire();
            ApplyCritical();
        }

        private int FrameIndexAt(float elapsed)
        {
            ShellEffectData.FireFrame[] frames = _data.FireFrames;
            int index = 0;

            while (index < frames.Length && elapsed >= frames[index].Duration)
            {
                elapsed -= frames[index].Duration;
                index++;
            }

            return index;
        }

        private void StopFire()
        {
            if (_fireRoutine != null)
            {
                StopCoroutine(_fireRoutine);
                _fireRoutine = null;
                _rect.anchoredPosition = _basePosition;
            }

            _fireOverlay.enabled = false;
        }

        private IEnumerator SparkRoutine()
        {
            Sprite[] sparks = _data.Sparks;

            while (true)
            {
                yield return new WaitForSecondsRealtime(Random.Range(_data.SparkInterval.x, _data.SparkInterval.y));

                // 서로 다른 스파크 1~3개를 한 프레임에 겹쳐 보여줍니다.
                int count = Random.Range(1, _sparkOverlays.Length + 1);
                int start = Random.Range(0, sparks.Length);
                for (int i = 0; i < count; i++)
                {
                    Image overlay = _sparkOverlays[i];
                    SetOverlaySprite(overlay, sparks[(start + i * sparks.Length / count) % sparks.Length]);
                    overlay.enabled = true;
                }

                yield return new WaitForSecondsRealtime(_data.SparkDuration);
                HideSparks();
            }
        }

        private void StopSparks()
        {
            if (_sparkRoutine != null)
            {
                StopCoroutine(_sparkRoutine);
                _sparkRoutine = null;
            }

            HideSparks();
        }

        private void HideSparks()
        {
            foreach (Image overlay in _sparkOverlays)
            {
                overlay.enabled = false;
            }
        }

        private Image CreateOverlay(string overlayName, Vector3 localScale)
        {
            var overlayObject = new GameObject(overlayName, typeof(RectTransform), typeof(Image));
            var overlayRect = (RectTransform)overlayObject.transform;
            overlayRect.SetParent(transform, false);
            overlayRect.anchorMin = overlayRect.anchorMax = new Vector2(0.5f, 0.5f);
            overlayRect.anchoredPosition = Vector2.zero;
            overlayRect.localScale = localScale;

            var image = overlayObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        // 오버레이 스프라이트의 피벗이 탄 중심에 오도록 크기와 피벗을 맞춥니다.
        private void SetOverlaySprite(Image overlay, Sprite sprite)
        {
            overlay.sprite = sprite;
            overlay.rectTransform.sizeDelta = sprite.rect.size * _pixelScale;
            overlay.rectTransform.pivot = sprite.pivot / sprite.rect.size;
        }
    }
}
