using UnityEngine;
using UnityEngine.Events;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 씬 고유 동작을 컷씬에 연결합니다. 컷씬 코드는 c.Signal("키")처럼 키만 부르고,
    /// 실제 동작(적 웨이브 생성, 오브젝트 켜기, UI 열기 등)은 이 컴포넌트의 이벤트에 인스펙터로 연결합니다.
    /// 대사 데이터의 Signal 칸도 같은 키로 이 이벤트를 호출합니다.
    /// </summary>
    public sealed class CutsceneSignal : MonoBehaviour
    {
        [Tooltip("신호 키 (예: SpawnAmbush)")]
        [SerializeField] private string _key;

        [Tooltip("신호를 받으면 실행할 동작")]
        [SerializeField] private UnityEvent _onRaised = new UnityEvent();

        public string Key => _key;

        public void Raise()
        {
            _onRaised.Invoke();
        }
    }
}
