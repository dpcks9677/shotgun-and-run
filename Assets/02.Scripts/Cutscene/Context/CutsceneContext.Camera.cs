namespace ShotgunAndRun.Cutscene
{
    // 카메라 명령: 지점 · 인물 비추기, 흔들기
    public sealed partial class CutsceneContext
    {
        /// <summary>
        /// 지금 화면 가운데에서 좌우 끝까지의 거리(월드 유닛)
        /// </summary>
        public float CameraHalfWidth => Camera.orthographicSize * Camera.aspect;

        /// <summary>
        /// 카메라를 마커 위치로 blend초 동안 부드럽게 옮깁니다. 0이면 즉시 옮깁니다.
        /// </summary>
        public CutsceneHandle CameraTo(string marker, float blend)
        {
            var position = MarkerPosition(marker);
            return _camera.MoveTo(() => position, marker, blend);
        }

        /// <summary>
        /// 카메라를 인물에게 blend초 동안 옮기고, 그 뒤에는 인물을 따라갑니다.
        /// </summary>
        public CutsceneHandle CameraTo(CutsceneActor actor, float blend)
        {
            RequireActor(actor);
            return _camera.MoveTo(() => actor.Position, actor, blend);
        }

        /// <summary>
        /// 카메라를 플레이어에게 돌립니다.
        /// </summary>
        public CutsceneHandle CameraToPlayer(float blend)
        {
            return CameraTo(Player, blend);
        }

        /// <summary>
        /// 카메라 크기(orthographicSize)를 seconds초 동안 바꿉니다. 작을수록 확대입니다. 컷씬이 끝나면 원래 크기로 돌아갑니다.
        /// </summary>
        public CutsceneHandle CameraZoom(float size, float seconds)
        {
            return _camera.ZoomTo(size, seconds);
        }

        /// <summary>
        /// 카메라 크기를 컷씬이 시작할 때 크기의 ratio배로 seconds초 동안 바꿉니다. 1보다 작으면 확대입니다.
        /// </summary>
        public CutsceneHandle CameraZoomScale(float ratio, float seconds)
        {
            return _camera.ZoomTo(_camera.BaseSize * ratio, seconds);
        }

        /// <summary>
        /// 카메라 크기를 컷씬이 시작할 때의 크기로 seconds초 동안 되돌립니다.
        /// </summary>
        public CutsceneHandle CameraZoomReset(float seconds)
        {
            return _camera.ZoomTo(_camera.BaseSize, seconds);
        }

        /// <summary>
        /// 카메라를 power(유닛) 세기로 seconds초 동안 흔듭니다. 세기는 점점 줄어듭니다.
        /// </summary>
        public CutsceneHandle CameraShake(float power, float seconds)
        {
            return _camera.Shake(power, seconds);
        }
    }
}
