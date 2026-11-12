using UnityEngine;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 체력바. 채워진 비율(fillAmount)과 "현재/최대" 숫자를 갱신한다. 플레이어 HUD와 몬스터 머리 위 체력바가 함께 쓴다.
    /// HPController.OnHPChanged에 OnDamageReceived가 인스펙터로 연결돼 있고(이름 유지), 몬스터가 죽으면 HideBar로 감춘다.
    /// </summary>
    public class HPBar : MonoBehaviour
    {
        [SerializeField] private Image hpBar;
        [Tooltip("체력바만 감추기 위한 표시용 루트(HP Bar BG). 이 컴포넌트가 붙은 오브젝트 자체를 끄면 같은 오브젝트에 있는 UIDamageText, UIBillboard까지 함께 꺼져버리므로 별도로 분리한다")]
        [SerializeField] private GameObject visualRoot;
        [Tooltip("플레이어 HUD처럼 \"480/500\" 형태로 숫자를 보여줘야 할 때만 연결한다. 몬스터 머리 위 체력바는 비워둬도 되게 null 허용")]
        [SerializeField] private TMPro.TextMeshProUGUI valueText;

        public void OnDamageReceived(float currentHP, float maxHP)
        {
            if (hpBar != null)
            {
                hpBar.fillAmount = currentHP / maxHP;
            }

            if (valueText != null)
            {
                valueText.text = $"{Mathf.CeilToInt(currentHP)}/{Mathf.CeilToInt(maxHP)}";
            }
        }

        // 시체 위에 체력바가 남아있지 않도록 감추는 함수. gameObject 자체는 끄지 않는다
        public void HideBar()
        {
            if (visualRoot != null)
            {
                visualRoot.SetActive(false);
            }
        }
    }
}
