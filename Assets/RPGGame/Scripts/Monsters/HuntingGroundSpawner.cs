using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 마을 근처 소규모 사냥터(초급·중급·고급) 하나를 맡는 스포너. 사냥터마다 하나씩 둔다.
    ///
    /// 전역 MonsterSpawner는 싱글턴이라 사냥터마다 다른 레벨을 줄 수 없어서, 고정된 소수 개체(5~6마리)를
    /// 정해진 레벨로 유지하는 용도로 따로 만들었다. 한 마리가 죽으면 그 한 마리만 일정 시간 뒤에 다시 채운다
    /// (개체 수가 적어 전역 스포너처럼 여러 마리가 죽을 때까지 기다리지 않는다).
    /// 이 오브젝트의 자식 Transform이 스폰 위치이고, 구역 전용 드랍(zoneDrops)을 따로 굴린다.
    /// </summary>
    public class HuntingGroundSpawner : MonoBehaviour
    {
        /// <summary>사냥터에서 나올 수 있는 몬스터 한 종류.</summary>
        [System.Serializable]
        public class EnemyEntry
        {
            public GameObject prefab;
            [Tooltip("무작위로 뽑힐 가중치")]
            [Min(0)] public int weight = 1;
        }

        [Tooltip("이 사냥터에 나오는 몬스터 목록")]
        [SerializeField] private EnemyEntry[] enemies;
        [Tooltip("이 사냥터에서 유지할 몬스터 레벨")]
        [SerializeField] private int monsterLevel = 1;
        [Tooltip("상시 유지할 개체 수")]
        [SerializeField] private int monsterCount = 6;
        [Header("첫 퀘스트 보정 (초심자의 소굴 전용)")]
        [Tooltip("이 퀘스트가 진행 중(수락~완료 보고 전)일 때만 개체 수를 아래 값으로 줄인다. 0이면 사용하지 않는다")]
        [SerializeField] private int reducedWhileQuestID = 0;
        [Tooltip("위 퀘스트가 진행 중일 때 유지할 개체 수 (그 외에는 상시 유지 개체 수를 쓴다)")]
        [SerializeField] private int reducedMonsterCount = 8;
        [Tooltip("개체 수를 줄일 때 플레이어와 이 거리(m) 안에 있는 개체는 눈앞에서 사라지지 않게 남겨 둔다")]
        [SerializeField] private float trimKeepDistance = 25f;

        [Header("리스폰")]
        [Tooltip("한 마리가 죽은 뒤 다시 나타나기까지의 시간(초)")]
        [SerializeField] private float respawnDelay = 20f;
        [Tooltip("스폰 지점 주변으로 흩뿌리는 반경(m). 지점 수보다 몬스터가 많을 때 한곳에 겹쳐 나오지 않게 한다")]
        [SerializeField, Min(0f)] private float spawnScatterRadius = 3f;
        [Tooltip("이 사냥터에서 잡은 몬스터가 추가로 떨어뜨리는 구역 전용 드랍 (초급=낡은 장비, 중급=강철 장비, 고급=최고급 장비). 종류별 드랍(EnemyData.drops)과 따로 굴린다")]
        [SerializeField] private List<EnemyData.DropEntry> zoneDrops = new();

        // 스폰 지점. 이 오브젝트의 자식 Transform을 그대로 스폰 위치로 쓴다 (MonsterSpawner와 같은 방식)
        private Transform[] spawnPoints;
        private readonly List<MonsterStateManager> alive = new();

        private const float SpawnGroundOffset = 0.1f;

        private void OnEnable()
        {
            var transforms = GetComponentsInChildren<Transform>();
            spawnPoints = new Transform[transforms.Length - 1];
            System.Array.Copy(transforms, 1, spawnPoints, 0, spawnPoints.Length);
        }

        private void Start()
        {
            // 퀘스트 상태에 따라 개체 수 목표가 달라지므로, 상태가 바뀌거나 세이브가 로드될 때 다시 맞춘다
            QuestManager questManager = QuestManager.Instance;
            if (reducedWhileQuestID > 0 && questManager != null)
            {
                questManager.SubscribeOnQuestAccepted(_ => ReconcileCount());
                questManager.SubscribeOnQuestCompleted(_ => ReconcileCount());
                questManager.SubscribeOnQuestReset(_ => ReconcileCount());
                questManager.SubscribeOnStateLoaded(ReconcileCount);
            }

            ReconcileCount();
        }

        // 지금 유지해야 하는 개체 수. 첫 퀘스트가 진행 중이면 줄인 수, 아니면 기본 수
        private int TargetCount
        {
            get
            {
                if (reducedWhileQuestID > 0 && QuestManager.Instance != null)
                {
                    QuestManager.QuestProgressState state = QuestManager.Instance.GetState(reducedWhileQuestID);
                    if (state == QuestManager.QuestProgressState.Accepted || state == QuestManager.QuestProgressState.ReadyToTurnIn)
                    {
                        return Mathf.Min(reducedMonsterCount, monsterCount);
                    }
                }

                return monsterCount;
            }
        }

        // 살아 있는 개체 수를 목표에 맞춘다. 모자라면 채우고, 넘치면 플레이어에게서 먼 개체부터 치운다
        private void ReconcileCount()
        {
            int target = TargetCount;

            for (int ix = alive.Count - 1; ix >= 0; --ix)
            {
                if (alive[ix] == null)
                {
                    alive.RemoveAt(ix);
                }
            }

            while (alive.Count < target)
            {
                int before = alive.Count;
                SpawnOne();
                if (alive.Count == before)
                {
                    break;
                }
            }

            if (alive.Count <= target)
            {
                return;
            }

            Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
            for (int ix = alive.Count - 1; ix >= 0 && alive.Count > target; --ix)
            {
                MonsterStateManager monster = alive[ix];
                if (monster == null)
                {
                    alive.RemoveAt(ix);
                    continue;
                }

                if (player != null && Vector3.Distance(monster.transform.position, player.position) < trimKeepDistance)
                {
                    continue;
                }

                alive.RemoveAt(ix);
                Destroy(monster.gameObject);
            }
        }

        private void SpawnOne()
        {
            GameObject prefab = PickPrefab();
            if (prefab == null || spawnPoints == null || spawnPoints.Length == 0)
            {
                return;
            }

            Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
            Vector3 position = point.position;

            // 지점 주변 원 안의 무작위 위치로 옮긴다 (높이는 아래에서 지면에 다시 맞춘다)
            Vector2 scatter = Random.insideUnitCircle * spawnScatterRadius;
            position.x += scatter.x;
            position.z += scatter.y;

            if (Util.SampleGroundHeight(position, out float groundY))
            {
                position.y = groundY + SpawnGroundOffset;
            }

            GameObject instance = Instantiate(prefab, position, point.rotation);
            MonsterStateManager manager = instance.GetComponent<MonsterStateManager>();
            manager.SetLevel(monsterLevel);

            alive.Add(manager);

            // 이 개체가 죽는 순간만 알면 되므로, 전역 이벤트 대신 이 몬스터의 HPController에 직접 구독한다
            HPController hpController = instance.GetComponentInChildren<HPController>();
            if (hpController != null)
            {
                hpController.SubscribeOnDead(() => OnOneDied(manager));
            }
        }

        private GameObject PickPrefab()
        {
            if (enemies == null || enemies.Length == 0)
            {
                return null;
            }

            int totalWeight = 0;
            foreach (EnemyEntry entry in enemies)
            {
                if (entry.prefab != null)
                {
                    totalWeight += entry.weight;
                }
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            int roll = Random.Range(0, totalWeight);
            foreach (EnemyEntry entry in enemies)
            {
                if (entry.prefab == null)
                {
                    continue;
                }

                roll -= entry.weight;
                if (roll < 0)
                {
                    return entry.prefab;
                }
            }

            return null;
        }

        private void OnOneDied(MonsterStateManager manager)
        {
            alive.Remove(manager);

            if (manager != null)
            {
                LootDropper.Instance?.DropEntries(zoneDrops, manager.transform.position);
            }

            StartCoroutine(RespawnAfterDelay());
        }

        private IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(respawnDelay);

            // 사냥터를 비활성화(씬 전환 등)한 상태로 대기 시간이 끝났으면 다시 채우지 않는다.
            // 첫 퀘스트로 개체 수를 줄인 상태에서는 이미 목표만큼 있으면 더 채우지 않는다
            if (this != null && isActiveAndEnabled && alive.Count < TargetCount)
            {
                SpawnOne();
            }
        }
    }
}
