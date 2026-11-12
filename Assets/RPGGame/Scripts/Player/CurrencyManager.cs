using UnityEngine;
using UnityEngine.Events;

namespace RPGGame
{
    /// <summary>
    /// 골드 보유량을 관리하는 지갑. 게임 씬의 매니저 오브젝트에 하나만 둔다.
    ///
    /// 얻기: 몬스터 드랍·퀘스트 보상·상점 판매 (AddGold)
    /// 쓰기: 상점 구매·장비 강화 (TrySpendGold), 사망 페널티 (LoseGold)
    /// 골드 표시(UIGoldDisplay)와 상점·강화 창은 SubscribeOnGoldChanged로 변화를 구독한다.
    /// 세이브는 GameSaveLoader가 Gold / LoadGold로 주고받는다.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class CurrencyManager : MonoBehaviour
    {
        private static CurrencyManager instance;
        public static CurrencyManager Instance => instance;

        private int gold;

        // 골드가 바뀔 때마다 새 잔액과 함께 발행. 코드에서만 구독하므로 인스펙터에 노출하지 않는다
        private readonly UnityEvent<int> onGoldChanged = new UnityEvent<int>();

        public int Gold => gold;

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

        public void SubscribeOnGoldChanged(UnityAction<int> listener)
        {
            onGoldChanged.AddListener(listener);
        }

        /// <summary>골드를 더한다 (0 이하는 무시).</summary>
        public void AddGold(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            gold += amount;
            onGoldChanged.Invoke(gold);
        }

        /// <summary>골드를 쓴다. 잔액이 모자라면 아무것도 하지 않고 false를 돌려준다.</summary>
        public bool TrySpendGold(int amount)
        {
            if (amount <= 0 || gold < amount)
            {
                return false;
            }

            gold -= amount;
            onGoldChanged.Invoke(gold);
            return true;
        }

        /// <summary>사망 페널티 등으로 골드를 잃는다. 잔액보다 많이 빼려 하면 0까지만 줄이고, 실제로 잃은 양을 돌려준다.</summary>
        public int LoseGold(int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            int lost = Mathf.Min(gold, amount);
            gold -= lost;
            onGoldChanged.Invoke(gold);
            return lost;
        }

        /// <summary>골드를 정해진 값으로 맞춘다 (테스트 도구용). 표시가 갱신되도록 변경 알림을 보낸다.</summary>
        public void SetGold(int amount)
        {
            gold = Mathf.Max(0, amount);
            onGoldChanged.Invoke(gold);
        }

        /// <summary>세이브에서 골드를 되살린다. 불러오기 전용이라 변경 알림은 보내지 않는다.</summary>
        public void LoadGold(int savedGold)
        {
            gold = Mathf.Max(0, savedGold);
        }
    }
}
