using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 텔레포트로 갈 수 있는 모든 장소(마을 + 던전 5곳)의 목록을 한곳에서 관리한다.
    ///
    /// 각 텔레포트 포털(TeleportPortal)은 자기가 어느 장소에 있는지(locationId)만 알고,
    /// 이동 목록 창(UITeleportWindow)은 이 목록에서 "지금 있는 장소"를 뺀 나머지를 보여 준다.
    /// 그래서 마을에서는 던전 5곳이, 던전에서는 마을 + 나머지 던전 4곳이 뜬다.
    /// 도착 지점은 Transform으로 두어 씬에서 마커를 옮기는 것만으로 바꿀 수 있다
    /// </summary>
    public class TeleportNetwork : MonoBehaviour
    {
        /// <summary>이동 목록의 한 장소.</summary>
        [Serializable]
        public class Destination
        {
            [Tooltip("장소를 구분하는 이름표. 포털의 locationId와 같은 값을 쓴다")]
            public string id;
            [Tooltip("목록에 보여 줄 이름")]
            public string displayName;
            [Tooltip("이름 아래에 작게 보여 줄 설명 (예: 사냥터)")]
            public string description;
            [Tooltip("도착 지점. 위치와 바라보는 방향을 그대로 쓴다")]
            public Transform arrival;
            [Tooltip("0보다 크면 이 퀘스트를 수락한 뒤(진행 중·완료 보고 대기·완료)에만 갈 수 있다. 0이면 항상 갈 수 있다")]
            public int requiredQuestID;
            [Tooltip("잠겨 있을 때 안내로 띄울 문구")]
            public string lockedMessage = "아직 갈 수 없는 곳입니다.";
        }

        [Tooltip("목록에 나오는 순서대로 넣는다 (마을이 있으면 던전 목록 맨 위에 온다)")]
        [SerializeField] private Destination[] destinations;

        public static TeleportNetwork Instance { get; private set; }

        public IReadOnlyList<Destination> Destinations => destinations;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // 에디터에서 도메인 리로드 없이 플레이를 다시 시작해도 이전 플레이의 참조가 남지 않게 비운다
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        /// <summary>id에 해당하는 장소를 찾는다. 없으면 null.</summary>
        public Destination Find(string id)
        {
            if (destinations == null)
            {
                return null;
            }

            foreach (Destination destination in destinations)
            {
                if (destination != null && destination.id == id)
                {
                    return destination;
                }
            }

            return null;
        }

        /// <summary>퀘스트 조건으로 이 장소에 갈 수 있는지. 퀘스트 매니저가 아직 없으면 잠긴 것으로 본다.</summary>
        public static bool IsUnlocked(Destination destination)
        {
            if (destination == null || destination.requiredQuestID <= 0)
            {
                return true;
            }

            if (QuestManager.Instance == null)
            {
                return false;
            }

            QuestManager.QuestProgressState state = QuestManager.Instance.GetState(destination.requiredQuestID);
            return state is QuestManager.QuestProgressState.Accepted
                or QuestManager.QuestProgressState.ReadyToTurnIn
                or QuestManager.QuestProgressState.Completed;
        }

        /// <summary>텔레포트 목록에서 장소 옆에 붙일 퀘스트 표시 종류.</summary>
        public enum QuestMarker { None, InProgress, TurnIn }

        // 마을 장소 id (완료 보고를 하러 돌아갈 곳)
        private const string VillageId = "village";

        /// <summary>
        /// 이 장소에 퀘스트 표시를 붙일지 정한다.
        ///  - InProgress: 수락한 퀘스트의 진행 장소 (처치·수집 지역이나 보스 던전)
        ///  - TurnIn: 목표를 마쳐 완료 보고만 남은 퀘스트가 있을 때의 마을
        /// 튜토리얼 퀘스트는 마을 안에서 끝나므로 표시하지 않는다.
        /// </summary>
        public static QuestMarker GetQuestMarker(Destination destination)
        {
            if (destination == null || QuestManager.Instance == null)
            {
                return QuestMarker.None;
            }

            QuestMarker result = QuestMarker.None;

            foreach (QuestData.Quest quest in QuestManager.Instance.AllQuests)
            {
                QuestManager.QuestProgressState state = QuestManager.Instance.GetState(quest.questID);

                if (state == QuestManager.QuestProgressState.ReadyToTurnIn && destination.id == VillageId)
                {
                    return QuestMarker.TurnIn;
                }

                if (state == QuestManager.QuestProgressState.Accepted && IsQuestPlace(quest, destination))
                {
                    result = QuestMarker.InProgress;
                }
            }

            return result;
        }

        // 퀘스트의 진행 장소가 이 장소인지. 지역 이름이 있으면 그 지역, 없으면 보스 종류로 던전을 정한다
        private static bool IsQuestPlace(QuestData.Quest quest, Destination destination)
        {
            if (quest.type != QuestData.Type.Kill && quest.type != QuestData.Type.Collect)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(quest.zoneName))
            {
                return destination.displayName == quest.zoneName;
            }

            return quest.targetType switch
            {
                QuestData.TargetType.DrekMalos => destination.id == "ruined-citadel",
                QuestData.TargetType.Zyrathon => destination.id == "blood-sanctum",
                _ => false,
            };
        }
    }
}
