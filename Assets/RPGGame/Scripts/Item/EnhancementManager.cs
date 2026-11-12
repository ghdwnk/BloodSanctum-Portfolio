using UnityEngine;
using UnityEngine.Events;

namespace RPGGame
{
    /// <summary>
    /// 장비 강화를 처리하는 관리자. 게임 씬의 매니저 오브젝트에 하나만 둔다.
    ///
    /// 강화 단계는 장비 개체(EquipmentInstance.enhanceLevel)가 직접 들고 있어서, 같은 장비를 여러 개 가져도 따로 강화된다.
    /// 이 관리자는 규칙(EnhancementData)을 들고 "강화할 수 있는지 / 시도 / 결과 알림"만 맡는다.
    /// 강화 창(UIEnhanceWindow)은 표시만 하고, 재료·골드 차감과 확률 판정은 전부 여기서 한다 (ShopController와 같은 구조).
    ///
    /// 알림 두 가지:
    ///  - SubscribeOnEnhancementChanged : 강화를 시도할 때마다 (강화 창·장비 스탯·무기 강화 빛 갱신)
    ///  - OnEnhanceAttempted(정적 이벤트) : 시도한 장비와 성공 여부 (튜토리얼 퀘스트의 "1회 강화" 판정)
    /// </summary>
    [DefaultExecutionOrder(-45)]
    public class EnhancementManager : MonoBehaviour
    {
        private static EnhancementManager instance;
        public static EnhancementManager Instance => instance;

        /// <summary>강화를 시도할 때마다 발행한다 (장비 개체, 성공 여부).</summary>
        public static event System.Action<EquipmentInstance, bool> OnEnhanceAttempted;

        [Tooltip("강화 규칙 에셋 (Data/Enhancement/Enhancement Data)")]
        [SerializeField] private EnhancementData data;

