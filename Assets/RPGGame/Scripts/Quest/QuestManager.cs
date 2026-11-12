using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RPGGame
{
    /// <summary>
    /// 모든 퀘스트의 상태와 진행도를 관리하는 관리자. 게임 씬의 매니저 오브젝트에 하나만 둔다.
    ///
    /// 퀘스트 정의는 Resources/Data/Quest Data 에셋(QuestData)에 있고, 이 관리자는 퀘스트마다
    /// "상태(잠김 → 수락 가능 → 진행 중 → 보고 대기 → 완료)"와 "진행도(처치 수 등)"만 따로 기록한다.
    ///
    /// 진행 방식은 퀘스트 종류(QuestData.Type)마다 다르다.
    ///  - Kill(처치)    : 몬스터가 죽을 때 QuestItem이 ProcessQuest를 부른다
    ///  - Collect(수집) : 인벤토리 보유 수가 바뀔 때마다 진행도를 다시 맞춘다
    ///  - Talk(대화)    : NPC와 대화할 때 NPCTalkState가 AdvanceTalkQuest를 부른다
    ///  - Tutorial*     : TutorialQuestTracker가 구매·퀵슬롯 등록·스킬 학습·강화를 감시해 SetProgress를 부른다
    ///
    /// 화면 쪽(퀘스트 창, 우측 추적 HUD, NPC 머리 위 ?/! 표식, 엔딩, 몬스터 정리)은 Subscribe* 함수로 변화를 구독한다.
    /// 세이브는 GameSaveLoader가 GetSaveEntries / LoadState로 주고받는다.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class QuestManager : MonoBehaviour
    {
        /// <summary>퀘스트 상태. 정수 값이 세이브에 저장되므로 순서를 바꾸지 않는다.</summary>
        public enum QuestProgressState { Locked, Available, Accepted, ReadyToTurnIn, Completed }

        /// <summary>NPC에게 말을 걸었을 때 지금 무엇을 해야 하는지 (CheckNPCState의 결과).</summary>
        public struct NPCQuestInteraction
        {
            /// <summary>None: 볼일 없음(잡담) / TurnIn: 완료 보고 / Offer: 퀘스트 제안 / Progress: 진행 중 대사</summary>
            public enum Kind { None, TurnIn, Offer, Progress }

            public Kind kind;
            public int questID;

            public static readonly NPCQuestInteraction None = new() { kind = Kind.None, questID = -1 };
        }

        // 퀘스트 하나의 상태와 진행도
        private class QuestProgress
        {
            public QuestProgressState state;
            public int progressCount;
        }

        // 다른 지역에서 잡았다는 안내를 다시 띄우기까지의 간격(초). 연달아 잡을 때마다 뜨면 방해된다
        private const float WrongZoneNoticeInterval = 6f;

        public static QuestManager Instance { get; private set; }

        // ───── 퀘스트 데이터 (Start에서 한 번 만든다) ─────
        // questID → 퀘스트 정의
        private readonly Dictionary<int, QuestData.Quest> questsByID = new();
        // questID → 상태·진행도
        private readonly Dictionary<int, QuestProgress> questStates = new();
        // NPC ID → 그 NPC가 주거나 보고받는 퀘스트 (메인 먼저, 같으면 questID 순).
        // NPC 표식이 0.3초마다, 상호작용 안내가 매 프레임 조회하므로 조회 때마다 걸러서 정렬하지 않도록 미리 만들어 둔다
        private readonly Dictionary<int, List<QuestData.Quest>> questsByNpc = new();
        // 수집형 퀘스트만 모은 목록. 인벤토리가 바뀔 때마다 훑는다
        private readonly List<QuestData.Quest> collectQuests = new();

        // ───── 변화 알림 (코드에서만 구독하므로 인스펙터에 노출하지 않는다) ─────
        private readonly UnityEvent<int> onQuestAccepted = new();
        private readonly UnityEvent<int> onQuestDeclined = new();
        private readonly UnityEvent<int, int> onQuestProgressChanged = new();
        private readonly UnityEvent<int> onQuestReadyToTurnIn = new();
        private readonly UnityEvent<int> onQuestCompleted = new();
        private readonly UnityEvent<int> onQuestUnlocked = new();
        // 테스트 도구가 퀘스트를 처음 상태로 되돌렸을 때. 퀘스트 창·추적 HUD가 목록을 다시 맞춘다
        private readonly UnityEvent<int> onQuestReset = new();
        // 세이브에서 모든 퀘스트 상태를 한꺼번에 되살린 직후. 퀘스트 창·추적 HUD가 목록을 처음부터 다시 맞춘다
        private readonly UnityEvent onStateLoaded = new();

        private float nextWrongZoneNoticeTime;

        public void SubscribeOnQuestAccepted(UnityAction<int> listener) => onQuestAccepted.AddListener(listener);
        public void SubscribeOnQuestDeclined(UnityAction<int> listener) => onQuestDeclined.AddListener(listener);
        public void SubscribeOnQuestProgressChanged(UnityAction<int, int> listener) => onQuestProgressChanged.AddListener(listener);
        public void SubscribeOnQuestReadyToTurnIn(UnityAction<int> listener) => onQuestReadyToTurnIn.AddListener(listener);
        public void SubscribeOnQuestCompleted(UnityAction<int> listener) => onQuestCompleted.AddListener(listener);
        public void SubscribeOnQuestUnlocked(UnityAction<int> listener) => onQuestUnlocked.AddListener(listener);
        public void SubscribeOnQuestReset(UnityAction<int> listener) => onQuestReset.AddListener(listener);
        public void SubscribeOnStateLoaded(UnityAction listener) => onStateLoaded.AddListener(listener);

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // 실행 순서가 -50이라 GameSaveLoader(기본 순서)의 Start보다 먼저 돈다.
        // 그래서 세이브를 불러올 때(LoadState)는 아래 목록이 이미 만들어져 있다
        private void Start()
        {
            BuildQuestTables();

            // 수집형 퀘스트는 처치 이벤트가 아니라 인벤토리 보유 수로 진행도를 계산한다.
            // 드랍 획득·상점 판매·세이브 불러오기 모두 이 이벤트 하나로 잡힌다
            InventoryManager.Instance?.SubscribeOnItemListChanged(RefreshCollectProgress);

            // 튜토리얼 퀘스트의 행동(구매·등록·학습·강화)을 감시하는 컴포넌트. 씬에 따로 배선하지 않아도 되게 코드로 붙인다
            if (GetComponent<TutorialQuestTracker>() == null)
            {
                gameObject.AddComponent<TutorialQuestTracker>();
            }
        }

        // 퀘스트 정의를 읽어 조회용 표와 초기 상태를 만든다
        private void BuildQuestTables()
        {
            foreach (QuestData.Quest quest in DataManager.Instance.questData.quests)
            {
                questsByID[quest.questID] = quest;
                questStates[quest.questID] = new QuestProgress
                {
                    state = quest.prerequisiteQuestIDs.Count == 0 ? QuestProgressState.Available : QuestProgressState.Locked,
                    progressCount = 0
                };

                AddToNpcTable(quest.giverNpcID, quest);
                if (quest.turnInNpcID != quest.giverNpcID)
                {
                    AddToNpcTable(quest.turnInNpcID, quest);
                }

                if (quest.type == QuestData.Type.Collect && quest.collectItem != null)
                {
                    collectQuests.Add(quest);
                }
            }

            // 같은 NPC에 퀘스트가 여럿이면 메인을 서브보다, questID가 작은 쪽을 먼저 고른다
            foreach (List<QuestData.Quest> list in questsByNpc.Values)
            {
                list.Sort((a, b) => a.category != b.category
                    ? a.category.CompareTo(b.category)
                    : a.questID.CompareTo(b.questID));
            }
        }

        private void AddToNpcTable(int npcID, QuestData.Quest quest)
        {
            if (!questsByNpc.TryGetValue(npcID, out List<QuestData.Quest> list))
            {
                list = new List<QuestData.Quest>();
                questsByNpc.Add(npcID, list);
            }

            list.Add(quest);
        }

        // ───────────────────────── 조회 ─────────────────────────

        /// <summary>퀘스트 상태. 없는 ID면 Locked.</summary>
        public QuestProgressState GetState(int questID)
        {
            return questStates.TryGetValue(questID, out QuestProgress progress) ? progress.state : QuestProgressState.Locked;
        }

        /// <summary>현재 진행도 (처치 수·모은 수 등).</summary>
        public int GetProgress(int questID)
        {
            return questStates.TryGetValue(questID, out QuestProgress progress) ? progress.progressCount : 0;
        }

        /// <summary>퀘스트 정의. 없는 ID면 null.</summary>
        public QuestData.Quest GetQuest(int questID)
        {
            return questsByID.TryGetValue(questID, out QuestData.Quest quest) ? quest : null;
        }

        /// <summary>모든 퀘스트 정의 (읽기 전용). 텔레포트 목록처럼 퀘스트를 훑어보는 화면이 쓴다.</summary>
        public IEnumerable<QuestData.Quest> AllQuests => questsByID.Values;

        public bool CanAccept(int questID) => GetState(questID) == QuestProgressState.Available;

        /// <summary>
        /// 이 NPC에게 말을 걸었을 때 할 일. 우선순위는 완료 보고 > 퀘스트 제안 > 진행 중 대사 순이다.
        /// NPC 머리 위 표식(NPCQuestMarker), 상호작용 안내 문구, 대화(NPCTalkState)가 함께 쓴다.
        /// </summary>
        public NPCQuestInteraction CheckNPCState(int npcID)
        {
            if (!questsByNpc.TryGetValue(npcID, out List<QuestData.Quest> relevant))
            {
                return NPCQuestInteraction.None;
            }

            foreach (QuestData.Quest quest in relevant)
            {
                if (quest.turnInNpcID == npcID && GetState(quest.questID) == QuestProgressState.ReadyToTurnIn)
                {
                    return new NPCQuestInteraction { kind = NPCQuestInteraction.Kind.TurnIn, questID = quest.questID };
                }
            }

            foreach (QuestData.Quest quest in relevant)
            {
                if (quest.giverNpcID == npcID && GetState(quest.questID) == QuestProgressState.Available)
                {
                    return new NPCQuestInteraction { kind = NPCQuestInteraction.Kind.Offer, questID = quest.questID };
                }
            }

            foreach (QuestData.Quest quest in relevant)
            {
                if (GetState(quest.questID) == QuestProgressState.Accepted)
                {
                    return new NPCQuestInteraction { kind = NPCQuestInteraction.Kind.Progress, questID = quest.questID };
                }
            }

            return NPCQuestInteraction.None;
        }

        // ───────────────────────── 수락 / 거절 ─────────────────────────

        /// <summary>
        /// 퀘스트를 수락한다 (대화의 수락 버튼). 처치형·수집형이면 대상 몬스터를 바로 생성한다.
        /// 수락할 수 없는 상태면 아무것도 하지 않는다.
        /// </summary>
        public void AcceptQuest(int questID)
        {
            if (!CanAccept(questID))
            {
                return;
            }

            QuestData.Quest quest = GetQuest(questID);
            if (quest == null)
            {
                return;
            }

            QuestProgress progress = questStates[questID];
            progress.state = QuestProgressState.Accepted;
            progress.progressCount = 0;

            SpawnQuestMonsters(quest);

            // 수락과 함께 주는 아이템 (최종 퀘스트의 블러드스톤). 수락 경로가 NPC 대화든 테스트 도구든 빠지지 않게 여기서 준다
            if (quest.acceptItem != null && quest.acceptItemCount > 0)
            {
                InventoryManager.Instance?.AddItem(quest.acceptItem, quest.acceptItemCount);
                UIPickupFeed.ShowItem(quest.acceptItem, quest.acceptItemCount);
            }

            onQuestAccepted.Invoke(questID);

            // 수락 전에 이미 모아 둔 아이템이 있으면 바로 인정한다
            RefreshCollectProgress();
        }

        /// <summary>퀘스트를 거절한다. 상태는 Available 그대로라 다음에 다시 제안받는다.</summary>
        public void DeclineQuest(int questID)
        {
            onQuestDeclined.Invoke(questID);
        }

        // ───────────────────────── 진행 ─────────────────────────

        /// <summary>
        /// 몬스터를 처치하는 등 퀘스트 목표 사건이 일어났을 때 QuestItem이 부른다.
        /// 조건이 맞는 '진행 중' 퀘스트 모두에 진행도를 1씩 더한다 (메인·서브가 같은 대상을 동시에 추적할 수 있다).
        /// </summary>
        /// <param name="position">사건이 일어난 위치 (몬스터가 쓰러진 곳). 퀘스트가 지역을 지정했으면 그 지역 안이어야 인정한다</param>
        public void ProcessQuest(QuestData.Type type, QuestData.TargetType targetType, Vector3 position)
        {
            foreach (KeyValuePair<int, QuestProgress> pair in questStates)
            {
                QuestProgress progress = pair.Value;
                if (progress.state != QuestProgressState.Accepted)
                {
                    continue;
                }

                QuestData.Quest quest = GetQuest(pair.Key);
                if (quest == null || quest.type != type || !IsTargetMatched(quest.targetType, targetType))
                {
                    continue;
                }

                if (!IsInQuestZone(quest, position))
                {
                    NotifyWrongZone(quest);
                    continue;
                }

                progress.progressCount = Mathf.Min(progress.progressCount + 1, quest.countToComplete);
                onQuestProgressChanged.Invoke(pair.Key, progress.progressCount);

                if (progress.progressCount >= quest.countToComplete)
                {
                    MarkReadyToTurnIn(pair.Key, progress);
                }
            }
        }

        /// <summary>
        /// 진행도를 직접 정한다 (수집형·튜토리얼). 진행도는 줄어들지 않고,
        /// 목표에 닿으면 처치형과 같이 보고 대기 상태가 된다.
        /// </summary>
        public void SetProgress(int questID, int count)
        {
            if (!questStates.TryGetValue(questID, out QuestProgress progress) || progress.state != QuestProgressState.Accepted)
            {
                return;
            }

            QuestData.Quest quest = GetQuest(questID);
            if (quest == null)
            {
                return;
            }

            int clamped = Mathf.Min(count, quest.countToComplete);
            if (clamped <= progress.progressCount)
            {
                return;
            }

            progress.progressCount = clamped;
            onQuestProgressChanged.Invoke(questID, progress.progressCount);

            if (progress.progressCount >= quest.countToComplete)
            {
                MarkReadyToTurnIn(questID, progress);
            }
        }

        /// <summary>
        /// 대화형(Talk) 퀘스트 전용. NPC와 대화할 때마다 진행도를 1 올린다.
        /// 대화형은 주는 NPC와 보고받는 NPC가 같아서, 목표를 채우면 다시 찾아갈 필요 없이 바로 보상까지 준다.
        /// </summary>
        public void AdvanceTalkQuest(int questID)
        {
            if (!questStates.TryGetValue(questID, out QuestProgress progress) || progress.state != QuestProgressState.Accepted)
            {
                return;
            }

            QuestData.Quest quest = GetQuest(questID);
            if (quest == null || quest.type != QuestData.Type.Talk)
            {
                return;
            }

            progress.progressCount = Mathf.Min(progress.progressCount + 1, quest.countToComplete);
            onQuestProgressChanged.Invoke(questID, progress.progressCount);

            if (progress.progressCount >= quest.countToComplete)
            {
                GrantReward(questID);
            }
        }

        // 진행 중인 수집형 퀘스트의 진행도를 인벤토리 보유 수에 맞춘다.
        // SetProgress는 진행도를 줄이지 않으므로, 모은 뒤 팔거나 버려도 이미 채운 진행은 유지된다
        private void RefreshCollectProgress()
        {
            if (InventoryManager.Instance == null)
            {
                return;
            }

            foreach (QuestData.Quest quest in collectQuests)
            {
                if (GetState(quest.questID) == QuestProgressState.Accepted)
                {
                    SetProgress(quest.questID, InventoryManager.Instance.GetItemCount(quest.collectItem));
                }
            }
        }

        // 목표를 모두 채워 보고만 남은 상태로 바꾼다. 이 순간 우측 퀘스트 추적 HUD가 빛나기 시작하므로
        // 퀘스트 완료음도 여기서 낸다 (NPC에게 보고하는 순간이 아니다)
        private void MarkReadyToTurnIn(int questID, QuestProgress progress)
        {
            progress.state = QuestProgressState.ReadyToTurnIn;
            onQuestReadyToTurnIn.Invoke(questID);
            UISound.PlayQuestClear();
        }

        // ───────────────────────── 처치 대상 판정 ─────────────────────────

        // 처치 대상이 보스인지. 등록된 EnemyData의 종류로 판단한다
        private static bool IsBossTarget(QuestData.TargetType targetType)
        {
            return DataManager.Instance.TryGetEnemyData(targetType.ToString(), out EnemyData data)
                   && data.enemyType == EnemyData.EnemyType.Boss;
        }

        // 처치 대상이 몬스터인지. EnemyData 등록 여부로 판단하므로 몬스터가 추가돼도 여기를 고칠 필요가 없다
        private static bool IsMonsterTarget(QuestData.TargetType target)
        {
            return DataManager.Instance.TryGetEnemyData(target.ToString(), out _);
        }

        // 잡은 몬스터가 퀘스트 목표로 인정되는지.
        //
        // 일반 몬스터 퀘스트("마을 외곽 몬스터 처치" 등)는 목표 문구가 종류를 지정하지 않고 수만 요구하므로
        // 어떤 일반 몬스터를 잡아도 진행되게 한다. (종류가 정확히 같아야만 인정하면, 뱀파이어 퀘스트 중에
        // 스켈레톤을 잡았을 때 아무 반응이 없어 "어떤 건 오르고 어떤 건 안 오른다"로만 보인다)
        //
        // 보스 퀘스트는 반드시 그 보스를 잡아야 한다. 보스 퀘스트는 목표 수가 1이라,
        // 여기까지 열어 두면 잡몹 한 마리로 최종 보스 퀘스트가 끝나 버린다.
        private static bool IsTargetMatched(QuestData.TargetType questTarget, QuestData.TargetType killed)
        {
            if (questTarget == killed)
            {
                return true;
            }

            if (IsBossTarget(questTarget))
            {
                return false;
            }

            return IsMonsterTarget(questTarget) && IsMonsterTarget(killed);
        }

        // 퀘스트가 지역(ZoneArea 이름)을 지정했으면 그 지역 안에서 일어난 사건만 인정한다. 비어 있으면 어디서든 인정
        private static bool IsInQuestZone(QuestData.Quest quest, Vector3 position)
        {
            return string.IsNullOrEmpty(quest.zoneName) || ZoneTracker.GetZoneNameAt(position) == quest.zoneName;
        }

        // 다른 지역에서 잡았을 때 아무 반응이 없으면 버그처럼 보이므로 이유를 알려 준다 (일정 간격으로만)
        private void NotifyWrongZone(QuestData.Quest quest)
        {
            if (Time.time < nextWrongZoneNoticeTime)
            {
                return;
            }

            nextWrongZoneNoticeTime = Time.time + WrongZoneNoticeInterval;
            UISystemMessage.ShowToastWarning($"'{quest.zoneName}'에서 처치해야 퀘스트가 진행됩니다");
        }

        // ───────────────────────── 완료 ─────────────────────────

        /// <summary>
        /// 완료 보고를 받아 보상을 주고 퀘스트를 완료한다 (보고 NPC와 대화할 때 NPCTalkState가 부른다).
        /// 수집형은 보상을 주기 전에 모은 아이템을 가져가고, 모자라면 진행 중 상태로 되돌린다.
        /// </summary>
        public void GrantReward(int questID)
        {
            if (!questStates.TryGetValue(questID, out QuestProgress progress))
            {
                return;
            }

            QuestData.Quest quest = GetQuest(questID);
            if (quest == null)
            {
                return;
            }

            // 목표를 채운 순간(ReadyToTurnIn) 완료음을 이미 냈으면 보고할 때 또 내지 않는다.
            // 보고 대기 단계 없이 대화로 바로 끝나는 Talk형만 여기서 낸다
            bool alreadyAnnounced = progress.state == QuestProgressState.ReadyToTurnIn;

            // 수집 재료는 보고할 때 넘기는 것이므로 보상 전에 가져간다.
            // 진행도는 줄지 않아서, 모은 뒤 아이템을 팔았다면 개수가 모자랄 수 있다. 그땐 아무것도 가져가지 않고 진행 중으로 되돌린다
            if (quest.type == QuestData.Type.Collect && quest.collectItem != null)
            {
                InventoryManager inventory = InventoryManager.Instance;
                if (inventory == null || !inventory.RemoveItem(quest.collectItem, quest.countToComplete))
                {
                    UISystemMessage.ShowToastWarning($"{quest.collectItem.itemName}이(가) 부족합니다");
                    progress.state = QuestProgressState.Accepted;
                    progress.progressCount = inventory != null
                        ? Mathf.Min(inventory.GetItemCount(quest.collectItem), quest.countToComplete)
                        : 0;
                    onQuestProgressChanged.Invoke(questID, progress.progressCount);
                    return;
                }
            }

            GiveRewards(quest);
            MarkCompleted(questID, progress, !alreadyAnnounced);
        }

        // 보상(골드·경험치·아이템)을 준다. 완료 보고와 테스트 도구의 강제 완료가 함께 쓴다
        private void GiveRewards(QuestData.Quest quest)
        {
            if (quest.rewardGold > 0)
            {
                CurrencyManager.Instance?.AddGold(quest.rewardGold);
                UIPickupFeed.ShowGold(quest.rewardGold);
            }

            if (quest.exp > 0f)
            {
                // 퀘스트를 완료할 때만 부르므로 캐시하지 않는다
                FindAnyObjectByType<PlayerLevelController>()?.GainExp(quest.exp);
            }

            // 보상 아이템이 없는 퀘스트는 rewardItem이 비어 있어 건너뛴다
            if (quest.rewardItem != null && quest.rewardItemCount > 0)
            {
                InventoryManager.Instance?.AddItem(quest.rewardItem, quest.rewardItemCount);
                UIPickupFeed.ShowItem(quest.rewardItem, quest.rewardItemCount);
            }
        }

        // 완료 상태로 바꾸고, 완료 알림과 뒤 퀘스트 잠금 해제까지 처리한다.
        // announce가 false면 완료음을 내지 않는다 (이미 냈거나, 여러 개를 한꺼번에 완료할 때 소리가 겹치지 않게)
        private void MarkCompleted(int questID, QuestProgress progress, bool announce)
        {
            progress.state = QuestProgressState.Completed;
            onQuestCompleted.Invoke(questID);

            if (announce)
            {
                UISound.PlayQuestClear();
            }

            RefreshUnlocks();
        }

        // 선행 퀘스트를 모두 끝낸 잠금 퀘스트를 수락 가능으로 연다
        private void RefreshUnlocks()
        {
            foreach (KeyValuePair<int, QuestProgress> pair in questStates)
            {
                if (pair.Value.state != QuestProgressState.Locked)
                {
                    continue;
                }

                QuestData.Quest lockedQuest = GetQuest(pair.Key);
                if (lockedQuest != null && ArePrerequisitesCompleted(lockedQuest))
                {
                    pair.Value.state = QuestProgressState.Available;
                    onQuestUnlocked.Invoke(pair.Key);
                }
            }
        }

        private bool ArePrerequisitesCompleted(QuestData.Quest quest)
        {
            foreach (int prerequisiteID in quest.prerequisiteQuestIDs)
            {
                if (GetState(prerequisiteID) != QuestProgressState.Completed)
                {
                    return false;
                }
            }

            return true;
        }

        // ───────────────────────── 세이브 ─────────────────────────

        /// <summary>세이브용으로 모든 퀘스트의 상태·진행도를 목록으로 만든다.</summary>
        public List<QuestSaveEntry> GetSaveEntries()
        {
            List<QuestSaveEntry> entries = new(questStates.Count);
            foreach (KeyValuePair<int, QuestProgress> pair in questStates)
            {
                entries.Add(new QuestSaveEntry
                {
                    questID = pair.Key,
                    state = (int)pair.Value.state,
                    progressCount = pair.Value.progressCount
                });
            }

            return entries;
        }

        /// <summary>
        /// 세이브에서 퀘스트 상태를 되살린다. 게임 시작 때 GameSaveLoader가 한 번 부르는 불러오기 전용이라
        /// 변화 알림(수락·진행·완료)은 보내지 않는다.
        /// </summary>
        public void LoadState(List<QuestSaveEntry> entries)
        {
            if (entries == null)
            {
                return;
            }

            HashSet<int> savedIDs = new();

            foreach (QuestSaveEntry entry in entries)
            {
                savedIDs.Add(entry.questID);
                questStates[entry.questID] = new QuestProgress
                {
                    state = (QuestProgressState)entry.state,
                    progressCount = entry.progressCount
                };
            }

            CompleteSkippedQuests(savedIDs);
            RefreshUnlocks();

            // 수락한 채로 저장된 퀘스트는 대상 몬스터가 다시 나타나야 진행할 수 있다 (몬스터는 저장하지 않는다)
            foreach (QuestData.Quest quest in questsByID.Values)
            {
                if (GetState(quest.questID) == QuestProgressState.Accepted && MonsterSpawner.Exists)
                {
                    SpawnQuestMonsters(quest);
                }
            }

            // 불러오기는 개별 알림(수락·진행·완료)을 보내지 않으므로, 화면에는 이 알림으로 한꺼번에 알린다
            onStateLoaded.Invoke();
        }

        // 처치·수집 퀘스트의 대상 몬스터를 만든다. 퀘스트를 수락할 때와, 수락한 채로 저장된 세이브를 불러올 때 함께 쓴다.
        // 불러올 때 부르지 않으면 보스 퀘스트의 보스가 나타나지 않아 퀘스트를 진행할 수 없다
        private void SpawnQuestMonsters(QuestData.Quest quest)
        {
            // 수집형도 아이템을 몬스터가 떨어뜨리므로 처치형과 똑같이 대상 몬스터를 생성한다.
            // 특정 몬스터 이름으로 분기하지 않고 "보스인지 아닌지"로만 나눠서, 몬스터를 추가해도 여기를 고칠 필요가 없다
            bool spawnsMonsters = quest.type == QuestData.Type.Kill || quest.type == QuestData.Type.Collect;
            if (!spawnsMonsters)
            {
                return;
            }

            string enemyId = quest.targetType.ToString();

            if (IsBossTarget(quest.targetType))
            {
                MonsterSpawner.SpawnBoss(enemyId, quest.monsterLevel);
            }
            else
            {
                // 스폰 지점이 넓게 퍼져 있어 목표 수만 만들면 다 찾기 어려우므로 스포너가 넉넉하게 만든다
                MonsterSpawner.SpawnForQuest(quest.countToComplete, quest.monsterLevel, enemyId);
            }
        }

        // 세이브를 만든 뒤에 중간에 끼워 넣은 퀘스트(튜토리얼 등)는 세이브에 기록이 없다.
        // 그 퀘스트의 뒤 단계가 이미 시작됐다면 건너뛴 것으로 보고 완료 처리한다.
        // 그러지 않으면 잠금 상태로 영영 남고, 그 퀘스트를 선행으로 가진 퀘스트까지 막힌다.
        // 끼워 넣은 퀘스트가 연달아 있으면(8 → 9 → 2) 앞 단계도 줄줄이 건너뛰어야 하므로 더 바뀔 게 없을 때까지 반복한다
        private void CompleteSkippedQuests(HashSet<int> savedIDs)
        {
            bool changed = true;
            while (changed)
            {
                changed = false;

                foreach (QuestData.Quest quest in questsByID.Values)
                {
                    if (savedIDs.Contains(quest.questID) || GetState(quest.questID) == QuestProgressState.Completed)
                    {
                        continue;
                    }

                    if (IsAnyFollowUpStarted(quest.questID))
                    {
                        QuestProgress progress = questStates[quest.questID];
                        progress.state = QuestProgressState.Completed;
                        progress.progressCount = quest.countToComplete;
                        changed = true;
                    }
                }
            }
        }

        // 이 퀘스트를 선행으로 가진 퀘스트 중 하나라도 잠금이 풀렸는지
        private bool IsAnyFollowUpStarted(int questID)
        {
            foreach (QuestData.Quest other in questsByID.Values)
            {
                if (other.prerequisiteQuestIDs.Contains(questID) && GetState(other.questID) != QuestProgressState.Locked)
                {
                    return true;
                }
            }

            return false;
        }

        // ───────────────────────── 테스트 도구용 (GameTestTools) ─────────────────────────

        /// <summary>
        /// 퀘스트를 대화 없이 곧바로 완료한다. 진행도는 가득 채운다.
        /// giveRewards가 true면 보상(골드·경험치·아이템)도 준다. 수집형이면 목표 수만큼 수집 재료를 인벤토리에 채워 주고(DebugGrantCollectItems), 가져가지는 않는다.
        /// </summary>
        public bool DebugComplete(int questID, bool giveRewards, bool announce = false)
        {
            if (!questStates.TryGetValue(questID, out QuestProgress progress) || progress.state == QuestProgressState.Completed)
            {
                return false;
            }

            QuestData.Quest quest = GetQuest(questID);
            if (quest == null)
            {
                return false;
            }

            // 이미 완료음을 낸 보고 대기 퀘스트는 소리를 다시 내지 않는다
            bool alreadyAnnounced = progress.state == QuestProgressState.ReadyToTurnIn;

            progress.progressCount = quest.countToComplete;

            if (giveRewards)
            {
                GiveRewards(quest);
            }

            MarkCompleted(questID, progress, announce && !alreadyAnnounced);

            // 완료 상태가 된 뒤에 주므로, 아이템 목록 변화가 진행도를 건드려 완료음을 또 내지 않는다
            DebugGrantCollectItems(questID);
            return true;
        }

        /// <summary>
        /// 테스트 도구용. 수집형 퀘스트의 재료(블러드스톤 조각 등)가 목표 수보다 모자라면 모자란 만큼 인벤토리에 채워 준다.
        /// 강제로 목표를 채우거나 완료해도 실제 아이템은 생기지 않아, 그 뒤 완료 보고(재료 제출)나 이후 진행이 막히는 것을 막는다.
        /// 이미 충분히 갖고 있으면 아무것도 하지 않으므로 여러 번 불러도 쌓이지 않는다
        /// </summary>
        public void DebugGrantCollectItems(int questID)
        {
            QuestData.Quest quest = GetQuest(questID);
            InventoryManager inventory = InventoryManager.Instance;
            if (quest == null || inventory == null || quest.type != QuestData.Type.Collect || quest.collectItem == null)
            {
                return;
            }

            int missing = quest.countToComplete - inventory.GetItemCount(quest.collectItem);
            if (missing > 0)
            {
                inventory.AddItem(quest.collectItem, missing);
                UIPickupFeed.ShowItem(quest.collectItem, missing);
            }
        }

        /// <summary>
        /// 퀘스트를 처음 상태로 되돌린다 (선행이 모두 완료돼 있으면 수락 가능, 아니면 잠김).
        /// 이미 받은 보상은 회수하지 않는다.
        /// </summary>
        public void DebugReset(int questID)
        {
            if (!questStates.TryGetValue(questID, out QuestProgress progress))
            {
                return;
            }

            QuestData.Quest quest = GetQuest(questID);
            if (quest == null)
            {
                return;
            }

            progress.progressCount = 0;
            progress.state = ArePrerequisitesCompleted(quest) ? QuestProgressState.Available : QuestProgressState.Locked;

            onQuestReset.Invoke(questID);
        }

        /// <summary>모든 퀘스트를 게임 시작 직후 상태로 되돌린다 (선행이 없는 퀘스트만 수락 가능).</summary>
        public void DebugResetAll()
        {
            foreach (QuestData.Quest quest in questsByID.Values)
            {
                QuestProgress progress = questStates[quest.questID];
                progress.progressCount = 0;
                progress.state = quest.prerequisiteQuestIDs.Count == 0 ? QuestProgressState.Available : QuestProgressState.Locked;
            }

            foreach (int questID in questsByID.Keys)
            {
                onQuestReset.Invoke(questID);
            }
        }
    }
}
