using System;
using System.Collections.Generic;
using System.Reflection;

namespace ShotgunAndRun.Quest
{
    // 퀘스트 · 컷씬이 쓰는 플래그 이름 모음입니다. 코드에서는 문자열 대신 이 상수를 쓰고,
    // 에셋에 문자열로 적은 플래그는 IsKnown으로 검사합니다 (QuestCatalogSO의 Validate Catalog).
    // 새 플래그를 쓰려면 먼저 여기에 추가합니다.
    public static class QuestFlags
    {
        // ── 영구 플래그 (QuestProgress.Flags) ──
        public const string TutorialCompleted = "FLAG_TUTORIAL_COMPLETED";
        public const string PrologueCompleted = "FLAG_PROLOGUE_COMPLETED";
        public const string ShelterUnlocked = "FLAG_SHELTER_UNLOCKED";
        public const string Q1IntroDone = "FLAG_Q1_INTRO_DONE";
        public const string Q1Accepted = "FLAG_Q1_ACCEPTED";
        public const string Q1Cleared = "FLAG_Q1_CLEARED";
        public const string ShelterCollapseDone = "FLAG_SHELTER_COLLAPSE_DONE";
        public const string Q1DebriefDone = "FLAG_Q1_DEBRIEF_DONE";
        public const string Q2BriefingDone = "FLAG_Q2_BRIEFING_DONE";
        public const string Q2Accepted = "FLAG_Q2_ACCEPTED";
        public const string ElenaAmbushSeen = "FLAG_ELENA_AMBUSH_SEEN";
        public const string ElenaTeleportSeen = "FLAG_ELENA_TELEPORT_SEEN";
        public const string ElenaRescued = "FLAG_ELENA_RESCUED";
        public const string Q2PortalDone = "FLAG_Q2_PORTAL_DONE";

        // 탐사 퀘스트 없이 Tab 지도의 존을 여는 플래그 (NPC 대화 등 특수 상황). 이름은 MapReveal(존 ID)로 만듭니다.
        private const string MapRevealPrefix = "FLAG_MAP_REVEAL_";
        public const string MapRevealZ1 = MapRevealPrefix + "Z1";
        public const string MapRevealZ2 = MapRevealPrefix + "Z2";
        public const string MapRevealZ3 = MapRevealPrefix + "Z3";
        public const string MapRevealZ4 = MapRevealPrefix + "Z4";
        public const string MapRevealZ5 = MapRevealPrefix + "Z5";
        public const string MapRevealZ6 = MapRevealPrefix + "Z6";
        public const string MapRevealZ7 = MapRevealPrefix + "Z7";
        public const string MapRevealZ8 = MapRevealPrefix + "Z8";
        public const string MapRevealZ9 = MapRevealPrefix + "Z9";
        public const string MapRevealZ10 = MapRevealPrefix + "Z10";

        // ── 런 플래그 (QuestService.RaiseRunFlag, 탈출해야 반영) ──
        public const string H2ElenaFreed = "H2_ELENA_FREED";
        public const string Z4DefenseCleared = "Z4_DEFENSE_CLEARED";
        public const string Z7DefenseCleared = "Z7_DEFENSE_CLEARED";
        public const string Z5EliteSniperKilled = "Z5_ELITE_SNIPER_KILLED";

        private static HashSet<string> _all;

        // 위 상수 전체
        public static IReadOnlyCollection<string> All => _all ??= CollectConstants();

        public static string MapReveal(string zoneId)
        {
            return MapRevealPrefix + zoneId;
        }

        public static bool IsKnown(string flag)
        {
            return !string.IsNullOrEmpty(flag) && (_all ??= CollectConstants()).Contains(flag);
        }

        private static HashSet<string> CollectConstants()
        {
            HashSet<string> flags = new HashSet<string>(StringComparer.Ordinal);
            foreach (FieldInfo field in typeof(QuestFlags).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.IsLiteral && field.FieldType == typeof(string))
                {
                    flags.Add((string)field.GetRawConstantValue());
                }
            }

            return flags;
        }
    }
}
