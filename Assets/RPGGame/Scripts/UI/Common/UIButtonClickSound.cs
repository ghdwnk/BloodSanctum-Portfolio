using UnityEngine;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 버튼이 눌리면 클릭음을 내는 표식 컴포넌트. UISound.AttachClick이 버튼에 붙여 준다.
    /// 비활성(interactable = false) 버튼은 Button이 onClick을 부르지 않으므로 소리도 나지 않는다
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class UIButtonClickSound : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(UISound.PlayClick);
        }
    }
}
