using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 몬스터 한 종류의 데이터(감지·공격·이동·배회 값, 레벨별 스탯, 드랍)를 담은 에셋.
    ///
    /// 새 몬스터는 이 에셋을 하나 더 만들어 프리팹의 MonsterStateManager에 연결하면 되고 코드를 고칠 필요가 없다.
    /// Resources/Data 아래의 에셋은 DataManager가 enemyId로 찾아 두고, 퀘스트·스포너가 그 이름으로 몬스터를 지목한다.
    /// </summary>
    [CreateAssetMenu(fileName = "New Enemy Data", menuName = "Scriptable Objects/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        /// <summary>몬스터 분류. 정수 값이 에셋에 저장되므로 순서를 바꾸지 않는다. 퀘스트가 Boss 여부로 보스를 구분한다.</summary>
        public enum EnemyType { Melee, Ranged, Tank, Boss }

        /// <summary>확률 드랍 한 줄. 아이템마다 확률과 개수 범위를 따로 두어, 한 몬스터가 여러 종류를 다른 확률로 떨어뜨릴 수 있다.</summary>
        [System.Serializable]
        public class DropEntry
        {
            public Item item;
            [Tooltip("드랍 확률 (0~1). 0.25면 네 번에 한 번꼴")]
            [Range(0f, 1f)] public float chance = 0.25f;
            public int minCount = 1;
            public int maxCount = 1;
        }

        /// <summary>레벨 하나의 능력치.</summary>
        [System.Serializable]
        public class LevelData
        {
            public int level = 1;
            public float maxHP;
            public float attack;
            public float defense;
            [Tooltip("처치하면 플레이어가 얻는 경험치")]
            public float gainExp;
        }

        [Header("식별")]
        [Tooltip("종류를 가리키는 고유 ID. 퀘스트의 처치 대상(TargetType)·스포너가 이 이름으로 몬스터를 찾는다")]
        public string enemyId;
        [Tooltip("머리 위 이름표와 보스 체력바에 표시할 이름")]
        public string enemyName;
        public EnemyType enemyType = EnemyType.Melee;

        [Header("감지")]
        [Tooltip("플레이어를 알아채는 거리(m)")]
        public float detectionRange = 10f;
        [Tooltip("시야각(도). 이 각도 안에 들어와야 플레이어를 알아챈다")]
        public float detectionAngle = 60f;
        [Tooltip("추격 중 시야를 벗어난 뒤 추격을 포기하기까지의 유예 시간(초)")]
        public float loseSightDuration = 1.5f;
        [Tooltip("공격받았을 때 시야와 상관없이 추격을 유지하는 시간(초). 등 뒤에서 맞은 직후엔 시야 판정이 실패하므로 그동안 판정을 건너뛴다")]
        public float aggroDuration = 6f;

        [Header("공격")]
        [Tooltip("이 거리(m) 안에 들어오면 공격을 시작한다")]
        public float attackRange = 2f;

        [Header("이동")]
        [Tooltip("이동 속도 배율. 이동은 애니메이션의 루트 모션이라 1이 원본 속도다")]
        public float moveSpeed = 1f;
        [Tooltip("배회 중 방향을 바꾸는 속도(도/초)")]
        public float patrolRotateSpeed = 360f;
        [Tooltip("추격 중 방향을 바꾸는 속도(도/초)")]
        public float chaseRotateSpeed = 540f;

        [Header("배회")]
        [Tooltip("스폰 지점을 중심으로 배회하는 반경(m). 목적지는 항상 이 안에서 뽑는다")]
        public float patrolRadius = 12f;
        [Tooltip("목적지 하나를 향해 걷는 최대 시간(초). 지형에 막혀 도착하지 못하면 이 시간 뒤 새로 뽑는다")]
        public float patrolLegDuration = 8f;
        [Tooltip("도착한 뒤 다음 이동까지 쉬는 시간의 최소(초)")]
        public float patrolIdleTimeMin = 0.4f;
        [Tooltip("쉬는 시간의 최대(초). 범위로 두어 여러 마리가 같은 박자로 동시에 출발하지 않게 한다")]
        public float patrolIdleTimeMax = 1.4f;

        [Header("간격 유지")]
        [Tooltip("다른 몬스터가 이 거리(m) 안에 들어오면 서로 밀어낸다 (캐릭터 컨트롤러 지름 0.7 + 여유)")]
        public float separationRadius = 1.6f;
        [Tooltip("밀어내는 속도(m/초). 너무 크면 걷는 동작보다 밀림이 두드러진다")]
        public float separationStrength = 2.2f;

        [Header("드랍")]
        [Tooltip("처치 시 떨어지는 골드의 최솟값. 최댓값이 0이면 골드를 떨어뜨리지 않는다")]
        public int goldMin = 0;
        public int goldMax = 0;
        [Tooltip("확률로 떨어지는 아이템 목록")]
        public List<DropEntry> drops = new();

        [Header("레벨별 능력치")]
        [Tooltip("레벨 1부터 순서대로 (목록 번호 = 레벨 - 1)")]
        public List<LevelData> levels = new();

        /// <summary>레벨 번호로 능력치를 가져온다. 범위를 벗어난 레벨은 양 끝 값으로 잘라 돌려준다. 표가 비어 있으면 null.</summary>
        public LevelData GetLevelData(int level)
        {
            if (levels == null || levels.Count == 0)
            {
                return null;
            }

            return levels[Mathf.Clamp(level, 1, levels.Count) - 1];
        }
    }
}
