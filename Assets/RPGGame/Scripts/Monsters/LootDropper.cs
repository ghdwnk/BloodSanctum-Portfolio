using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 몬스터가 죽은 자리에 골드·아이템 드랍(DroppedLoot)을 뿌리는 컴포넌트. 게임 씬의 매니저 오브젝트에 하나만 둔다.
    /// 드랍 프리팹 참조를 들고 있어야 해서 정적 클래스가 아니라 씬 싱글턴으로 두었다 (InventoryManager 등과 같은 방식).
    /// 일반 몬스터는 MonsterStateManager.OnMonsterDead가 DropFrom을, 사냥터는 구역 전용 목록으로 DropEntries를 부른다.
    /// </summary>
    public class LootDropper : MonoBehaviour
    {
        private static LootDropper instance;
        public static LootDropper Instance => instance;

        [Tooltip("드랍 프리팹")]
        [SerializeField] private DroppedLoot lootPrefab;
        [Tooltip("골드 드랍에 쓸 아이콘 (아이템은 각자의 아이콘을 쓴다)")]
        [SerializeField] private Sprite goldIcon;
        [Tooltip("드랍이 한 점에 겹치지 않도록 흩뿌리는 반경(m)")]
        [SerializeField] private float scatterRadius = 0.9f;
        [Tooltip("드랍이 지면에서 떠 있는 높이(m)")]
        [SerializeField] private float dropHeight = 0.6f;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        /// <summary>처치된 몬스터의 데이터에 따라 골드와 아이템을 떨어뜨린다.</summary>
        public void DropFrom(EnemyData data, Vector3 position)
        {
            if (data == null || lootPrefab == null)
            {
                return;
            }

            if (data.goldMax > 0)
            {
                int gold = Random.Range(data.goldMin, data.goldMax + 1);
                if (gold > 0)
                {
                    Spawn(position).SetupGold(gold, goldIcon);
                }
            }

            if (data.drops == null)
            {
                return;
            }

            DropEntries(data.drops, position);
        }

        /// <summary>
        /// 확률 드랍 목록을 굴려 떨어뜨린다. 사냥터가 가진 구역 전용 목록(구역 전용 장비 등)도 이 함수를 쓴다.
        /// 적 종류와 무관하게 "이 구역에서 잡으면 나온다"를 표현하는 경로라 골드는 다루지 않는다.
        /// </summary>
        public void DropEntries(System.Collections.Generic.IReadOnlyList<EnemyData.DropEntry> entries, Vector3 position)
        {
            if (entries == null || lootPrefab == null)
            {
                return;
            }

            for (int ix = 0; ix < entries.Count; ++ix)
            {
                EnemyData.DropEntry entry = entries[ix];
                if (entry == null || entry.item == null || Random.value > entry.chance)
                {
                    continue;
                }

                int count = Random.Range(entry.minCount, entry.maxCount + 1);
                if (count > 0)
                {
                    Spawn(position).SetupItem(entry.item, count);
                }
            }
        }

        // 드랍 하나를 지면 위에 생성한다.
        // 스폰 지점이 지형과 어긋나 있어도 되도록 높이는 지면을 다시 재서 잡는다
        private DroppedLoot Spawn(Vector3 origin)
        {
            Vector2 offset = Random.insideUnitCircle * scatterRadius;
            Vector3 position = origin + new Vector3(offset.x, 0f, offset.y);

            if (Util.SampleGroundHeight(position, out float groundY))
            {
                position.y = groundY;
            }

            position.y += dropHeight;

            return Instantiate(lootPrefab, position, Quaternion.identity);
        }
    }
}
