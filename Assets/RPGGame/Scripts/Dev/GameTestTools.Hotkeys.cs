using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGGame
{
    /// <summary>
    /// GameTestTools - 단축키.
    /// 인스펙터 버튼은 에디터에서만 쓸 수 있어서, 개발 빌드(Development Build)에서도 같은 기능을 부를 수 있게
    /// 기능키(F1~F12, 숫자패드)에 연결한다. 새 기능을 만든 것이 아니라 기존 버튼 함수를 그대로 부르는 입구다.
    /// 에디터나 개발 빌드에서만 동작하고 릴리스 빌드에서는 아무 일도 하지 않는다 (IsAllowed).
    /// 게임이 쓰는 키(1~6, A/C/D/F/I/J/K/S/U/W, 방향키, Shift, Space, Enter, Esc)와 겹치지 않는 키만 골랐다.
    ///
    ///  F1  체력·MP 가득            F7   보스 체력 절반 깎기
    ///  F2  무적 + MP 무한 토글     F8   보스 즉시 처치
    ///  F3  진행 중 퀘스트 목표 채우기   F9   필드 몬스터 전부 처치
    ///  F4  진행 중 퀘스트 즉시 완료     F10  다음 지역으로 이동 (순환)
    ///  F5  드렉 말로스 보스전 시작   F11  마을로 이동
    ///  F6  자라톤 보스전 시작        F12  지원 키트 (골드·강화석·장비·물약)
    ///  숫자패드 +  경험치 추가      숫자패드 *  스킬 전부 배우기 + 포인트 지급
    ///  숫자패드 -  게임 속도 순환 (1배 → 2배 → 0.5배 → 1배)
    /// </summary>
    public partial class GameTestTools
    {
        [Header("단축키")]
        [Tooltip("끄면 단축키를 쓰지 않는다 (인스펙터 버튼은 그대로 쓸 수 있다)")]
        [SerializeField] private bool enableHotkeys = true;

        // 지역 순환 이동용. 처음 쓸 때 한 번만 모으고 이름순으로 정렬해 순서를 고정한다
        private ZoneArea[] zoneCycle;
        private int zoneCycleIndex;
        private int speedCycleIndex;
        private static readonly float[] SpeedCycle = { 1f, 2f, 0.5f };

        // 단축키 입력은 다른 입력 처리가 끝난 뒤에 읽는다 (기존 Update와 이름이 겹치지 않게 LateUpdate에 둔다)
        private void LateUpdate()
        {
            if (!enableHotkeys || !IsAllowed)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !EnsurePlayer())
            {
                return;
            }

            if (keyboard.f1Key.wasPressedThisFrame)
            {
                HealPlayerFull();
                FillMP();
                Notify("체력·MP 가득");
            }
            else if (keyboard.f2Key.wasPressedThisFrame)
            {
                godMode = !godMode;
                infiniteMP = godMode;
                Notify(godMode ? "무적 + MP 무한 켜짐" : "무적 + MP 무한 꺼짐");
            }
            else if (keyboard.f3Key.wasPressedThisFrame)
            {
                QuestReachAllObjectives();
                Notify("진행 중 퀘스트 목표 채움");
            }
            else if (keyboard.f4Key.wasPressedThisFrame)
            {
                QuestCompleteAllInProgress();
                Notify("진행 중 퀘스트 즉시 완료");
            }
            else if (keyboard.f5Key.wasPressedThisFrame)
            {
                StartBoss(BossType.DrekMalos);
            }
            else if (keyboard.f6Key.wasPressedThisFrame)
            {
                StartBoss(BossType.Zyrathon);
            }
            else if (keyboard.f7Key.wasPressedThisFrame)
            {
                CutBossHP();
                Notify("보스 체력 절반 깎음");
            }
            else if (keyboard.f8Key.wasPressedThisFrame)
            {
                KillBoss();
                Notify("보스 즉시 처치");
            }
            else if (keyboard.f9Key.wasPressedThisFrame)
            {
                KillAllMonsters();
                Notify("필드 몬스터 전부 처치");
            }
            else if (keyboard.f10Key.wasPressedThisFrame)
            {
                TeleportToNextZone();
            }
            else if (keyboard.f11Key.wasPressedThisFrame)
            {
                TeleportToVillage();
                Notify("마을로 이동");
            }
            else if (keyboard.f12Key.wasPressedThisFrame)
            {
                GiveSupportKit();
            }
            else if (keyboard.numpadPlusKey.wasPressedThisFrame)
            {
                AddExp();
                Notify($"경험치 +{expAmount:0}");
            }
            else if (keyboard.numpadMultiplyKey.wasPressedThisFrame)
            {
                UnlockAllSkills();
                GiveSkillPoints();
                GiveStatPoints();
                Notify("스킬 전부 배움 + 포인트 지급");
            }
            else if (keyboard.numpadMinusKey.wasPressedThisFrame)
            {
                CycleGameSpeed();
            }
        }

        // 조우 대사까지 보여 주려고 '대사 건너뛰기'를 끄고 보스전을 시작한다 (기존 설정은 되돌려 놓는다)
        private void StartBoss(BossType type)
        {
            bool savedSkip = skipEncounterDialogue;
            bossType = type;
            skipEncounterDialogue = false;

            StartBossFight();

            skipEncounterDialogue = savedSkip;
            Notify($"{type} 보스전 시작");
        }

        // 씬의 지역(ZoneArea)을 이름순으로 한 곳씩 돌아가며 이동한다
        private void TeleportToNextZone()
        {
            if (zoneCycle == null || zoneCycle.Length == 0)
            {
                zoneCycle = FindObjectsByType<ZoneArea>();
                Array.Sort(zoneCycle, (a, b) => string.CompareOrdinal(a.name, b.name));
            }

            if (zoneCycle.Length == 0)
            {
                Notify("이동할 지역이 없습니다");
                return;
            }

            ZoneArea zone = zoneCycle[zoneCycleIndex % zoneCycle.Length];
            zoneCycleIndex = (zoneCycleIndex + 1) % zoneCycle.Length;

            TeleportToZone(zone);
            Notify($"{zone.name}으로 이동");
        }

        // 골드·강화석·모든 장비·소모품을 한 번에 지급한다
        private void GiveSupportKit()
        {
            AddGold();
            AddEnhanceStones();
            GiveAllEquipment();
            GiveAllConsumables();
            Notify("지원 키트 지급");
        }

        private void CycleGameSpeed()
        {
            speedCycleIndex = (speedCycleIndex + 1) % SpeedCycle.Length;
            gameSpeed = SpeedCycle[speedCycleIndex];
            ApplyGameSpeed();
            Notify($"게임 속도 {gameSpeed:0.#}배");
        }

        // 눌렀을 때 화면에 한 줄 알린다. 편집에서 잘라 낼 구간을 알아보기 쉽게 [DEBUG] 머리말을 붙인다
        private static void Notify(string message)
        {
            UIPickupFeed.ShowMessage($"[DEBUG] {message}");
        }
    }
}
