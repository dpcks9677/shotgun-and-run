using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    // 이펙트 명령: 인물 하얗게 물들이기 · VFX 프리팹 소환. 물들이기용 덧그림은 컷씬이 끝나면 모두 지웁니다.
    public sealed partial class CutsceneContext
    {
        private const string WhiteOverlayName = "CutsceneWhiteOverlay";

        private sealed class WhiteOverlay
        {
            public SpriteRenderer Source;
            public SpriteRenderer Overlay;
        }

        private sealed class WhiteOutState
        {
            public readonly List<WhiteOverlay> Overlays = new List<WhiteOverlay>();
            public float Alpha;
        }

        private readonly List<WhiteOutState> _whiteOuts = new List<WhiteOutState>();

        /// <summary>
        /// 인물의 보이는 스프라이트마다 단색 덧그림을 올리고, seconds초 동안 투명에서 완전한 흰색으로 물들입니다.
        /// 애니메이션은 계속 재생됩니다. silhouette는 스프라이트 알파를 SpriteRenderer 색으로 칠하는 머티리얼입니다.
        /// 덧그림은 인물이 숨겨지거나 컷씬이 끝날 때까지 남고, 컷씬이 끝나면 지워집니다.
        /// </summary>
        public CutsceneHandle WhiteOut(CutsceneActor actor, Material silhouette, float seconds)
        {
            return WhiteFade(actor, silhouette, 0f, 1f, seconds);
        }

        /// <summary>
        /// WhiteOut의 반대입니다. 인물을 완전한 흰색으로 덮은 채 시작해 seconds초 동안 원래 색으로 되돌립니다.
        /// 숨긴 인물이면 먼저 보이게 한 뒤 호출하세요 (꺼진 스프라이트에는 덧그림을 올리지 않음).
        /// </summary>
        public CutsceneHandle WhiteIn(CutsceneActor actor, Material silhouette, float seconds)
        {
            return WhiteFade(actor, silhouette, 1f, 0f, seconds);
        }

        private CutsceneHandle WhiteFade(CutsceneActor actor, Material silhouette, float from, float to, float seconds)
        {
            RequireActor(actor);
            if (silhouette == null)
            {
                Debug.LogError("WhiteOut · WhiteIn: silhouette 머티리얼이 비어 있어 건너뜁니다.", Asset);
                return CutsceneHandle.Done();
            }

            // 화면 카메라가 그리지 않는 레이어(미니맵 아이콘 등)는 칠하지 않는다
            int visibleLayers = _camera.Camera.cullingMask;

            var whiteOut = new WhiteOutState { Alpha = from };
            foreach (var source in actor.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!source.enabled || source.name == WhiteOverlayName) continue;
                if ((visibleLayers & (1 << source.gameObject.layer)) == 0) continue;

                var go = new GameObject(WhiteOverlayName);
                go.layer = source.gameObject.layer;
                go.transform.SetParent(source.transform, false);
                var overlay = go.AddComponent<SpriteRenderer>();
                overlay.sharedMaterial = silhouette;
                overlay.sortingLayerID = source.sortingLayerID;
                overlay.sortingOrder = source.sortingOrder + 1;
                overlay.maskInteraction = source.maskInteraction;
                whiteOut.Overlays.Add(new WhiteOverlay { Source = source, Overlay = overlay });
            }

            _whiteOuts.Add(whiteOut);
            ApplyWhiteOut(whiteOut);
            return Run(WhiteOutRoutine(whiteOut, from, to, seconds));
        }

        /// <summary>
        /// VFX 프리팹을 position에 소환해 돌려줍니다. 부모 없이 소환하며, 사라지는 것은 프리팹이 맡으므로 컷씬이 끝나도 지우지 않습니다.
        /// </summary>
        public GameObject SpawnEffect(GameObject prefab, Vector3 position)
        {
            if (prefab == null)
            {
                Debug.LogError("SpawnEffect: 이펙트 프리팹이 비어 있어 건너뜁니다.", Asset);
                return null;
            }

            return Object.Instantiate(prefab, position, Quaternion.identity);
        }

        private IEnumerator WhiteOutRoutine(WhiteOutState whiteOut, float from, float to, float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
                whiteOut.Alpha = Mathf.Lerp(from, to, elapsed / seconds);
            }

            whiteOut.Alpha = to;
        }

        // 매 프레임 원본의 스프라이트 · 뒤집힘 · 크기를 덧그림에 복사하고 흰색 알파를 적용한다
        private void UpdateWhiteOuts()
        {
            foreach (var whiteOut in _whiteOuts)
            {
                ApplyWhiteOut(whiteOut);
            }
        }

        private static void ApplyWhiteOut(WhiteOutState whiteOut)
        {
            foreach (var pair in whiteOut.Overlays)
            {
                var overlay = pair.Overlay;
                var source = pair.Source;
                if (overlay == null) continue;

                if (source == null)
                {
                    overlay.enabled = false;
                    continue;
                }

                overlay.enabled = source.enabled;
                overlay.sprite = source.sprite;
                overlay.flipX = source.flipX;
                overlay.flipY = source.flipY;
                overlay.drawMode = source.drawMode;
                if (source.drawMode != SpriteDrawMode.Simple)
                {
                    overlay.size = source.size;
                }
                overlay.color = new Color(1f, 1f, 1f, whiteOut.Alpha);
            }
        }

        private void ClearWhiteOuts()
        {
            foreach (var whiteOut in _whiteOuts)
            {
                foreach (var pair in whiteOut.Overlays)
                {
                    if (pair.Overlay != null) Object.Destroy(pair.Overlay.gameObject);
                }
            }
            _whiteOuts.Clear();
        }
    }
}
