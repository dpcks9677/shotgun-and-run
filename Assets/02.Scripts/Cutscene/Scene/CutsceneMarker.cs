using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 컷씬 연출 위치를 표시하는 씬 오브젝트입니다. 컷씬 코드는 좌표 대신 이 마커의 키를 씁니다.
    /// 컷씬 칸을 비우면 여러 컷씬이 함께 쓰는 공용 마커가 됩니다. (조회 순서: 해당 컷씬 전용 → 공용)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CutsceneMarker : MonoBehaviour
    {
        [Tooltip("이 마커를 쓰는 컷씬. 비우면 공용 마커입니다.")]
        [SerializeField] private CutsceneAsset _cutscene;

        [Tooltip("컷씬 코드에서 쓰는 마커 키 (예: GuardAside)")]
        [SerializeField] private string _key;

        public CutsceneAsset Cutscene => _cutscene;
        public string Key => _key;
        public Vector3 Position => transform.position;

        private void OnDrawGizmos()
        {
            Gizmos.color = _cutscene != null ? new Color(0.3f, 0.85f, 1f, 0.9f) : new Color(1f, 0.75f, 0.3f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.15f);
            Gizmos.DrawLine(transform.position + Vector3.left * 0.25f, transform.position + Vector3.right * 0.25f);
            Gizmos.DrawLine(transform.position + Vector3.down * 0.25f, transform.position + Vector3.up * 0.25f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.35f, string.IsNullOrEmpty(_key) ? "(키 없음)" : _key);
#endif
        }
    }
}
