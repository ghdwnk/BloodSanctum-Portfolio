using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// GameTestTools - 퀘스트.
    /// 처음부터 마지막까지 어느 지점으로든 건너뛰거나 되돌릴 수 있게 한다.
    /// 모든 동작은 선행 퀘스트를 자동으로 먼저 완료해 주므로, 순서를 신경 쓰지 않고 원하는 퀘스트만 고르면 된다
    /// </summary>
    public partial class GameTestTools
    {
        [Tooltip("켜면 강제로 완료되는 퀘스트의 보상(골드·경험치·아이템)도 함께 지급한다. 끄면 상태만 바뀐다")]
        [SerializeField] private bool giveQuestRewards;

        [Tooltip("'대상 퀘스트까지 클리어'의 대상. 이 퀘스트와 그 선행 퀘스트(연쇄)를 완료 상태로 만든다. " +
                 "퀘스트 ID는 진행 순서와 다르므로(예: 1→8→9→2→10→3) 번호가 아니라 선행 관계를 따라간다")]
        [SerializeField, Min(0)] private int jumpToQuestID = 4;
        [Tooltip("끄면 대상 퀘스트는 클리어하지 않고 선행 퀘스트만 클리어해, 대상 퀘스트를 '수락 가능' 상태로 남긴다. 그 퀘스트 자체를 테스트할 때 쓴다")]
        [SerializeField] private bool jumpClearsTarget;
        [Tooltip("켜면 이동 전에 모든 퀘스트를 처음 상태로 되돌린다. 앞 단계로 돌아가려면 켜 둔다")]
        [SerializeField] private bool resetBeforeJump = true;

        private static QuestManager Quests => QuestManager.Instance;

        public IReadOnlyList<QuestData.Quest> QuestList =>
            DataManager.Instance != null && DataManager.Instance.questData != null ? DataManager.Instance.questData.quests : null;

        private bool QuestsReady => IsAllowed && Quests != null && QuestList != null;

        // ── 한 퀘스트 단위 ──

        // 퀘스트를 수락한다. 선행 퀘스트가 남아 있으면 먼저 완료하고, 이미 완료한 퀘스트면 다시 받을 수 있게 되돌린다
        public void QuestAccept(int questID)
        {
            if (!QuestsReady)
            {
                return;
            }

            CompletePrerequisites(questID);

            if (Quests.GetState(questID) == QuestManager.QuestProgressState.Completed)
            {
                Quests.DebugReset(questID);
            }

            Quests.AcceptQuest(questID);
        }

        // 퀘스트 목표를 즉시 채워 '완료 보고 대기' 상태로 만든다 (NPC에게 가서 보고하면 완료)
        public void QuestReachObjective(int questID)
        {
            if (!QuestsReady)
            {
                return;
            }

            if (Quests.GetState(questID) != QuestManager.QuestProgressState.Accepted)
            {
                QuestAccept(questID);
            }

            QuestData.Quest quest = Quests.GetQuest(questID);
            if (quest != null)
            {
                // 수집형이면 실제 재료도 채워 줘야 NPC에게 보고할 때 제출할 수 있다
                Quests.DebugGrantCollectItems(questID);
                Quests.SetProgress(questID, quest.countToComplete);
            }
        }

        // 퀘스트를 즉시 완료한다. 선행 퀘스트가 남아 있으면 먼저 완료한다
        public void QuestComplete(int questID)
        {
            if (!QuestsReady)
            {
                return;
            }

            CompletePrerequisites(questID);
            Quests.DebugComplete(questID, giveQuestRewards, true);
        }

        // 퀘스트를 처음 상태로 되돌린다. 이미 받은 보상은 회수하지 않는다
        public void QuestReset(int questID)
        {
            if (QuestsReady)
            {
                Quests.DebugReset(questID);
            }
        }

        // ── 일괄 ──

        // 대상 퀘스트까지의 진행을 한 번에 건너뛴다.
        // 퀘스트 번호가 아니라 선행 관계를 따라가므로, 번호가 뒤섞인 튜토리얼 퀘스트도 올바른 순서로 처리된다.
        // '대상도 클리어'를 끄면 대상 퀘스트가 수락 가능 상태로 남아 그 퀘스트를 처음부터 테스트할 수 있다
        public void QuestJump()
        {
            if (!QuestsReady)
            {
                return;
            }

            QuestData.Quest target = Quests.GetQuest(jumpToQuestID);
            if (target == null)
            {
                Debug.LogWarning($"[GameTestTools] 퀘스트 ID {jumpToQuestID}번이 없습니다.");
                return;
            }

            if (resetBeforeJump)
            {
                Quests.DebugResetAll();
            }

            int completed = CompletePrerequisites(target.questID);

            if (jumpClearsTarget && Quests.DebugComplete(target.questID, giveQuestRewards))
            {
                ++completed;
            }

            Debug.Log($"[GameTestTools] '{target.questTitle}'({target.questID})까지 진행했습니다. " +
                      $"(이번에 완료된 퀘스트 {completed}개, 대상은 {Quests.GetState(target.questID)})");
        }

        // 퀘스트 ID로 제목을 찾는다. 인스펙터에서 입력한 ID가 어떤 퀘스트인지 보여 주는 데 쓴다
        public string GetQuestTitle(int questID)
        {
            QuestData.Quest quest = Quests != null ? Quests.GetQuest(questID) : null;
            return quest != null ? quest.questTitle : null;
        }

        // 메인 퀘스트(또는 전체)를 모두 완료한다 - 엔딩 직전/이후 상태를 확인할 때
        public void QuestClearAll(bool mainOnly)
        {
            if (!QuestsReady)
            {
                return;
            }

            int completed = 0;
            foreach (QuestData.Quest quest in QuestList)
            {
                if (mainOnly && quest.category != QuestData.Category.Main)
                {
                    continue;
                }

                completed += CompletePrerequisites(quest.questID);
                if (Quests.DebugComplete(quest.questID, giveQuestRewards))
                {
                    ++completed;
                }
            }

            Debug.Log($"[GameTestTools] {(mainOnly ? "메인" : "전체")} 퀘스트를 완료 처리했습니다. (이번에 완료된 퀘스트 {completed}개)");
        }

        // 진행 중(수락함)인 퀘스트의 목표를 전부 채운다
        public void QuestReachAllObjectives()
        {
            if (!QuestsReady)
            {
                return;
            }

            foreach (QuestData.Quest quest in QuestList)
            {
                if (Quests.GetState(quest.questID) == QuestManager.QuestProgressState.Accepted)
                {
                    Quests.DebugGrantCollectItems(quest.questID);
                    Quests.SetProgress(quest.questID, quest.countToComplete);
                }
            }
        }

        // 진행 중이거나 보고만 남은 퀘스트를 전부 완료한다
        public void QuestCompleteAllInProgress()
        {
            if (!QuestsReady)
            {
                return;
            }

            foreach (QuestData.Quest quest in QuestList)
            {
                QuestManager.QuestProgressState state = Quests.GetState(quest.questID);
                if (state == QuestManager.QuestProgressState.Accepted || state == QuestManager.QuestProgressState.ReadyToTurnIn)
                {
                    Quests.DebugComplete(quest.questID, giveQuestRewards, false);
                }
            }
        }

        // 모든 퀘스트를 게임 시작 직후 상태로 되돌린다
        public void QuestResetAll()
        {
            if (QuestsReady)
            {
                Quests.DebugResetAll();
            }
        }

        // ── 현황 ──

        public int CountQuests(QuestManager.QuestProgressState state)
        {
            int count = 0;
            if (QuestsReady)
            {
                foreach (QuestData.Quest quest in QuestList)
                {
                    if (Quests.GetState(quest.questID) == state)
                    {
                        ++count;
                    }
                }
            }

            return count;
        }

        // 선행 퀘스트를 거슬러 올라가며 완료하지 않은 것을 선행 순서대로 완료한다. 이번에 완료한 퀘스트 수를 돌려준다
        private int CompletePrerequisites(int questID)
        {
            List<int> order = new List<int>();
            HashSet<int> visited = new HashSet<int>();
            CollectPrerequisites(questID, questID, visited, order);

            int completed = 0;
            foreach (int id in order)
            {
                if (Quests.DebugComplete(id, giveQuestRewards))
                {
                    ++completed;
                }
            }

            return completed;
        }

        // 깊이 우선으로 선행 퀘스트를 모은다. 가장 깊은 선행이 먼저 담기므로 순서대로 완료하면 된다
        private static void CollectPrerequisites(int rootID, int questID, HashSet<int> visited, List<int> order)
        {
            if (!visited.Add(questID))
            {
                return;
            }

            QuestData.Quest quest = Quests.GetQuest(questID);
            if (quest == null)
            {
                return;
            }

            foreach (int prerequisiteID in quest.prerequisiteQuestIDs)
            {
                CollectPrerequisites(rootID, prerequisiteID, visited, order);
            }

            if (questID != rootID)
            {
                order.Add(questID);
            }
        }
    }
}
