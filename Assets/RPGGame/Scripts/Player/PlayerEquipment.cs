using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RPGGame
{
    /// <summary>
    /// 무기·갑옷·투구 3부위의 장착 상태를 관리한다. 플레이어 루트에 붙어 있다.
    ///
    /// 장착 구성이 바뀔 때마다 세 부위의 보정치를 모두 더해 PlayerStats.SetEquipmentBonus로 한 번에 넘긴다.
    ///  - 공격력·방어력·최대 HP: 개체가 굴린 값 x 강화 배율
    ///  - 치명타·쿨타임·속도: 장비 에셋 값 그대로 (배율을 곱하면 금방 상한을 넘어서 강화·랜덤 대상에서 뺐다)
    /// 부활 장비(reviveOnDeath)를 입고 있으면 체력이 0이 되는 순간 한 번 되살린다 (HPController.DeathInterceptor).
    ///
    /// 장착한 개체도 인벤토리 목록에 그대로 남아 있다 (인벤토리 창에서 해제·교체할 수 있게).
    /// 실행 순서 -55: WeaponController 등이 Start에서 장착 상태를 읽을 때 이미 기본 장비가 들어가 있게 한다.
    /// </summary>
    [DefaultExecutionOrder(-55)]
    public class PlayerEquipment : MonoBehaviour
    {
        // 부위 수는 EquipType 개수로 고정되므로 배열로 잡는다 (부위 번호로 바로 찾는다)
        private static readonly int SlotCount = System.Enum.GetValues(typeof(EquipmentItem.EquipType)).Length;

        [Tooltip("새 게임을 시작할 때 장착하고 인벤토리에도 넣어 주는 기본 장비")]
        [SerializeField] private EquipmentItem[] startingEquipment;

        // 부위별로 장착 중인 장비 개체 (EquipType 순서). 개체는 인벤토리가 들고 있다
        private readonly EquipmentInstance[] equipped = new EquipmentInstance[SlotCount];

        // 장착 구성이 바뀔 때마다 발행 (장비 패널·인벤토리 창·무기 모델 갱신). 코드에서만 구독한다
        private readonly UnityEvent onEquipmentChanged = new UnityEvent();

        private PlayerStats playerStats;
        private HPController hpController;
        // 이번 "목숨"에서 부활을 이미 썼는지. 장비를 벗었다 입어도 초기화되지 않고, 마을에서 부활할 때 초기화된다
        private bool reviveUsed;

        private void Awake()
        {
            playerStats = GetComponent<PlayerStats>();
            hpController = GetComponentInChildren<HPController>();

            if (hpController != null)
            {
                hpController.DeathInterceptor = TryReviveOnDeath;
            }
        }

        // 체력이 0이 되는 순간 호출된다. 부활 장비를 입고 있고 아직 안 썼으면 그 자리에서 체력을 채우고 true를 반환한다
        private bool TryReviveOnDeath()
        {
            if (reviveUsed)
            {
                return false;
            }

            EquipmentItem reviveItem = null;
            foreach (EquipmentInstance instance in equipped)
            {
                if (instance != null && instance.item.reviveOnDeath)
                {
                    reviveItem = instance.item;
                    break;
                }
            }

            if (reviveItem == null)
            {
                return false;
            }

            reviveUsed = true;

            // OnHealed를 쓰면 회복 연출(PlayerHealEffect)도 함께 나온다. 이 시점 체력은 0이다
            hpController.OnHealed(hpController.MaxHP * reviveItem.reviveHPRatio);

            UISystemMessage.ShowToast($"{reviveItem.ColoredName}의 힘으로 쓰러지지 않았다! (체력 {reviveItem.reviveHPRatio * 100f:0}%)");

            return true;
        }

        // 마을에서 부활했을 때 호출: 다음 사망에서 다시 부활 효과를 쓸 수 있게 한다
        public void ResetRevive()
        {
            reviveUsed = false;
        }

        private void Start()
        {
            // 이어하기로 로드된 장비가 이미 있으면 기본 장비를 덮어쓰지 않는다
            if (!HasAnyEquipped())
            {
                EquipStartingItems();
            }

            // 강화 수치가 바뀌면 보정치를 다시 합산해야 한다.
            // 관리자의 Awake는 모든 Start보다 먼저 돌기 때문에 여기서 구독하면 놓치지 않는다
            EnhancementManager.Instance?.SubscribeOnEnhancementChanged(ApplyBonuses);

            // PlayerStats가 레벨 데이터를 잡은 뒤에 장비 보정을 밀어넣는다
            ApplyBonuses();

            // 장비의 최대 HP 보정이 반영된 뒤 한 번 가득 채운다.
            // ApplyBonuses는 전투 중 장비 교체를 고려해 현재 HP를 유지하므로,
            // 게임 시작 시점에는 여기서 따로 채워줘야 한다
            playerStats?.RefillToMax();
        }

        private bool HasAnyEquipped()
        {
            foreach (EquipmentInstance instance in equipped)
            {
                if (instance != null)
                {
                    return true;
                }
            }

            return false;
        }

        // 기본 장비를 장착하고 인벤토리에도 등록하는 함수.
        // 장착된 장비도 인벤토리에 남아 있어야 스탯창에서 해제할 수 있다
        private void EquipStartingItems()
        {
            if (startingEquipment == null)
            {
                return;
            }

            foreach (EquipmentItem item in startingEquipment)
            {
                if (item == null)
                {
                    continue;
                }

                // 기본 지급 장비는 랜덤 없이 원본 스탯 그대로 지급한다
                EquipmentInstance instance = EquipmentInstance.Create(item, false);
                equipped[(int)item.equipType] = instance;

                InventoryManager.Instance?.AddEquipment(instance);
            }

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.NotifyItemListChanged();
            }
        }

        // 해당 부위에 장착된 아이템을 반환하는 함수 (없으면 null)
        public EquipmentInstance GetEquipped(EquipmentItem.EquipType type)
        {
            return equipped[(int)type];
        }

        // 같은 종류 장비가 여러 개여도 "이 개체"가 장착 중일 때만 true다 (참조 비교)
        public bool IsEquipped(EquipmentInstance instance)
        {
            return instance != null && instance.item != null && equipped[(int)instance.item.equipType] == instance;
        }

        // 우클릭 등으로 장착/해제를 전환하는 함수
        public void ToggleEquip(EquipmentInstance instance)
        {
            if (instance == null || instance.item == null)
            {
                return;
            }

            if (IsEquipped(instance))
            {
                Unequip(instance.item.equipType);
            }
            else
            {
                Equip(instance);
            }
        }

        // 아이템을 장착하는 함수. 같은 부위에 이미 장착된 아이템은 자동으로 교체된다
        public void Equip(EquipmentInstance instance)
        {
            if (instance == null || instance.item == null)
            {
                return;
            }

            EquipmentInstance previous = equipped[(int)instance.item.equipType];
            equipped[(int)instance.item.equipType] = instance;

            // 교체해 입는 경우, 벗겨진 장비가 입은 장비의 인벤토리 칸을 그대로 이어받도록 목록 위치를 맞바꾼다.
            // (장착 중인 개체는 창에서 숨겨지므로, 안 바꾸면 벗은 장비가 옛 획득 순서 자리로 가서 칸이 밀려 보인다)
            if (previous != null && previous != instance)
            {
                InventoryManager.Instance?.SwapEquipmentPositions(instance, previous);
            }

            ApplyBonuses();

            // 간결하게 "OO을(를) 장착했습니다"만 보여준다 (교체되는 장비 해제 안내는 생략)
            string message = $"{instance.item.itemName}{Josa(instance.item.itemName, "을", "를")} 장착했습니다";
            // 아이템 획득 알림과 같은 줄(화면 좌측 하단)에 쌓아 보여준다
            UIPickupFeed.ShowMessage(message);
        }

        // 마지막 글자의 받침 유무로 을/를 같은 조사를 고른다 (한글이 아니면 받침 없음으로 본다)
        private static string Josa(string word, string withBatchim, string withoutBatchim)
        {
            if (string.IsNullOrEmpty(word))
            {
                return withoutBatchim;
            }

            char last = word[word.Length - 1];
            bool hasBatchim = last >= '\uAC00' && last <= '\uD7A3' && (last - '\uAC00') % 28 != 0;
            return hasBatchim ? withBatchim : withoutBatchim;
        }

        // 해당 부위의 장비를 해제하는 함수
        public void Unequip(EquipmentItem.EquipType type)
        {
            EquipmentInstance previous = equipped[(int)type];
            if (previous == null)
            {
                return;
            }

            equipped[(int)type] = null;

            ApplyBonuses();

            UIPickupFeed.ShowMessage($"{previous.item.itemName} 해제");
        }

        // 장착 중인 모든 장비의 보정치를 더해 PlayerStats에 넘긴다
        private void ApplyBonuses()
        {
            float attack = 0f, defense = 0f, maxHP = 0f;
            float critRate = 0f, critDamage = 0f, cooldownReduction = 0f, dashCooldownReduction = 0f, moveSpeed = 0f, attackSpeed = 0f;

            foreach (EquipmentInstance instance in equipped)
            {
                if (instance == null)
                {
                    continue;
                }

                // 개체가 굴린 스탯에 강화 배율을 곱한다. 나머지 수치는 에셋 값 그대로 더한다
                float multiplier = EnhancementManager.GetStatMultiplier(instance);

                attack += instance.attack * multiplier;
                defense += instance.defense * multiplier;
                maxHP += instance.maxHP * multiplier;
                critRate += instance.item.critRateBonus;
                critDamage += instance.item.critDamageBonus;
                cooldownReduction += instance.item.cooldownReductionBonus;
                dashCooldownReduction += instance.item.dashCooldownReductionBonus;
                moveSpeed += instance.item.moveSpeedBonus;
                attackSpeed += instance.item.attackSpeedBonus;
            }

            if (playerStats != null)
            {
                playerStats.SetEquipmentBonus(attack, defense, maxHP, critRate, critDamage, cooldownReduction, dashCooldownReduction,
                    moveSpeed, attackSpeed);
            }

            onEquipmentChanged.Invoke();
        }

        public void SubscribeOnEquipmentChanged(UnityAction listener)
        {
            onEquipmentChanged.AddListener(listener);
        }

        // 세이브용: 부위 순서대로 장착 개체의 ID를 반환한다 (빈 부위는 빈 문자열)
        public List<string> GetEquippedIds()
        {
            List<string> ids = new List<string>(SlotCount);
            foreach (EquipmentInstance instance in equipped)
            {
                ids.Add(instance != null ? instance.id : string.Empty);
            }

            return ids;
        }

        // 로드용: 인벤토리에 복원된 개체 중 ID가 같은 것을 찾아 장착 상태를 복원한다.
        // 인벤토리 복원이 먼저 끝나 있어야 한다
        public void LoadState(List<string> savedIds)
        {
            for (int ix = 0; ix < equipped.Length; ++ix)
            {
                equipped[ix] = null;
            }

            if (savedIds != null && InventoryManager.Instance != null)
            {
                foreach (string id in savedIds)
                {
                    EquipmentInstance instance = InventoryManager.Instance.FindEquipmentById(id);
                    if (instance != null)
                    {
                        equipped[(int)instance.item.equipType] = instance;
                    }
                }
            }

            ApplyBonuses();
        }
    }
}
