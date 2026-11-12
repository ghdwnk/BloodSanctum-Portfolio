using TMPro;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 보유 골드를 표시하는 UI. 인벤토리 창 하단에 붙는다.
    /// </summary>
    public class UIGoldDisplay : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI amountText;

        private void OnEnable()
        {
            // 창이 닫혀 있는 동안 바뀐 값과, 이벤트를 발행하지 않는 LoadGold(세이브 복원)까지
            // 반영하려면 열릴 때마다 현재 값을 직접 읽어야 한다
            Refresh(CurrencyManager.Instance != null ? CurrencyManager.Instance.Gold : 0);
        }

        private void Start()
        {
            // 구독은 한 번만 한다. OnEnable에서 하면 창을 여닫을 때마다 리스너가 쌓인다
            CurrencyManager.Instance?.SubscribeOnGoldChanged(Refresh);
        }

        private void Refresh(int gold)
        {
            if (amountText != null)
            {
                // 천 단위 구분 기호를 넣어 큰 금액도 읽기 쉽게 한다
                amountText.text = gold.ToString("N0");
            }
        }
    }
}
