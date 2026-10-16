using System.Collections;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// (프로토타입) CS_TUTORIAL_ESCAPE: 빨강 구간. 무너지는 복도를 달려 나와 방에 도착하고, 복도가 막힌 것을 돌아본다.
    /// 대본: Docs/quests/CS_TUTORIAL.md 3-1. 시작할 때 플레이어는 맨손 · 체력 3칸이고 HUD가 없으며, 끝나면 체력 · 대시 HUD가 나타난다.
    /// </summary>
    [CreateAssetMenu(fileName = "CS_TUTORIAL_ESCAPE", menuName = "ShotgunAndRun/Cutscene/Tutorial/Escape")]
    public sealed class TutorialEscapeCutscene : CutsceneAsset
    {
        private const string EscapeStart = "EscapeStart";
        private const string EscapeCorridorEnd = "EscapeCorridorEnd";
        private const string EscapeStop = "EscapeStop";
        private const string CollapseSignal = "CollapseDebris";

        [Header("대사")]
        [Tooltip("2줄: '돌아갈 길이 막혔어...' · '일단 앞으로 더 가보자.' (화자: 플레이어)")]
        [SerializeField] private DialogueDataSO _lines;

        [Tooltip("1줄: '으아아악!' (화자: 플레이어). 화면이 검은 채로 잔해 소리와 함께 나오고, 끝나면 페이드 인")]
        [SerializeField] private DialogueDataSO _scream;

        [Header("효과음 (임시, 비워 두면 소리 없이 진행)")]
        [SerializeField] private AudioClip _rumble;
        [Tooltip("컷씬이 시작되기 전 암전 중에 순서대로 울리는 잔해 떨어지는 소리 (2~3개)")]
        [SerializeField] private AudioClip[] _blackoutSfx = new AudioClip[0];

        [Header("연출 수치")]
        [SerializeField, Min(1)] private int _startHealth = 3;
        [SerializeField, Min(0.1f)] private float _runSpeed = 7f;
        [SerializeField, Min(0.1f)] private float _walkSpeed = 3f;
        [SerializeField, Min(0f)] private float _collapseSeconds = 2f;
        [SerializeField, Min(0f)] private float _fadeInSeconds = 1f;
        [Tooltip("암전 중 잔해 소리 사이의 시간(초)")]
        [SerializeField, Min(0f)] private float _blackoutSfxGap = 0.5f;
        [Tooltip("몇 번째 잔해 소리(0부터)와 함께 비명 대사를 시작할지")]
        [SerializeField, Min(0)] private int _screamAtHit = 1;

        protected override void DeclareRequirements(CutsceneRequirements r)
        {
            r.Dialogue(_lines, nameof(_lines));
            if (_scream != null)
            {
                r.Dialogue(_scream, nameof(_scream));
            }

            r.Marker(EscapeStart, EscapeCorridorEnd, EscapeStop);
            r.Signal(CollapseSignal);
        }

        protected override IEnumerator Play(CutsceneContext c)
        {
            var player = c.Player;

            // 배치: 맨손 · 체력 3칸으로 복도 안쪽 끝에서 시작. 화면은 검게 덮어 둔다
            c.SetPlayerArmed(false);
            c.SetPlayerHealth(_startHealth);
            c.ClearConsumables();
            c.Teleport(player, EscapeStart);
            c.Face(player, left: false);
            c.CameraToPlayer(0f);
            c.FadeOut(0f);
            c.Letterbox(true);

            // 암전인 채로 잔해가 무너지는 소리가 몇 번 들리고 비명이 나온다. 대사가 끝나면 페이드 인
            CutsceneHandle scream = null;
            for (int i = 0; i < _blackoutSfx.Length; i++)
            {
                if (_blackoutSfx[i] != null)
                {
                    c.PlaySfx(_blackoutSfx[i]);
                }

                if (i == _screamAtHit && _scream != null)
                {
                    scream = c.Say(_scream);
                }

                yield return c.Wait(_blackoutSfxGap);
            }

            if (scream == null && _scream != null)
            {
                scream = c.Say(_scream);
            }

            if (scream != null)
            {
                yield return scream;
            }

            // 복도 안쪽에서 굉음과 함께 달려 나온다
            c.FadeIn(_fadeInSeconds);
            PlayRumble(c);
            c.CameraShake(0.12f, 3f);
            yield return c.MoveTo(player, EscapeCorridorEnd, _runSpeed);

            // 방에 닿으면 걸음으로 바뀐다
            yield return c.MoveTo(player, EscapeStop, _walkSpeed);

            // 복도가 무너져 잔해가 입구를 가린다
            PlayRumble(c);
            c.CameraShake(0.4f, _collapseSeconds);
            c.Signal(CollapseSignal);
            yield return c.Wait(_collapseSeconds);

            // 뒤를 돌아보며 혼잣말
            yield return c.Say(_lines, line =>
            {
                c.Face(player, left: line == 0);
            });

            c.Letterbox(false);
            c.ShowHud("Health");
            c.ShowHud("Dash");
        }

        private void PlayRumble(CutsceneContext c)
        {
            if (_rumble != null)
            {
                c.PlaySfx(_rumble);
            }
        }
    }
}
