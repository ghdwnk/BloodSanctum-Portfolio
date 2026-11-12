using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 게임 데이터(ScriptableObject)를 Resources에서 한 번 읽어 두고 어디서든 꺼내 쓰게 하는 싱글턴.
    ///
    /// 실행 순서를 -100으로 앞당겨, 다른 컴포넌트의 Awake/Start보다 먼저 데이터가 준비되게 한다.
    /// 읽는 데이터 (모두 Assets/RPGGame/Resources/Data 아래)
    ///  - Player Data     : 레벨별 기본 스탯과 필요 경험치
    ///  - EnemyData 전부  : 일반 몬스터·보스 종류별 데이터. enemyId로 색인한다
    ///  - Quest Data      : 퀘스트 목록
    ///  - NPC Data        : NPC 이름·대사
    ///  - Item Database   : 세이브에 저장된 아이템 이름으로 아이템 에셋을 다시 찾을 때 쓴다
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class DataManager : MonoBehaviour
    {
        private const string DataFolder = "Data";

        public static DataManager Instance { get; private set; }

        public PlayerData playerData { get; private set; }
        public QuestData questData { get; private set; }
        public NPCData npcData { get; private set; }
        public ItemDatabase itemDatabase { get; private set; }

        // 몬스터 종류 ID(EnemyData.enemyId) → 데이터.
        // 새 몬스터를 추가할 때 이 클래스에 필드를 늘리지 않고 Resources/Data에 에셋만 넣으면 된다
        private readonly Dictionary<string, EnemyData> enemyDataById = new Dictionary<string, EnemyData>();

        private void Awake()
        {
            // 씬이 다시 열려 두 번째 인스턴스가 생기면 먼저 있던 쪽을 유지한다
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            LoadAll();
        }

        private void LoadAll()
        {
            playerData = Resources.Load<PlayerData>(DataFolder + "/Player Data");
            if (playerData == null || playerData.levels.Count == 0)
            {
                Debug.LogError("[DataManager] 플레이어 레벨 데이터가 비어 있습니다 (Resources/Data/Player Data).");
            }

            questData = Resources.Load<QuestData>(DataFolder + "/Quest Data");
            if (questData == null || questData.quests.Count == 0)
            {
                Debug.LogError("[DataManager] 퀘스트 데이터가 비어 있습니다 (Resources/Data/Quest Data).");
            }

            npcData = Resources.Load<NPCData>(DataFolder + "/NPC Data");
            if (npcData == null || npcData.attributes.Count == 0)
            {
                Debug.LogError("[DataManager] NPC 데이터가 비어 있습니다 (Resources/Data/NPC Data).");
            }

            itemDatabase = Resources.Load<ItemDatabase>(DataFolder + "/Item Database");
            if (itemDatabase == null)
            {
                Debug.LogError("[DataManager] 아이템 데이터베이스를 찾지 못했습니다 (Resources/Data/Item Database).");
            }

            LoadEnemyData();
        }

        // Resources/Data 아래(하위 폴더 포함)의 EnemyData 에셋을 모두 읽어 ID로 색인한다.
        // enemyId를 비워 두면 에셋 이름을 ID로 쓴다
        private void LoadEnemyData()
        {
            enemyDataById.Clear();

            foreach (EnemyData enemyData in Resources.LoadAll<EnemyData>(DataFolder))
            {
                string id = string.IsNullOrEmpty(enemyData.enemyId) ? enemyData.name : enemyData.enemyId;

                if (enemyDataById.ContainsKey(id))
                {
                    Debug.LogError($"[DataManager] 몬스터 데이터 ID가 중복됩니다: {id}");
                    continue;
                }

                enemyDataById.Add(id, enemyData);

                if (enemyData.levels.Count == 0)
                {
                    Debug.LogError($"[DataManager] {id}의 레벨 데이터가 비어 있습니다.");
                }
            }

            if (enemyDataById.Count == 0)
            {
                Debug.LogError("[DataManager] EnemyData 에셋을 찾지 못했습니다. Resources/Data 폴더를 확인하세요.");
            }
        }

        /// <summary>몬스터 데이터를 로그 없이 조회한다. 존재 여부만 확인할 때 쓴다.</summary>
        public bool TryGetEnemyData(string enemyId, out EnemyData data)
        {
            return enemyDataById.TryGetValue(enemyId, out data);
        }

        /// <summary>몬스터 종류 ID로 데이터를 조회한다. 없으면 에러 로그를 남기고 null을 돌려준다.</summary>
        public EnemyData GetEnemyData(string enemyId)
        {
            if (enemyDataById.TryGetValue(enemyId, out EnemyData found))
            {
                return found;
            }

            Debug.LogError($"[DataManager] 등록되지 않은 몬스터 데이터 ID: {enemyId}");
            return null;
        }
    }
}
