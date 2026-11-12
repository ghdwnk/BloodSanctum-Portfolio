using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 몬스터가 떨어뜨린 전리품(아이템 또는 골드). 플레이어가 가까이 오면 저절로 주워진다.
    ///
    /// 생성: 몬스터가 죽을 때 LootDropper가 Dropped Loot 프리팹을 만들고 SetupItem 또는 SetupGold로 내용을 정한다.
    /// 동작: 제자리에서 위아래로 떠다니며 돌다가, 생성 후 pickupDelay가 지나면 플레이어와의 거리를 재서
    ///       pickupRadius 안이면 인벤토리(또는 소지금)에 넣고, 획득 알림·이펙트·소리를 낸 뒤 사라진다.
    ///       lifeTime 동안 아무도 줍지 않으면 그냥 사라진다.
    /// 아이콘 뒤에 외곽선 스프라이트를 하나 더 그려 종류(골드/물약/재료/장비 등급)를 색으로 구분한다.
    /// </summary>
    public class DroppedLoot : MonoBehaviour
    {
        [Header("아이콘")]
        [Tooltip("아이템 아이콘을 그릴 스프라이트 렌더러")]
        [SerializeField] private SpriteRenderer iconRenderer;
        [Tooltip("아이콘과 같은 위치·스프라이트를 'RPGGame/Sprite Outline' 셰이더로 그려 가장자리에만 색을 남기는 렌더러")]
        [SerializeField] private SpriteRenderer outlineRenderer;

        [Header("종류별 외곽선 색 (장비는 등급 색을 쓴다)")]
        [Tooltip("골드")]
        [SerializeField] private Color currencyColor = new Color(1f, 0.85f, 0.2f);
        [Tooltip("체력 물약")]
        [SerializeField] private Color potionColor = new Color(1f, 0.25f, 0.25f);
        [Tooltip("강화석 등 재료")]
        [SerializeField] private Color materialColor = new Color(0.3f, 0.9f, 0.35f);

        [Header("획득")]
        [Tooltip("이 거리(m, 높이 차이 무시) 안에 플레이어가 들어오면 주워진다")]
        [SerializeField] private float pickupRadius = 1.3f;
        [Tooltip("떨어진 뒤 이 시간(초)이 지나야 주울 수 있다. 튀어나오는 모습을 볼 시간을 준다")]
        [SerializeField] private float pickupDelay = 0.5f;
        [Tooltip("아무도 줍지 않으면 이 시간(초) 뒤에 사라진다")]
        [SerializeField] private float lifeTime = 60f;

        [Header("연출")]
        [Tooltip("위아래로 떠다니는 높이(m)")]
        [SerializeField] private float bobHeight = 0.08f;
        [Tooltip("위아래로 떠다니는 빠르기")]
        [SerializeField] private float bobSpeed = 2f;
        [Tooltip("초당 회전 각도(도)")]
        [SerializeField] private float spinSpeed = 45f;
        [Tooltip("주웠을 때 그 자리에 띄울 이펙트")]
        [SerializeField] private GameObject pickupEffectPrefab;
        [Tooltip("획득 이펙트를 지우기까지의 시간(초)")]
        [SerializeField] private float pickupEffectLifeTime = 2f;

        [Header("획득 소리")]
        [Tooltip("장비를 주웠을 때")]
        [SerializeField] private AudioClip equipmentPickupSound;
        [Tooltip("물약·재료·골드를 주웠을 때")]
        [SerializeField] private AudioClip miscPickupSound;
        [SerializeField, Range(0f, 1f)] private float pickupSoundVolume = 0.8f;

        // 아이템 드랍이면 item이, 골드 드랍이면 goldAmount가 채워진다
        private Item item;
        private int count = 1;
        private int goldAmount;

        private Transform refTransform;
        private Transform playerTransform;
        private float spawnTime;
        // 떠다니는 연출의 기준 높이 (생성 위치)
        private float baseHeight;
        private bool isCollected;

        private void Awake()
        {
            refTransform = transform;
            spawnTime = Time.time;
            baseHeight = refTransform.position.y;
        }

        // ───────────────────────── 초기화 (LootDropper가 호출) ─────────────────────────

        /// <summary>아이템 드랍으로 만든다.</summary>
        public void SetupItem(Item droppedItem, int droppedCount)
        {
            item = droppedItem;
            count = Mathf.Max(1, droppedCount);
            goldAmount = 0;
            ApplyIcon(droppedItem != null ? droppedItem.sprite : null);
            ApplyOutlineColor(GetOutlineColor(droppedItem));
        }

        /// <summary>골드 드랍으로 만든다.</summary>
        public void SetupGold(int amount, Sprite icon)
        {
            item = null;
            goldAmount = Mathf.Max(1, amount);
            ApplyIcon(icon);
            ApplyOutlineColor(currencyColor);
        }

        // 물약·재료는 종류 색으로, 장비는 인벤토리와 같은 등급 색으로 구분한다
        private Color GetOutlineColor(Item droppedItem)
        {
            if (droppedItem is HealthItem)
            {
                return potionColor;
            }

            if (droppedItem is MaterialItem)
            {
                return materialColor;
            }

            return droppedItem != null ? droppedItem.GradeColor : Item.GetGradeColor(Item.Grade.Common);
        }

        private void ApplyIcon(Sprite icon)
        {
            if (icon == null)
            {
                return;
            }

            if (iconRenderer != null)
            {
                iconRenderer.sprite = icon;
            }

            // 외곽선도 아이콘과 같은 모양이어야 하므로 같은 스프라이트를 넣는다
            if (outlineRenderer != null)
            {
                outlineRenderer.sprite = icon;
            }
        }

        private void ApplyOutlineColor(Color color)
        {
            if (outlineRenderer != null)
            {
                outlineRenderer.color = color;
            }
        }

        // ───────────────────────── 매 프레임 ─────────────────────────

        private void Update()
        {
            if (isCollected)
            {
                return;
            }

            float elapsed = Time.time - spawnTime;

            if (elapsed > lifeTime)
            {
                Destroy(gameObject);
                return;
            }

            // 위아래로 살짝 떠다니며 회전하는 연출
            Vector3 position = refTransform.position;
            position.y = baseHeight + Mathf.Sin(elapsed * bobSpeed) * bobHeight;
            refTransform.position = position;
            refTransform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

            if (elapsed < pickupDelay || !TryGetPlayer())
            {
                return;
            }

            // 획득 판정은 트리거 콜라이더 대신 거리 비교로 한다.
            // 드랍 개수가 많지 않아 비용이 작고, CharacterController와 트리거의 조합보다 동작이 확실하다.
            // 높이 차이는 무시한다 (경사면에서 플레이어와 드랍의 높이가 달라 안 주워지는 것을 막는다)
            Vector3 delta = playerTransform.position - position;
            delta.y = 0f;

            if (delta.sqrMagnitude <= pickupRadius * pickupRadius)
            {
                Collect();
            }
        }

        // 플레이어를 한 번 찾아 기억해 둔다
        private bool TryGetPlayer()
        {
            if (playerTransform != null)
            {
                return true;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                return false;
            }

            playerTransform = player.transform;
            return true;
        }

        // ───────────────────────── 획득 ─────────────────────────

        private void Collect()
        {
            isCollected = true;

            // 획득 안내는 확인 창 대신 화면 구석의 짧은 알림(UIPickupFeed)으로만 보여 준다.
            // 전투 중 드랍을 밟을 때마다 창이 뜨면 시야를 가리기 때문이다
            if (item != null)
            {
                // 장비는 한 개씩 스탯을 굴려 개체로 들어가고, 나머지는 개수만큼 쌓인다
                InventoryManager.Instance?.AddItem(item, count);

                UIPickupFeed.ShowItem(item, count);
            }
            else if (goldAmount > 0)
            {
                CurrencyManager.Instance?.AddGold(goldAmount);
                UIPickupFeed.ShowGold(goldAmount);
            }

            if (pickupEffectPrefab != null)
            {
                GameObject effect = Instantiate(pickupEffectPrefab, refTransform.position, Quaternion.identity);
                Destroy(effect, pickupEffectLifeTime);
            }

            // 이 오브젝트는 곧바로 파괴되므로 자기 AudioSource로는 소리가 끊긴다.
            // 소리만 재생하고 저절로 사라지는 임시 오브젝트를 만드는 PlayClipAtPoint를 쓴다
            AudioClip pickupSound = item is EquipmentItem ? equipmentPickupSound : miscPickupSound;
            if (pickupSound != null)
            {
                AudioSource.PlayClipAtPoint(pickupSound, refTransform.position, pickupSoundVolume);
            }

            Destroy(gameObject);
        }
    }
}
