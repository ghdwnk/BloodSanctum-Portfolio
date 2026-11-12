using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 튜토리얼 퀘스트 3종의 진행을 감시한다.
    ///  - TutorialPotion  : 1단계 물약 구매 → 2단계 물약을 퀵슬롯에 등록
    ///  - TutorialSkill   : 1단계 스킬 습득 → 2단계 배운 스킬을 퀵슬롯에 등록 (수락할 때 스킬 포인트 1개 지급)
    ///  - TutorialEnhance : 장비 강화 1회 시도 (수락할 때 첫 강화에 모자란 재료·골드 지급)
    ///
    /// 처치형과 달리 몬스터가 아니라 플레이어의 행동으로 진행되므로, 상점·퀵슬롯·스킬·강화 쪽에서
    /// 알려 주는 이벤트를 듣고 QuestManager.SetProgress로 진행도를 올린다.
    /// "스킬을 배웠는가"처럼 현재 상태로 확인할 수 있는 단계는 상태를 직접 검사한다 (받기 전에 이미 배워 뒀어도 막히지 않게).
    /// 단, 퀵슬롯 등록 단계는 이미 등록돼 있어도 인정하지 않고, 퀘스트를 받은 뒤 직접 등록하는 순간(슬롯 변경 이벤트)만 인정한다.
    ///
    /// 씬에 따로 두지 않는다. QuestManager가 Start에서 자기 오브젝트에 붙인다.
    /// </summary>
    public class TutorialQuestTracker : MonoBehaviour
    {
        // "스킬의 기초" 수락 때 주는 스킬 포인트. 레벨이 낮아 포인트가 없어도 퀘스트가 막히지 않게 한다
        private const int TutorialSkillBonusPoints = 1;

        private QuestManager questManager;
        private QuickSlotController quickSlot;
        private PlayerSkillController skillController;
        private PlayerStats playerStats;

        private void Start()
        {
            questManager = QuestManager.Instance;
            if (questManager == null)
            {
                enabled = false;
                return;
            }

            questManager.SubscribeOnQuestAccepted(OnQuestAccepted);
            ShopController.OnItemPurchased += OnItemPurchased;
            EnhancementManager.OnEnhanceAttempted += OnEnhanceAttempted;

            PlayerStateManager player = FindAnyObjectByType<PlayerStateManager>();
            if (player != null)
            {
                quickSlot = player.GetComponent<QuickSlotController>();
                skillController = player.GetComponent<PlayerSkillController>();
                playerStats = player.GetComponent<PlayerStats>();
            }

            quickSlot?.SubscribeOnSlotChanged(OnQuickSlotChanged);
            skillController?.SubscribeOnSkillUnlockChanged(EvaluateAll);
        }

        private void OnDestroy()
        {
            ShopController.OnItemPurchased -= OnItemPurchased;
            EnhancementManager.OnEnhanceAttempted -= OnEnhanceAttempted;
        }

        // 수락은 퀘스트당 한 번만 일어나므로(Available에서만 수락 가능) 여기서 주는 보너스는 중복 지급되지 않는다
        private void OnQuestAccepted(int questID)
        {
            QuestData.Quest quest = questManager.GetQuest(questID);
            if (quest != null)
            {
                switch (quest.type)
                {
                    case QuestData.Type.TutorialSkill:
                        GrantSkillBonus();
                        break;
                    case QuestData.Type.TutorialEnhance:
                        GrantEnhanceStarterMaterials();
                        break;
                }
            }

            EvaluateAll();
        }

        private void GrantSkillBonus()
        {
            if (playerStats == null)
            {
                return;
            }

            playerStats.GrantSkillPoints(TutorialSkillBonusPoints);
            UISystemMessage.ShowToast($"스킬 포인트 {TutorialSkillBonusPoints}개를 받았습니다");
        }

        // 첫 강화(+0 → +1)에 드는 재료와 골드 중 모자란 만큼만 채워 준다.
        // 이미 갖고 있으면 더 주지 않아서 퀘스트를 반복해도 재화가 불어나지 않는다
        private void GrantEnhanceStarterMaterials()
        {
            EnhancementData data = EnhancementManager.Instance != null ? EnhancementManager.Instance.Data : null;
            EnhancementData.Step step = data != null ? data.GetStep(0) : null;
            if (step == null)
            {
                return;
            }

            if (data.materialItem != null && InventoryManager.Instance != null)
            {
                int shortage = step.materialCount - InventoryManager.Instance.GetItemCount(data.materialItem);
                if (shortage > 0)
                {
                    InventoryManager.Instance.AddItem(data.materialItem, shortage);
                    UIPickupFeed.ShowItem(data.materialItem, shortage);
                }
            }

            if (CurrencyManager.Instance != null)
            {
                int shortage = step.goldCost - CurrencyManager.Instance.Gold;
                if (shortage > 0)
                {
                    CurrencyManager.Instance.AddGold(shortage);
                    UIPickupFeed.ShowGold(shortage);
                }
            }
        }

        // 물약을 사면 1단계를 채운다. 구매는 상태로 확인할 수 없는 행동이라 이벤트로만 받는다
        private void OnItemPurchased(Item item)
        {
            if (item is HealthItem)
            {
                ForEachAccepted(QuestData.Type.TutorialPotion, quest => questManager.SetProgress(quest.questID, 1));
            }
        }

        // 강화는 성공/실패와 상관없이 한 번 시도하면 인정한다 (재료와 골드가 실제로 소모되므로)
        private void OnEnhanceAttempted(EquipmentInstance item, bool success)
        {
            ForEachAccepted(QuestData.Type.TutorialEnhance, quest => questManager.SetProgress(quest.questID, quest.countToComplete));
        }

        // 현재 상태로 확인할 수 있는 단계를 다시 검사한다 (수락 직후, 스킬을 배우거나 잊었을 때)
        private void EvaluateAll()
        {
            if (questManager == null)
            {
                return;
            }

            // 스킬: 하나라도 배웠으면 1단계 (2단계 등록은 OnQuickSlotChanged에서 처리)
            ForEachAccepted(QuestData.Type.TutorialSkill, quest =>
            {
                if (HasLearnedSkill())
                {
                    questManager.SetProgress(quest.questID, 1);
                }
            });
        }

        // 퀘스트를 받은 뒤 플레이어가 단축키 칸을 바꾼 순간에만 등록 단계를 올린다.
        // 현재 슬롯 상태를 검사하면 미리 올려 둔 물약/스킬 때문에 수락하자마자 완료돼 버린다
        private void OnQuickSlotChanged(int index)
        {
            QuickSlotController.SlotEntry slot = quickSlot != null ? quickSlot.GetSlot(index) : null;
            if (slot == null || slot.IsEmpty)
            {
                return;
            }

            // 물약: 1단계(구매)가 끝난 뒤 물약을 단축키에 올리면 2단계
            if (slot.item is HealthItem)
            {
                ForEachAccepted(QuestData.Type.TutorialPotion, quest =>
                {
                    if (questManager.GetProgress(quest.questID) >= 1)
                    {
                        questManager.SetProgress(quest.questID, 2);
                    }
                });
            }

            // 스킬: 1단계(습득)가 끝난 뒤 배운 스킬을 단축키에 올리면 2단계
            if (slot.skill != null && skillController != null && skillController.IsSkillUnlocked(slot.skill))
            {
                ForEachAccepted(QuestData.Type.TutorialSkill, quest =>
                {
                    if (questManager.GetProgress(quest.questID) >= 1)
                    {
                        questManager.SetProgress(quest.questID, 2);
                    }
                });
            }
        }

        // 해당 종류의 진행 중인 퀘스트마다 action을 실행한다 (보통 하나뿐이다)
        private void ForEachAccepted(QuestData.Type type, System.Action<QuestData.Quest> action)
        {
            QuestData data = DataManager.Instance != null ? DataManager.Instance.questData : null;
            if (data == null)
            {
                return;
            }

            foreach (QuestData.Quest quest in data.quests)
            {
                if (quest.type == type && questManager.GetState(quest.questID) == QuestManager.QuestProgressState.Accepted)
                {
                    action(quest);
                }
            }
        }

        private bool HasLearnedSkill()
        {
            if (skillController == null)
            {
                return false;
            }

            // 퀵슬롯 등록 후보가 되는 스킬 목록은 컨트롤러가 가진 스킬 슬롯이다
            for (int ix = 0; ix < skillController.SkillCount; ++ix)
            {
                SkillData skill = skillController.GetSkill(ix);
                if (skill != null && skillController.IsSkillUnlocked(skill))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
