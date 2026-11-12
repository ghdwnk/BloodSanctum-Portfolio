using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// GameTestTools - 플레이어 성장 (레벨·경험치·포인트·MP·스킬)
    /// </summary>
    public partial class GameTestTools
    {
        [Tooltip("'레벨 적용' 버튼이 맞출 목표 레벨")]
        [SerializeField, Min(1)] private int targetLevel = 10;
        [Tooltip("'경험치 추가' 버튼이 더하는 양. 레벨업 연출·포인트 지급이 실제 플레이와 똑같이 일어난다")]
        [SerializeField, Min(0f)] private float expAmount = 1000f;
        [SerializeField, Min(0)] private int skillPointAmount = 5;
        [SerializeField, Min(0)] private int statPointAmount = 5;
        [Tooltip("켜면 MP가 계속 가득 찬 상태로 유지된다")]
        [SerializeField] private bool infiniteMP;

        // MP를 매 프레임 채우면 UI 갱신이 잦으므로 이 간격(초)마다 채운다
        private const float MPRefillInterval = 0.25f;
        private float nextMPRefillTime;

        public int MaxLevel => DataManager.Instance != null ? DataManager.Instance.playerData.levels.Count : 1;

        private PlayerLevelController LevelController =>
            playerState != null ? playerState.GetComponentInChildren<PlayerLevelController>(true) : null;

        private PlayerStats Stats => playerState != null ? playerState.GetComponent<PlayerStats>() : null;

        // 인스펙터 맨 위에 보여 줄 현재 상태 요약 (플레이 중에만 의미가 있다)
        public string BuildStatusText()
        {
            if (!EnsurePlayer())
            {
                return "플레이어를 찾지 못했습니다.";
            }

            PlayerLevelController levelController = LevelController;
            PlayerStats stats = Stats;
            IReadOnlyList<QuestData.Quest> quests = QuestList;
            int questTotal = quests != null ? quests.Count : 0;

            return $"레벨 {(levelController != null ? levelController.Level : 0)} / {MaxLevel}    "
                   + $"경험치 {(levelController != null ? levelController.CurrentExp : 0f):0}\n"
                   + $"체력 {playerHP.CurrentHP:0} / {playerHP.MaxHP:0}\n"
                   + $"골드 {CurrentGold:N0}    강화석 {CurrentEnhanceStones}\n"
                   + $"스탯 포인트 {(stats != null ? stats.AvailableStatPoints : 0)}    "
                   + $"스킬 포인트 {(stats != null ? stats.AvailableSkillPoints : 0)}\n"
                   + $"퀘스트  완료 {CountQuests(QuestManager.QuestProgressState.Completed)} / {questTotal}    "
                   + $"진행 중 {CountQuests(QuestManager.QuestProgressState.Accepted) + CountQuests(QuestManager.QuestProgressState.ReadyToTurnIn)}    "
                   + $"수락 가능 {CountQuests(QuestManager.QuestProgressState.Available)}";
        }

        // Update에서 부른다. 무한 MP가 켜져 있으면 일정 간격으로 MP를 채운다
        private void KeepMP()
        {
            if (!infiniteMP || Time.unscaledTime < nextMPRefillTime)
            {
                return;
            }

            nextMPRefillTime = Time.unscaledTime + MPRefillInterval;
            FillMP();
        }

        public void FillMP()
        {
            if (!IsAllowed || !EnsurePlayer())
            {
                return;
            }

            playerState.GetComponentInChildren<MPController>()?.RestoreMP(100000f);
        }

        // 레벨을 목표 레벨에 맞춘다.
        // 올릴 때는 경험치를 더해 실제 레벨업 경로를 그대로 타므로 포인트·레벨업 연출이 정상적으로 나온다.
        // 내릴 때는 그 레벨의 시작 경험치로 되돌린다 (이미 받은 포인트는 회수하지 않는다)
        public void ApplyLevel()
        {
            PlayerLevelController levelController = IsAllowed && EnsurePlayer() ? LevelController : null;
            if (levelController == null)
            {
                return;
            }

            int target = Mathf.Clamp(targetLevel, 1, MaxLevel);
            float requiredExp = DataManager.Instance.playerData.levels[target - 1].requiredExp;

            if (target > levelController.Level)
            {
                levelController.GainExp(requiredExp - levelController.CurrentExp);
            }
            else if (target < levelController.Level)
            {
                levelController.LoadState(target, requiredExp);
                playerState.OnPlayerLevelUP(target);
            }

            Debug.Log($"[GameTestTools] 레벨 {levelController.Level} (경험치 {levelController.CurrentExp:0})");
        }

        public void AddExp()
        {
            if (!IsAllowed || !EnsurePlayer())
            {
                return;
            }

            LevelController?.GainExp(expAmount);
        }

        public void GiveSkillPoints()
        {
            if (IsAllowed && EnsurePlayer())
            {
                Stats?.GrantSkillPoints(skillPointAmount);
            }
        }

        public void GiveStatPoints()
        {
            if (IsAllowed && EnsurePlayer())
            {
                Stats?.GrantStatPoints(statPointAmount);
            }
        }

        // 보유 스킬을 전부 배우고 최대 레벨로 올린다
        public void UnlockAllSkills()
        {
            if (!IsAllowed || !EnsurePlayer())
            {
                return;
            }

            PlayerSkillController skillController = playerState.GetComponent<PlayerSkillController>();
            if (skillController == null)
            {
                return;
            }

            List<string> names = new List<string>();
            List<int> levels = new List<int>();

            for (int ix = 0; ix < skillController.SkillCount; ++ix)
            {
                SkillData skill = skillController.GetSkill(ix);
                if (skill != null)
                {
                    names.Add(skill.name);
                    levels.Add(skill.maxLevel);
                }
            }

            skillController.LoadUnlockedSkills(names, levels);
            Debug.Log($"[GameTestTools] 스킬 {names.Count}개를 최대 레벨로 배웠습니다.");
        }
    }
}
