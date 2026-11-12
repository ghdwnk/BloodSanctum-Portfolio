using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace RPGGame
{
    /// <summary>
    /// 마을 밖 필드의 일반 몬스터와 보스를 생성·유지하는 스포너. 게임 씬에 하나만 두고, 자식 오브젝트들이 스폰 지점이다.
    ///
    /// - 상시 몬스터: 시작할 때 ambientMonsterCount마리를 채우고, 일정 수(respawnDeathThreshold)가 죽으면
    ///   respawnDelay 뒤 한 번에 다시 채운다 (한 마리씩 바로 채우면 사냥터를 정리한 느낌이 없다)
    /// - 퀘스트 몬스터: 처치·수집 퀘스트를 수락하면 QuestManager가 SpawnForQuest를 불러 목표 수보다 넉넉히 만들고,
    ///   퀘스트가 끝나면 플레이어에게서 먼 개체부터 정리한다
    /// - 보스: 보스 퀘스트를 수락하면 SpawnBoss로 보스 ID에 맞는 프리팹을 지정된 아레나 지점에 만든다
    /// - 보스 소환수: SpawnMonsterAt으로 지정 위치에 만들며 상시 개체 수에는 넣지 않는다
    /// 마을 근처 사냥터는 HuntingGroundSpawner가 따로 맡는다.
    /// </summary>
    public class MonsterSpawner : MonoBehaviour
    {
        /// <summary>
        /// 스폰 가능한 일반 몬스터 한 종류. 프리팹이 자신의 EnemyData를 들고 있으므로
        /// 새 몬스터는 이 배열에 항목을 하나 추가하는 것만으로 등록된다.
        /// </summary>
        [Serializable]
        public class EnemySpawnEntry
        {
            public GameObject prefab;
            [Tooltip("무작위 스폰에서 뽑힐 가중치. 0이면 자동 스폰 대상에서 제외된다 (퀘스트가 종류를 지정할 때는 상관없다)")]
            [Min(0)] public int weight = 1;

            // 프리팹에 연결된 EnemyData (ID 조회용)
            public EnemyData Data
            {
                get
                {
                    if (prefab == null)
                    {
                        return null;
                    }

                    MonsterStateManager manager = prefab.GetComponent<MonsterStateManager>();

                    return manager != null ? manager.EnemyData : null;
                }
            }
        }

        /// <summary>보스마다 전용 아레나가 있을 때 쓰는 개별 스폰 지점.</summary>
        [Serializable]
        public class BossSpawnOverride
        {
            [Tooltip("BossData.enemyId (예: Zyrathon)")]
            public string bossId;
            public Transform spawnPoint;
        }

        private static MonsterSpawner instance;

        /// <summary>씬에 스포너가 있는지. 세이브를 불러올 때 퀘스트 몬스터를 만들어도 되는지 확인하는 용도다.</summary>
        public static bool Exists => instance != null;

        [Header("스폰 대상")]
        [Tooltip("스폰 가능한 일반 몬스터 목록")]
        [SerializeField] private EnemySpawnEntry[] normalEnemies;
        [Tooltip("스폰 가능한 보스 프리팹 목록 (프리팹이 BossData를 들고 있다)")]
        [SerializeField] private GameObject[] bossPrefabs;
        [Tooltip("보스를 생성할 기본 위치. 예전 이름(grenadierSpawnPoint)으로 저장된 씬도 읽도록 연결해 둔다")]
        [FormerlySerializedAs("grenadierSpawnPoint")]
        [SerializeField] private Transform bossSpawnPoint;
        [Tooltip("보스마다 따로 쓰는 스폰 지점. 여기에 없는 보스는 기본 위치에서 생성된다. 새 아레나는 코드를 고치지 않고 항목만 추가하면 된다")]
        [SerializeField] private BossSpawnOverride[] bossSpawnOverrides;

        [Header("상시 몬스터")]
        [Tooltip("맵에 상시 유지할 일반 몬스터 수 (시작할 때 이만큼 만들고, 리스폰 때 이 수까지 채운다)")]
        [SerializeField] private int ambientMonsterCount = 12;
        [Tooltip("상시 몬스터의 레벨")]
        [SerializeField] private int ambientMonsterLevel = 1;
        [Tooltip("이 수만큼 죽으면 리스폰을 예약한다. 한 마리 죽을 때마다 채우면 사냥터를 정리한 느낌이 없다")]
        [SerializeField] private int respawnDeathThreshold = 5;
        [Tooltip("리스폰이 예약된 뒤 실제로 나타나기까지의 시간(초)")]
        [SerializeField] private float respawnDelay = 10f;
        [Tooltip("필드에 존재할 수 있는 일반 몬스터의 상한. 퀘스트를 받을 때마다 더 만들어지는데 안 잡은 개체가 사라지지 않아 개체 수가 불어나는 것을 막는다")]
        [SerializeField] private int maxAliveMonsters = 30;
        [Tooltip("동시에 플레이어를 공격할 수 있는 몬스터 수")]
        [SerializeField] private int maxSimultaneousAttackers = 4;

        [Header("퀘스트 몬스터")]
        [Tooltip("퀘스트를 수락할 때 목표 수의 몇 배를 만들지. 스폰 지점이 넓게 흩어져 있어 목표 수만 만들면 찾기 어렵다")]
        [SerializeField] private int questSpawnMultiplier = 2;
        [Tooltip("배율에 더해 추가로 만들 수")]
        [SerializeField] private int questSpawnBonus = 3;
        [Tooltip("퀘스트가 끝났을 때 남은 퀘스트 몬스터를 치우는 기준 거리(m). 눈앞에서 사라지면 어색하므로 이보다 먼 개체만 치운다")]
        [SerializeField] private float questCleanupDistance = 25f;

        // 스폰 위치 (이 오브젝트의 자식들). OnEnable에서 모은다
        private Transform[] spawnPositions;

        // 생성한 몬스터 목록. 죽은 몬스터는 씬에 남으므로 살아 있는 수를 셀 때 걸러낸다
        private readonly List<MonsterStateManager> spawnedMonsters = new();
        // 퀘스트 때문에 만든 개체. 퀘스트가 끝나면 정리할 수 있도록 따로 기억한다
        private readonly List<MonsterStateManager> questSpawnedMonsters = new();
        // 마지막 리스폰 이후 누적된 사망 수
        private int deathsSinceRespawn;
        // 리스폰이 이미 예약돼 있으면 중복으로 예약하지 않는다
        private bool isRespawnPending;

        // 지면에 맞춰 생성할 때 살짝 띄우는 높이.
        // 캡슐 바닥이 지면과 정확히 겹치면 첫 프레임에 파고들 수 있어 여유를 둔다
        private const float SpawnGroundOffset = 0.1f;

        // 스폰 지점의 XZ는 그대로 쓰고 높이만 지면에 맞추는 함수.
        // 스폰 지점 중 일부가 지형보다 낮게 배치돼 있어(가장 깊은 곳은 -7.6m),
        // 그대로 생성하면 몬스터가 지형 안에서 시작해 땅에 박히거나 월드 밖으로 떨어진다.
        // 스폰 지점 Transform 자체는 건드리지 않으므로 배치 작업과 충돌하지 않는다
        private static Vector3 SnapToGround(Vector3 position)
        {
            if (Util.SampleGroundHeight(position, out float groundY))
            {
                position.y = groundY + SpawnGroundOffset;
            }

            return position;
        }

        private void Awake()
        {
            // 정적 값이라 에디터에서 플레이를 반복하면 남아 있는다. 씬 시작마다 초기화한다
            GameStats.ResetForNewSession();

            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            // 몬스터를 생성할 위치를 검색해 배열에 저장
            var transforms = GetComponentsInChildren<Transform>();
            // 검색한 트랜스폼 중 첫 번째는 사용하지 않을 것이므로 검색한 수 중에서 1개 제외
            spawnPositions = new Transform[transforms.Length - 1];
            // 두 번째 트랜스폼부터 저장
            Array.Copy(transforms, 1, spawnPositions, 0, spawnPositions.Length);
        }

        private void Start()
        {
            // 동시 공격 인원 제한을 몬스터 쪽 정적 값에 밀어넣는다 (인스펙터에서 조정하기 위함)
            MonsterStateManager.MaxSimultaneousAttackers = Mathf.Max(1, maxSimultaneousAttackers);

            // 퀘스트가 끝나면 남은 퀘스트 몬스터를 정리한다
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.SubscribeOnQuestCompleted(_ => CleanupQuestMonsters());
            }

            // 사망 수를 세어 리스폰 시점을 정한다
            MonsterStateManager.OnAnyMonsterDead += OnMonsterDied;

            // 시작 시 상시 몬스터를 채워 둔다.
            // 예전에는 8초 주기 코루틴이 부족분을 계속 채웠는데, 그러면 죽인 만큼 곧바로 다시 나와
            // 사냥터를 정리한 느낌이 없었다. 이제는 일정 수가 죽었을 때만 한 번에 채운다
            FillAmbientMonsters();
        }

        private void OnDestroy()
        {
            MonsterStateManager.OnAnyMonsterDead -= OnMonsterDied;

            if (instance == this)
            {
                instance = null;
            }
        }

        // 일반 몬스터가 죽을 때마다 호출. 누적 사망 수가 기준에 닿으면 리스폰을 예약한다
        private void OnMonsterDied()
        {
            ++deathsSinceRespawn;

            if (isRespawnPending || deathsSinceRespawn < respawnDeathThreshold)
            {
                return;
            }

            isRespawnPending = true;
            StartCoroutine(RespawnAfterDelay());
        }

        private IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(respawnDelay);

            FillAmbientMonsters();

            // 대기 중에 더 죽은 개체도 이번 보충에 포함됐으므로 카운터를 비운다
            deathsSinceRespawn = 0;
            isRespawnPending = false;
        }

        // 상시 몬스터 수(ambientMonsterCount)에 모자란 만큼 생성하는 함수.
        // 퀘스트 몬스터는 별도 목록으로 관리되고 퀘스트가 끝나면 정리되므로 상시 수에서 제외한다
        private int FillAmbientMonsters()
        {
            int alive = CountAliveMonsters() - CountAliveQuestMonsters();
            int shortage = ambientMonsterCount - alive;
            int spawned = 0;

            for (int ix = 0; ix < shortage; ++ix)
            {
                if (SpawnAMonster(ambientMonsterLevel) != null)
                {
                    ++spawned;
                }
            }

            return spawned;
        }

        private int CountAliveQuestMonsters()
        {
            int alive = 0;

            foreach (MonsterStateManager monster in questSpawnedMonsters)
            {
                if (monster != null && !monster.IsDead)
                {
                    ++alive;
                }
            }

            return alive;
        }

        // 살아있는 몬스터 수를 세면서, 파괴됐거나 죽은 항목을 목록에서 제거하는 함수.
        // 씬 전체를 검색(FindObjectsByType)하지 않고 직접 관리하는 목록만 훑는다
        private int CountAliveMonsters()
        {
            int alive = 0;

            for (int ix = spawnedMonsters.Count - 1; ix >= 0; --ix)
            {
                MonsterStateManager monster = spawnedMonsters[ix];

                if (monster == null || monster.IsDead)
                {
                    spawnedMonsters.RemoveAt(ix);
                    continue;
                }

                ++alive;
            }

            return alive;
        }

        /// <summary>
        /// 처치·수집 퀘스트를 수락했을 때 QuestManager가 부른다. 목표 수보다 넉넉히 만들어 퀘스트를 깰 수 있게 한다.
        /// enemyId를 지정하면 그 종류만 만든다.
        /// </summary>
        public static void SpawnForQuest(int requiredCount, int monsterLevel, string enemyId = null)
        {
            int spawnCount = requiredCount * instance.questSpawnMultiplier + instance.questSpawnBonus;

            // 새 퀘스트를 시작할 때 이전 퀘스트에서 남은 몬스터부터 정리해 자리를 비운다
            instance.CleanupQuestMonsters();

            SpawnMonsters(spawnCount, monsterLevel, enemyId, instance.questSpawnedMonsters, true);
        }

        /// <summary>퀘스트가 끝났을 때 남은 퀘스트 몬스터를 치운다. 플레이어와 가까운 개체는 눈앞에서 사라지면 어색해 그대로 둔다.</summary>
        public void CleanupQuestMonsters()
        {
            Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;

            for (int ix = questSpawnedMonsters.Count - 1; ix >= 0; --ix)
            {
                MonsterStateManager monster = questSpawnedMonsters[ix];

                if (monster == null || monster.IsDead)
                {
                    questSpawnedMonsters.RemoveAt(ix);
                    continue;
                }

                if (player != null &&
                    Vector3.Distance(monster.transform.position, player.position) < questCleanupDistance)
                {
                    continue;
                }

                questSpawnedMonsters.RemoveAt(ix);
                spawnedMonsters.Remove(monster);
                Destroy(monster.gameObject);
            }
        }

        /// <summary>몬스터를 count마리 만든다. collector를 주면 만들어진 개체를 거기에 모은다.</summary>
        public static void SpawnMonsters(int count, int monsterLevel, string enemyId = null,
            List<MonsterStateManager> collector = null, bool ignoreCap = false)
        {
            // 전달받은 수만큼 루프를 통해 몬스터 생성
            for (int ix = 0; ix < count; ++ix)
            {
                MonsterStateManager spawned = SpawnAMonster(monsterLevel, enemyId, ignoreCap);

                // 생성 결과를 직접 받아 모은다.
                // spawnedMonsters의 인덱스 범위로 추리면, 상한 검사가 목록에서 죽은 항목을
                // 지우면서 인덱스가 밀려 기록이 어긋난다
                if (spawned != null)
                {
                    collector?.Add(spawned);
                }
            }
        }

        // 스폰할 종류를 고르는 함수. enemyId가 지정되면 그 종류를, 아니면 가중치 무작위로 뽑는다
        private static EnemySpawnEntry PickEntry(string enemyId)
        {
            if (instance.normalEnemies == null || instance.normalEnemies.Length == 0)
            {
                Debug.LogError("스폰 가능한 일반 몬스터가 등록되지 않았습니다.");
                return null;
            }

            if (!string.IsNullOrEmpty(enemyId))
            {
                foreach (EnemySpawnEntry entry in instance.normalEnemies)
                {
                    EnemyData data = entry.Data;
                    if (entry.prefab != null && data != null && data.enemyId == enemyId)
                    {
                        return entry;
                    }
                }

                Debug.LogError($"스폰 목록에 없는 몬스터 ID: {enemyId}");
                return null;
            }

            // 가중치 합을 구한 뒤 누적 방식으로 하나를 고른다
            int totalWeight = 0;
            foreach (EnemySpawnEntry entry in instance.normalEnemies)
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

            int roll = UnityEngine.Random.Range(0, totalWeight);
            foreach (EnemySpawnEntry entry in instance.normalEnemies)
            {
                if (entry.prefab == null)
                {
                    continue;
                }

                roll -= entry.weight;
                if (roll < 0)
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>몬스터 하나를 스폰 지점 중 임의의 곳에 만든다. 상한에 닿았으면 만들지 않고 null (ignoreCap이면 상한을 무시한다).</summary>
        public static MonsterStateManager SpawnAMonster(int monsterLevel, string enemyId = null, bool ignoreCap = false)
        {
            // 씬이 내려가는 중(플레이 종료/씬 전환)에 보스 소환 코루틴이 늦게 도착하면 스포너가 먼저 파괴돼 있을 수 있다
            if (instance == null)
            {
                return null;
            }

            // 상한 판정은 스포너의 내부 목록이 아니라 몬스터가 직접 등록하는 생존 목록을 쓴다.
            // 내부 목록은 죽은 항목을 지우는 시점이 달라 실제 씬 개체 수와 어긋난다.
            // 퀘스트 대상 몬스터는 상한 때문에 못 나오면 퀘스트를 깰 수 없으므로 예외로 둔다
            if (!ignoreCap && MonsterStateManager.AliveMonsterCount >= instance.maxAliveMonsters)
            {
                return null;
            }

            // 스폰 지점이 하나도 없으면 생성하지 않는다. 사냥터는 HuntingGroundSpawner가 따로 맡으므로
            // 이 스포너의 지점은 비어 있을 수 있고, 그 상태에서 Random.Range(0, 0)으로 인덱싱하면 예외가 난다
            if (instance.spawnPositions == null || instance.spawnPositions.Length == 0)
            {
                return null;
            }

            EnemySpawnEntry entry = PickEntry(enemyId);
            if (entry == null)
            {
                return null;
            }

            // 몬스터를 생성할 위치를 랜덤으로 선택
            int spawnPositionIndex = UnityEngine.Random.Range(0, instance.spawnPositions.Length);
            // 몬스터 생성 위치/회전 저장
            Vector3 spawnLocation = SnapToGround(instance.spawnPositions[spawnPositionIndex].position);
            Quaternion spawnRotation = instance.spawnPositions[spawnPositionIndex].rotation;
            
            // 생성
            GameObject newMonster = Instantiate(entry.prefab, spawnLocation, spawnRotation);
            
            // 생성한 몬스터의 레벨 설정
            MonsterStateManager stateManager = newMonster.GetComponent<MonsterStateManager>();
            stateManager.SetLevel(monsterLevel);

            // 개체 수 유지를 위해 생성한 몬스터를 목록에 등록
            instance.spawnedMonsters.Add(stateManager);
            
            return stateManager;
        }
        
        /// <summary>
        /// 지정한 위치에 몬스터 하나를 만든다 (보스 소환수용). 일반 스폰과 달리 스폰 지점을 쓰지 않고, 개체 수 상한과 리스폰 집계(spawnedMonsters)에도 넣지 않는다.
        /// 소환수는 보스전 동안만 존재해야 해서, 구역의 평상시 개체 수 유지 로직과 섞이면 안 되기 때문이다.
        /// </summary>
        public static MonsterStateManager SpawnMonsterAt(Vector3 position, Quaternion rotation, int monsterLevel, string enemyId)
        {
            if (instance == null)
            {
                return null;
            }

            EnemySpawnEntry entry = PickEntry(enemyId);
            if (entry == null)
            {
                return null;
            }

            GameObject newMonster = Instantiate(entry.prefab, SnapToGround(position), rotation);
            MonsterStateManager stateManager = newMonster.GetComponent<MonsterStateManager>();
            stateManager.SetLevel(monsterLevel);

            return stateManager;
        }

        // 보스 전용 스폰 지점이 등록돼 있으면 그것을, 없으면 기본 보스 스폰 지점을 돌려준다
        private static Transform FindBossSpawnPoint(string bossId)
        {
            if (instance.bossSpawnOverrides != null)
            {
                foreach (BossSpawnOverride entry in instance.bossSpawnOverrides)
                {
                    if (entry != null && entry.spawnPoint != null && entry.bossId == bossId)
                    {
                        return entry.spawnPoint;
                    }
                }
            }

            return instance.bossSpawnPoint;
        }

        /// <summary>보스를 ID로 찾아 만든다. 보스는 죽으면 되돌릴 수 없으므로 매번 새 인스턴스를 만든다.</summary>
        public static GameObject SpawnBoss(string bossId, int monsterLevel)
        {
            if (instance.bossPrefabs == null)
            {
                Debug.LogError("보스 프리팹이 등록되지 않았습니다.");
                return null;
            }

            foreach (GameObject prefab in instance.bossPrefabs)
            {
                if (prefab == null)
                {
                    continue;
                }

                BossStateManager manager = prefab.GetComponent<BossStateManager>();
                if (manager == null || manager.Data == null || manager.Data.enemyId != bossId)
                {
                    continue;
                }

                Transform spawnPoint = FindBossSpawnPoint(bossId);
                Vector3 position = SnapToGround(spawnPoint != null ? spawnPoint.position : instance.transform.position);
                Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

                GameObject newBoss = Instantiate(prefab, position, rotation);
                // 프리팹이 비활성 상태로 저장돼 있으면 Awake가 아직 실행되지 않으므로 먼저 활성화한다
                newBoss.SetActive(true);
                newBoss.GetComponent<BossStateManager>().SetLevel(monsterLevel);

                return newBoss;
            }

            Debug.LogError($"등록되지 않은 보스 ID: {bossId}");

            return null;
        }
    }
}
