using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 컷씬 디버거가 디버그 스폰 마커 키로 찾을 수 있는 위치입니다. CutsceneMarker 외의 씬 오브젝트(예: 튜토리얼 구역)가 구현합니다.
    /// </summary>
    public interface ICutsceneSpawnPoint
    {
        public string Key
        {
            get;
        }

        public Vector3 SpawnPosition
        {
            get;
        }
    }
}
