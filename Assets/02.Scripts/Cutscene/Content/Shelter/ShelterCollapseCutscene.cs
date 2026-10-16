using System.Collections;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// CS_SHELTER_COLLAPSE: 주 진입로 붕괴. 대본: Docs/quests/CS_Q1_WHETSTONE.md 5장
    /// Q1을 마치고 쉘터에 돌아오면 씬 시작 시 자동 재생합니다. 플레이어는 숨긴 채, 입구 주변의 로나와 경비병 3명이
    /// 던전 입구가 무너지는 것을 지켜봅니다. 그 뒤 플레이어가 스폰 지점에 순간이동으로 나타나고, 무리가 달려와 맞이한 다음
    /// 모두 원래 자리로 돌아갑니다.
    /// 에셋 설정: Required FLAG_Q1_CLEARED / Blocked FLAG_SHELTER_COLLAPSE_DONE / Completion FLAG_SHELTER_COLLAPSE_DONE / AutoPlay 켜기
    /// </summary>
    [CreateAssetMenu(fileName = "CS_SHELTER_COLLAPSE", menuName = "ShotgunAndRun/Cutscene/Shelter/Collapse")]
    public sealed class ShelterCollapseCutscene : CutsceneAsset
    {
        // 씬에 같은 키의 CutsceneMarker가 있어야 한다 (모두 무너지는 입구 주변)
        private const string CollapseCamera = "CollapseCamera";
        private const string CollapseLona = "CollapseLona";
        private const string CollapseGuard1 = "CollapseGuard1";
        private const string CollapseGuard2 = "CollapseGuard2";
        private const string CollapseGuard3 = "CollapseGuard3";
        private const string CollapseEntrance = "CollapseEntrance";

        // 귀환 뒤 무리가 기둥을 피해 플레이어에게 가는 경유지. 인물마다 '<인물 마커 키>Route1'(기둥 앞) · 'Route2'(기둥 뒤) 마커가 씬에 있어야 한다
        private static readonly string[] WatcherMarkers = { CollapseLona, CollapseGuard1, CollapseGuard2, CollapseGuard3 };
        private const string Route1 = "Route1";
        private const string Route2 = "Route2";

        // 씬에 같은 키의 CutsceneSignal이 있어야 한다 (계단 → 무너진 계단 교체 + 출격 구역 끄기)
        private const string CollapseSignal = "ShelterCollapse";

        [Header("등장인물")]
        [SerializeField] private CharacterProfileSO _lona;
        [SerializeField] private CharacterProfileSO _guard1;
        [SerializeField] private CharacterProfileSO _guard2;
        [SerializeField] private CharacterProfileSO _guard3;

        [Header("대사")]
        [Tooltip("대본 01: 첫 진동 뒤 경비병의 경고")]
        [SerializeField] private DialogueDataSO _warning;

        [Tooltip("대본 02~03: 붕괴 직후 로나 (플레이어를 걱정하는 마지막 줄 포함)")]
        [SerializeField] private DialogueDataSO _aftermath;

        [Tooltip("플레이어 귀환 뒤 무리가 달려와 건네는 로나의 말")]
        [SerializeField] private DialogueDataSO _reunion;

        [Header("귀환 연출")]
        [Tooltip("플레이어가 스폰 지점에 나타나는 순간이동 도착 연출 (CS_TELEPORT_ARRIVE)")]
        [SerializeField] private TeleportInCutscene _arrival;

        [Header("소리 (비워 두면 소리 없이 진행)")]
        [SerializeField] private AudioClip _rumble;
        [SerializeField] private AudioClip _explosion;

        [Header("연출 수치")]
        [SerializeField, Min(0f)] private float _fadeSeconds = 1f;
        [SerializeField, Min(0f)] private float _openingHold = 0.6f;
        [SerializeField, Min(0f)] private float _rumblePower = 0.05f;
        [SerializeField, Min(0f)] private float _rumbleSeconds = 1.2f;
        [SerializeField, Min(0f)] private float _collapsePower = 0.25f;
        [SerializeField, Min(0f)] private float _collapseSeconds = 1.6f;
        [SerializeField, Min(0f)] private float _dustSettleSeconds = 0.8f;
        [SerializeField, Min(0f)] private float _alertSeconds = 1f;
        [SerializeField, Min(0f)] private float _cameraPanSeconds = 1.5f;
        [SerializeField, Min(0f)] private float _rushSpeed = 8f;

        protected override void DeclareRequirements(CutsceneRequirements r)
        {
            r.Actor(_lona, nameof(_lona));
            r.Actor(_guard1, nameof(_guard1));
            r.Actor(_guard2, nameof(_guard2));
            r.Actor(_guard3, nameof(_guard3));
            r.Dialogue(_warning, nameof(_warning));
            r.Dialogue(_aftermath, nameof(_aftermath));
            r.Dialogue(_reunion, nameof(_reunion));
            r.Asset(_arrival, nameof(_arrival));
            r.Marker(CollapseCamera, CollapseLona, CollapseGuard1, CollapseGuard2, CollapseGuard3, CollapseEntrance);
            foreach (string key in WatcherMarkers)
            {
                r.Marker(key + Route1, key + Route2);
            }

            r.Signal(CollapseSignal);
        }

        protected override IEnumerator Play(CutsceneContext c)
        {
            var lona = c.Actor(_lona);
            var guard1 = c.Actor(_guard1);
            var guard2 = c.Actor(_guard2);
            var guard3 = c.Actor(_guard3);

            // 끝나고 되돌릴 원래 자리와 방향 (에셋 필드가 아니라 지역 변수에 둔다)
            CutsceneActor[] watchers = { lona, guard1, guard2, guard3 };
            var homes = new Vector3[watchers.Length];
            var facesLeft = new bool[watchers.Length];
            for (int i = 0; i < watchers.Length; i++)
            {
                homes[i] = watchers[i].Position;
                facesLeft[i] = c.IsFacingLeft(watchers[i]);
            }

            // 중단 · 예외로 끝나도 플레이어가 숨은 채, 인물이 입구 앞에 남지 않게 한 번은 되돌린다
            bool restored = false;
            void Restore()
            {
                if (restored) return;
                restored = true;
                for (int i = 0; i < watchers.Length; i++)
                {
                    c.TeleportToPosition(watchers[i], homes[i]);
                    c.Face(watchers[i], facesLeft[i]);
                }
                c.SetVisible(c.Player, true);
            }

            try
            {
                // 배치: 화면을 덮은 채 플레이어를 숨기고, 로나와 경비병을 입구 주변에 세워 입구를 보게 한다
                c.FadeOut(0f);
                c.SetVisible(c.Player, false);
                c.Teleport(lona, CollapseLona);
                c.Teleport(guard1, CollapseGuard1);
                c.Teleport(guard2, CollapseGuard2);
                c.Teleport(guard3, CollapseGuard3);
                c.FaceTo(lona, CollapseEntrance);
                c.FaceTo(guard1, CollapseEntrance);
                c.FaceTo(guard2, CollapseEntrance);
                c.FaceTo(guard3, CollapseEntrance);
                c.CameraTo(CollapseCamera, 0f);

                // 1비트: 페이드 인, 낮은 울림과 첫 진동. 경비병이 입구 쪽을 경계한다
                yield return c.FadeIn(_fadeSeconds);
                yield return c.Wait(_openingHold);
                PlaySfx(c, _rumble);
                c.CameraShake(_rumblePower, _rumbleSeconds);
                c.Bubble(guard2, EmotionType.Alert, _alertSeconds);
                yield return c.Bubble(guard3, EmotionType.Alert, _alertSeconds);
                yield return c.Say(_warning);

                // 2비트: 굉음과 함께 입구가 무너진다. 계단을 무너진 계단으로 바꾸고 잔해 · 먼지가 떨어진다 (CutsceneShakeDebris)
                PlaySfx(c, _explosion);
                c.Signal(CollapseSignal);
                yield return c.CameraShake(_collapsePower, _collapseSeconds);
                yield return c.Wait(_dustSettleSeconds);

                // 3비트: 로나의 한탄 (말풍선은 대사 데이터에 있음)
                yield return c.Say(_aftermath);

                // 4비트: 페이드 없이 카메라를 스폰 지점으로 옮기고, 플레이어가 순간이동으로 나타나는 모습을 비춘다
                yield return c.CameraToPlayer(_cameraPanSeconds);
                yield return _arrival.PlayArrival(c);

                // 5비트: 카메라가 로나에게 돌아가는 동안 무리가 경유지를 거쳐, 모인 모양 그대로 플레이어에게 달려온다
                // 무리의 중심은 무리를 비추던 카메라 마커다
                Vector3 center = c.MarkerPoint(CollapseCamera);
                var rush = new CutsceneHandle[watchers.Length + 1];
                rush[watchers.Length] = c.CameraTo(lona, _cameraPanSeconds);
                for (int i = 0; i < watchers.Length; i++)
                {
                    rush[i] = c.MovePathToPositions(watchers[i], _rushSpeed,
                        c.MarkerPoint(WatcherMarkers[i] + Route1),
                        c.MarkerPoint(WatcherMarkers[i] + Route2),
                        c.Player.Position + watchers[i].Position - center);
                }

                yield return c.WaitAll(rush);

                // 6비트: 서로 마주 보고 로나가 플레이어를 맞는다 (대사가 끝나면 페이드 아웃)
                foreach (var watcher in watchers)
                {
                    c.FaceTo(watcher, c.Player);
                }

                c.FaceTo(c.Player, lona);
                yield return c.Say(_reunion);

                // 마무리: 페이드 아웃 동안 모두 원래 자리로 돌려보내고, 플레이어를 보이게 한 뒤 카메라를 돌린다
                yield return c.FadeOut(_fadeSeconds);
                Restore();
                yield return c.CameraToPlayer(0f);
                yield return c.FadeIn(_fadeSeconds);
            }
            finally
            {
                Restore();
            }
        }

        private static void PlaySfx(CutsceneContext c, AudioClip clip)
        {
            if (clip != null)
            {
                c.PlaySfx(clip);
            }
        }
    }
}
