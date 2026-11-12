using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 체력 물약(소모품). 사용하면 플레이어 체력을 회복하고 1개 줄어든다.
    ///
    /// 에셋: Potion_HP_Small(하급), Potion_HP_Large(상급). 상점·몬스터 드랍·퀘스트 보상으로 얻는다.
    /// 인벤토리 목록(UIInventoryListItem)이나 퀵슬롯(QuickSlotController)에서 Use()가 불린다.
    /// 모든 체력 물약은 쿨타임 하나를 같이 쓴다 (퀵슬롯 UI가 CooldownRemaining으로 남은 시간을 그린다).
    /// </summary>
    [CreateAssetMenu(fileName = "New Health Item", menuName = "Inventory/Item/HealthItem")]
    public class HealthItem : Item
    {
        /// <summary>
        /// 체력 물약 공용 쿨타임(초).
        /// 종류별로 따로 두면 하급과 상급을 번갈아 마셔 쿨타임을 피할 수 있어서 정적 값 하나로 관리한다.
        /// </summary>
        public const float PotionCooldown = 3f;

        // 다음에 마실 수 있는 시각 (Time.time 기준). 모든 물약 에셋이 공유한다
        private static float nextUseTime;

        [Header("회복")]
        [Tooltip("한 번 마실 때 회복하는 체력")]
        public float healthAmount;

        [Tooltip("마셨을 때 잠깐 띄우는 안내 문구 (예: 체력을 60 회복했습니다.)")]
        [TextArea(2, 15)] public string messageWhenUsed;

        /// <summary>남은 공용 쿨타임(초). 0이면 바로 마실 수 있다.</summary>
        public static float CooldownRemaining => Mathf.Max(0f, nextUseTime - Time.time);

        // 에디터에서 도메인 리로드 없이 플레이를 다시 시작해도 이전 플레이의 쿨타임이 남지 않게 한다
        // (Time.time은 플레이마다 0부터 다시 시작한다)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCooldown()
        {
            nextUseTime = 0f;
        }

        public override void Use()
        {
            // 쿨타임 중에는 물약을 소모하지 않고 안내만 한다
            if (CooldownRemaining > 0f)
            {
                UISystemMessage.ShowToastWarning($"체력물약은 {Mathf.CeilToInt(CooldownRemaining)}초 뒤에 사용할 수 있습니다.");
                return;
            }

            HPController playerHP = FindPlayerHP();
            if (playerHP == null)
            {
                return;
            }

            nextUseTime = Time.time + PotionCooldown;

            playerHP.OnHealed(healthAmount);
            UISound.PlayPotion();

            // 인벤토리에서 1개를 빼고 목록 갱신을 알린다
            InventoryManager.Instance.OnItemUsed(this);

            // 전투 중에도 마시므로 확인 버튼이 있는 창 대신 저절로 사라지는 토스트로 알린다
            UISystemMessage.ShowToast(messageWhenUsed);
        }

        // 플레이어의 HPController를 찾는다. 물약을 마실 때만 부르므로 캐시하지 않는다.
        // Player 태그는 플레이어 루트가 아니라 자식 오브젝트에 붙어 있어서 루트로 올라간 뒤 아래에서 찾는다
        private static HPController FindPlayerHP()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.transform.root.GetComponentInChildren<HPController>() : null;
        }
    }
}
