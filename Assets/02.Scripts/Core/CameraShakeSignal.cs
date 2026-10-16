using System;

namespace ShotgunAndRun.Core
{
    /// <summary>
    /// 게임플레이 카메라(CameraRecoil)에 한 번짜리 흔들림을 요청하는 신호입니다.
    /// 카메라 스크립트가 다른 어셈블리에 있어 직접 부를 수 없는 쪽(출입구 문 등)이 씁니다.
    /// </summary>
    public static class CameraShakeSignal
    {
        public static event Action<float, float> Requested;

        public static void Request(float magnitude, float seconds)
        {
            Requested?.Invoke(magnitude, seconds);
        }
    }
}
