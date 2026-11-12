using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 모든 퀘스트의 정의(제목·목표·보상·대사·선행 조건)를 담은 에셋.
    ///
    /// 에셋: Resources/Data/Quest Data. DataManager가 불러 두고 QuestManager가 읽는다.
    /// 진행 상태(수락했는지, 몇 마리 잡았는지)는 여기 저장하지 않는다. 그건 QuestManager가 따로 관리하고 세이브한다.
    /// </summary>
    [CreateAssetMenu(fileName = "Quest Data", menuName = "Scriptable Objects/Quest Data")]
    public class QuestData : ScriptableObject
    {
        /// <summary>
        /// 퀘스트 종류(어떻게 진행되는지). 정수 값이 Quest Data 에셋과 몬스터 프리팹의 QuestItem에 저장돼 있어서
        /// 값을 숫자로 고정해 둔다. 예전에 쓰던 값(0 무기 수집, 2 웨이브)은 지웠으니 그 번호는 다시 쓰지 않는다.
        /// </summary>
        public enum Type
        {
            None = -1,
            Kill = 1,               // 몬스터 처치 (QuestItem → ProcessQuest)
            Talk = 3,               // NPC와 대화 (NPCTalkState → AdvanceTalkQuest)
            Collect = 4,            // 아이템 수집 (인벤토리 보유 수)
            TutorialPotion = 5,     // 물약 구매·퀵슬롯 등록 (TutorialQuestTracker)
            TutorialSkill = 6,      // 스킬 습득·퀵슬롯 등록 (TutorialQuestTracker)
            TutorialEnhance = 7,    // 장비 강화 1회 (TutorialQuestTracker)
        }

        /// <summary>
        /// 처치 대상. 이름이 EnemyData.enemyId와 같아야 한다 (퀘스트를 수락하면 이 이름으로 몬스터를 생성한다).
        /// 정수 값이 에셋·프리팹에 저장돼 있어 숫자로 고정해 둔다. 예전 몬스터·수집 대상(0~5)은 지웠으니 그 번호는 다시 쓰지 않는다.
        /// </summary>
        public enum TargetType
        {
            None = -1,
            Vampire = 6,
            Nightshade = 7,
            Skeletonzombie = 8,
            DrekMalos = 9,
            Zyrathon = 10,
        }

        /// <summary>메인 퀘스트 / 서브 퀘스트. 같은 NPC에 둘 다 있으면 메인을 먼저 제안한다.</summary>
        public enum Category { Main, Sub }

        /// <summary>퀘스트 하나의 정의.</summary>
        [System.Serializable]
        public class Quest
        {
            [Header("기본")]
            [Tooltip("퀘스트 번호. 세이브·선행 조건·엔딩 판정이 이 번호를 쓰므로 한번 정하면 바꾸지 않는다")]
            public int questID;
            public string questTitle = string.Empty;
            [Tooltip("퀘스트 창 목록에 보이는 짧은 설명")]
            [TextArea(2, 4)] public string questDescription = string.Empty;
            [Tooltip("목표 한 줄 요약. '→ {이 문구} 0/5' 형태로 보인다 (예: 몬스터 처치)")]
            public string objectiveLabel = string.Empty;
            public Category category = Category.Main;

            [Header("목표")]
            public Type type = Type.None;
            [Tooltip("처치형·수집형에서 생성할 몬스터 종류")]
            public TargetType targetType = TargetType.None;
            [Tooltip("목표 수 (처치 수, 모을 개수, 대화 횟수 등)")]
            public int countToComplete;
            [Tooltip("이 지역(ZoneArea 이름)에서 잡은 몬스터만 인정한다. 비어 있으면 어디서든 인정")]
            public string zoneName = string.Empty;
            [Tooltip("퀘스트를 수락할 때 생성하는 몬스터의 레벨")]
            public int monsterLevel;
            [Tooltip("수집형이 모아야 하는 아이템. 인벤토리 보유 수가 진행도가 되고, 완료 보고 때 목표 수만큼 가져간다")]
            public Item collectItem;

            [Header("보상")]
            public float exp;
            public int rewardGold;
            [Tooltip("퀘스트 창에 보이는 보상 아이템 이름")]
            public string rewardItemDisplayName = string.Empty;
            public int rewardItemCount;
            [Tooltip("실제로 지급할 아이템. 비워 두면 아이템을 주지 않는다")]
            public Item rewardItem;

            [Header("NPC")]
            [Tooltip("퀘스트를 주는 NPC 번호")]
            public int giverNpcID;
            [Tooltip("완료 보고를 받는 NPC 번호 (대부분 주는 NPC와 같다)")]
            public int turnInNpcID;
            [Tooltip("이 퀘스트가 열리려면 먼저 완료해야 하는 퀘스트 번호들")]
            public List<int> prerequisiteQuestIDs = new();

            [Header("대사 ('|'로 페이지를 나눈다)")]
            [Tooltip("퀘스트를 제안할 때")]
            public string questBeginText = string.Empty;
            [Tooltip("진행 중에 말을 걸었을 때")]
            public string questProgressText = string.Empty;
            [Tooltip("완료 보고(보상 지급) 때")]
            public string questCompleteText = string.Empty;
            [Tooltip("제안에서 '거절'을 눌렀을 때 NPC가 하는 말. 비워 두면 거절해도 아무 말 없이 창이 그대로 있다")]
            public string questDeclineText = string.Empty;

            [Header("수락 옵션")]
            [Tooltip("켜면 수락/거절 버튼 없이, 제안 대사를 끝까지 읽고 창을 닫는 순간 자동으로 수락한다 (첫 튜토리얼 퀘스트용)")]
            public bool autoAccept;
            [Tooltip("수락하는 순간 인벤토리에 지급할 아이템 (예: 봉인에 쓸 블러드스톤). 비워 두면 주지 않는다")]
            public Item acceptItem;
            public int acceptItemCount = 1;
        }

        public List<Quest> quests = new();
    }
}