        [Header("효과음")]
        [Tooltip("강화에 실패했을 때 나는 소리 (비워 두면 소리 없음). 성공음은 퀘스트 완료음(UISound.PlayQuestClear)을 쓴다")]
        [SerializeField] private AudioClip failSound;
        [Tooltip("실패 소리의 음량 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float failSoundVolume = 0.8f;

        // 강화 결과가 나올 때마다 발행. 코드에서만 구독하므로 인스펙터에 노출하지 않는다
        private readonly UnityEvent onEnhancementChanged = new UnityEvent();

        public EnhancementData Data => data;

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

        public void SubscribeOnEnhancementChanged(UnityAction listener)
        {
            onEnhancementChanged.AddListener(listener);
        }

        // ───────────────────────── 조회 ─────────────────────────

        /// <summary>장비의 현재 강화 단계. 장비가 없으면 0.</summary>
        public int GetLevel(EquipmentInstance item)
        {
            return item != null ? item.enhanceLevel : 0;
        }

        public bool IsMaxLevel(EquipmentInstance item)
        {
            return GetLevel(item) >= EnhancementData.FixedMaxLevel;
        }

        /// <summary>다음 단계로 올릴 때의 비용·확률. 최대 단계이거나 규칙이 없으면 null.</summary>
        public EnhancementData.Step GetNextStep(EquipmentInstance item)
        {
            if (data == null || IsMaxLevel(item))
            {
                return null;
            }

            return data.GetStep(GetLevel(item));
        }

        /// <summary>
        /// 강화 단계에 따른 스탯 배율. 관리자가 없는 씬(로비 등)에서도 1을 돌려줘 안전하게 부를 수 있다.
        /// PlayerEquipment·툴팁이 계산할 때마다 null 검사를 반복하지 않도록 여기로 모았다.
        /// </summary>
        public static float GetStatMultiplier(EquipmentInstance item)
        {
            if (instance == null || instance.data == null || item == null)
            {
                return 1f;
            }

            return instance.data.GetStatMultiplier(item.enhanceLevel);
        }

        /// <summary>이름 뒤에 강화 단계를 노란색으로 붙인 표시용 이름 (+0이면 이름만). colored면 이름에 등급 색을 입힌다.</summary>
        public static string GetDisplayName(EquipmentInstance item, bool colored = false)
        {
            string baseName = colored ? item.item.ColoredName : item.item.itemName;

            return item.enhanceLevel > 0 ? $"{baseName} <color=#FFD24A>+{item.enhanceLevel}</color>" : baseName;
        }

        // ───────────────────────── 강화 ─────────────────────────

        /// <summary>
        /// 지금 강화할 수 있는지. 강화 창이 버튼 활성화와 안내 문구에 그대로 쓴다.
        /// </summary>
        /// <param name="reason">강화할 수 없을 때 그 이유 (화면에 그대로 보여 준다)</param>
        public bool CanEnhance(EquipmentInstance item, out string reason)
        {
            reason = string.Empty;

            if (item == null || data == null)
            {
                reason = "강화할 수 없는 장비입니다.";
                return false;
            }

            if (IsMaxLevel(item))
            {
                reason = "이미 최대 단계까지 강화했습니다.";
                return false;
            }

            EnhancementData.Step step = GetNextStep(item);
            if (step == null)
            {
                reason = "강화 규칙이 설정되지 않았습니다.";
                return false;
            }

            if (data.materialItem == null)
            {
                reason = "강화 재료가 설정되지 않았습니다.";
                return false;
            }

            if (InventoryManager.Instance == null ||
                InventoryManager.Instance.GetItemCount(data.materialItem) < step.materialCount)
            {
                reason = $"{data.materialItem.itemName}이(가) 부족합니다.";
                return false;
            }

            if (CurrencyManager.Instance == null || CurrencyManager.Instance.Gold < step.goldCost)
            {
                reason = "재화가 부족합니다.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 강화를 시도한다. 성공하면 단계가 1 오르고, 실패해도 단계가 내려가거나 장비가 사라지지는 않는다.
        /// 재료와 골드는 성공 여부와 상관없이 소모된다.
        /// </summary>
        /// <param name="success">강화에 성공했는지</param>
        /// <param name="message">결과 문구 (예: "강철 대검 +3", "강철 대검 +2 유지") 또는 시도하지 못한 이유</param>
        /// <returns>시도 자체가 이루어졌으면 true. 조건이 모자라 시도조차 못 했으면 false</returns>
        public bool TryEnhance(EquipmentInstance item, out bool success, out string message)
        {
            success = false;

            if (!CanEnhance(item, out message))
            {
                return false;
            }

            EnhancementData.Step step = GetNextStep(item);

            // 재료·골드가 둘 다 충분한지는 CanEnhance에서 이미 확인했으므로 바로 차감한다
            // (하나를 먼저 빼고 다른 하나가 모자라 되돌리는 경우가 생기지 않는다)
            InventoryManager.Instance.RemoveItem(data.materialItem, step.materialCount);
            CurrencyManager.Instance.TrySpendGold(step.goldCost);

            int before = GetLevel(item);
            success = Random.value < step.successRate;

            if (success)
            {
                item.enhanceLevel = before + 1;
                message = $"{item.item.itemName} +{before + 1}";
            }
            else
            {
                message = $"{item.item.itemName} +{before} 유지";
            }

            // 성공은 퀘스트 완료음, 실패는 전용 소리. 같은 프레임에 퀘스트 완료음이 또 불려도 UISound가 한 번만 낸다
            // (튜토리얼 퀘스트의 "1회 강화"가 이 성공으로 완료되는 경우)
            if (success)
            {
                UISound.PlayQuestClear();
            }
            else
            {
                SfxPlayer.Play(failSound, failSoundVolume);
            }

            // 실패해도 재료와 골드가 줄었으므로 인벤토리·강화 창은 항상 다시 그려야 한다
            InventoryManager.Instance.NotifyItemListChanged();
            onEnhancementChanged.Invoke();
            OnEnhanceAttempted?.Invoke(item, success);

            return true;
        }
    }
}
