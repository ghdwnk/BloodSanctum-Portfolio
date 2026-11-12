using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// NPC 대화 상태. 켜지는 순간 QuestManager에 "지금 이 NPC와 무엇을 해야 하는지" 묻고 알맞은 대사를 띄운다.
    ///  - 완료 보고 : 보상을 주고 완료 대사. 이어서 줄 퀘스트가 열렸으면 같은 창에서 바로 제안까지 이어 붙인다
    ///  - 퀘스트 제안 : 제안 대사와 수락/거절 버튼
    ///  - 진행 중   : 진행 대사 (대화형 퀘스트는 대화 횟수를 1 올린다)
    ///  - 볼일 없음 : 잡담 (엔딩 뒤에는 후일담)
    ///
    /// 대화하는 동안 카메라를 NPC 쪽으로 당기고(DialogueCameraController), NPC가 플레이어를 바라보게 돌린다.
    /// 플레이어가 멀어지거나 대화창을 닫으면 대기 상태로 돌아간다.
    /// </summary>
    public class NPCTalkState : NPCStateBase
    {
        // 이 퀘스트가 완료돼 있으면 엔딩을 본 뒤다. 잡담 대신 후일담을 들려준다 (UIEndingScreen의 finalQuestID와 같은 값)
        private const int FinalQuestID = 7;
        // 수락 버튼을 누른 뒤 대화창이 닫히기까지의 시간(초)
        private const float AcceptCloseDelay = 0.5f;
        // 대화 중에 플레이어가 멀어졌을 때 대화창을 닫기까지의 시간(초)
        private const float WalkAwayCloseDelay = 3f;

        private QuestManager questManager;

        protected override void OnEnable()
        {
            base.OnEnable();

            // 대화 중에는 "F : 대화하기" 안내가 필요 없다
            manager.HidePrompt();

            // 줌인된 얼굴을 외곽선이 가리지 않도록 대화 중에는 끈다.
            // 대화가 끝나 대기 상태로 돌아가면 NPCIdleState가 거리 판정에 따라 다시 켠다
            manager.SetHighlight(false);

            DialogueCameraController.Focus(refTransform);

            if (questManager == null)
            {
                questManager = QuestManager.Instance;
            }

            // 대사 위에 말하는 사람의 이름을 띄운다
            Dialogue.SetSpeaker(manager.Data.name);

            QuestManager.NPCQuestInteraction interaction = questManager.CheckNPCState(manager.NPCID);
            QuestData.Quest quest = interaction.kind != QuestManager.NPCQuestInteraction.Kind.None
                ? questManager.GetQuest(interaction.questID)
                : null;

            switch (interaction.kind)
            {
                case QuestManager.NPCQuestInteraction.Kind.TurnIn:
                    ShowTurnIn(quest);
                    break;

                case QuestManager.NPCQuestInteraction.Kind.Offer:
                    // 머리 위에 물음표가 떠 있던 NPC와 대화를 시작하는 소리
                    UISound.PlayQuestStart();
                    ShowOffer(quest, null);
                    break;

                case QuestManager.NPCQuestInteraction.Kind.Progress:
                    ShowProgress(quest);
                    break;

                default:
                    ShowSmallTalk();
                    break;
            }
        }

        private void OnDisable()
        {
            // 대화 상태를 벗어나면(대화 종료·거리 이탈) 카메라를 원래대로 돌린다
            DialogueCameraController.Release();
        }

        protected override void Update()
        {
            base.Update();

            // 플레이어가 대화 거리를 벗어나면 잠시 뒤 대화창을 닫고 대기 상태로 돌아간다
            if (!CanTalk())
            {
                Dialogue.CloseDialogueAfterTime(WalkAwayCloseDelay);
                manager.SetState(NPCStateManager.State.Idle);
                return;
            }

            // 플레이어가 대화창을 직접 닫았으면 거리 안이어도 대기 상태로 돌아간다.
            // 이게 없으면 대화가 끝났는데도 카메라가 줌인된 채로 남는다
            if (!Dialogue.IsOpen)
            {
                manager.SetState(NPCStateManager.State.Idle);
                return;
            }

            // 플레이어를 바라본다 (높이 차이는 무시해 고개를 숙이거나 들지 않게 한다)
            Vector3 direction = manager.PlayerTransform.position - refTransform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                refTransform.rotation = Quaternion.LookRotation(direction);
            }
        }

        // ───────────────────────── 대사 종류별 ─────────────────────────

        // 완료 보고: 보상을 주고 완료 대사를 띄운다
        private void ShowTurnIn(QuestData.Quest quest)
        {
            // 느낌표가 떠 있던 NPC와 대화를 시작하는 순간에도 물음표 NPC와 같은 소리를 낸다
            UISound.PlayQuestStart();
            questManager.GrantReward(quest.questID);

            // 이 NPC가 이어서 줄 퀘스트가 방금 열렸으면 완료 대사 뒤에 제안을 바로 이어 붙인다.
            // (끊어 버리면 같은 자리에서 다시 말을 걸어야 해서 번거롭다)
            QuestManager.NPCQuestInteraction next = questManager.CheckNPCState(manager.NPCID);
            if (next.kind == QuestManager.NPCQuestInteraction.Kind.Offer)
            {
                ShowOffer(questManager.GetQuest(next.questID), quest.questCompleteText);
            }
            else
            {
                Dialogue.ShowDialogueText(quest.questCompleteText);
            }
        }

        // 진행 중: 대화형 퀘스트는 대화 횟수를 채우고, 이번 대화로 끝났으면 완료 대사를, 아니면 진행 대사를 띄운다
        private void ShowProgress(QuestData.Quest quest)
        {
            if (quest.type == QuestData.Type.Talk)
            {
                questManager.AdvanceTalkQuest(quest.questID);
            }

            bool justCompleted = questManager.GetState(quest.questID) == QuestManager.QuestProgressState.Completed;
            Dialogue.ShowDialogueText(justCompleted ? quest.questCompleteText : quest.questProgressText);
        }

        // 볼일 없음: 잡담. 엔딩을 본 뒤에는 후일담이 있으면 그것을 들려준다
        private void ShowSmallTalk()
        {
            bool afterEnding = questManager.GetState(FinalQuestID) == QuestManager.QuestProgressState.Completed;
            string afterEndingTalk = manager.Data.postEndingTalk;

            Dialogue.ShowDialogueText(afterEnding && !string.IsNullOrEmpty(afterEndingTalk)
                ? afterEndingTalk
                : manager.Data.idleSmallTalk);
        }

        // 퀘스트 제안 대사와 수락/거절 버튼을 띄운다.
        // leadingText(직전 퀘스트의 완료 대사)가 있으면 같은 창에서 이어 읽히도록 앞에 붙인다.
        // 대사는 '|'로 페이지가 나뉘므로 '|'로 잇기만 하면 페이지가 이어진다
        private void ShowOffer(QuestData.Quest offer, string leadingText)
        {
            int questID = offer.questID;
            string text = string.IsNullOrEmpty(leadingText)
                ? offer.questBeginText
                : $"{leadingText}|{offer.questBeginText}";

            // 수락 처리. 실제로 수락되는 경우에만 소리를 낸다 (이미 수락했거나 열리지 않은 퀘스트면 AcceptQuest가 무시한다)
            void Accept()
            {
                if (questManager.CanAccept(questID))
                {
                    UISound.PlayQuestAccept();
                }

                questManager.AcceptQuest(questID);
            }

            // 버튼 없이 자동으로 수락하는 퀘스트: 대사를 끝까지 읽고 창이 닫히는 순간 수락한다
            if (offer.autoAccept)
            {
                Dialogue.ShowDialogueThen(text, Accept);
                return;
            }

            Dialogue.ShowDialogueChoice(text,
                onAccept: () =>
                {
                    Accept();

                    // 제안 대사에서 이미 할 일을 말했으므로 진행 대사를 또 띄우지 않고 잠깐 뒤 창을 닫는다
                    // (진행 대사는 수락한 뒤 다시 말을 걸었을 때 나온다)
                    Dialogue.CloseDialogueAfterTime(AcceptCloseDelay);
                },
                onDecline: () =>
                {
                    questManager.DeclineQuest(questID);

                    // 거절 대사가 있으면 같은 창에서 보여 주고, 읽고 나면 스페이스바로 닫는다. 없으면 예전처럼 창을 둔다
                    if (!string.IsNullOrEmpty(offer.questDeclineText))
                    {
                        Dialogue.ShowDialogueText(offer.questDeclineText);
                    }
                });
        }
    }
}
