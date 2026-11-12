using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 인게임에서 ESC(Cancel) 입력을 처리한다. ESC를 누를 때마다 위에서부터 차례로 하나씩 처리한다:
    ///  1. 키 가이드가 떠 있으면 그것만 닫는다
    ///  2. 환경설정 패널이 열려 있으면 패널을 닫는다
    ///  3. 수량 입력창·시스템 메시지는 스스로 ESC를 처리하므로 건드리지 않는다
    ///  4. 인벤토리·퀘스트·스킬 같은 창이 열려 있으면 연 순서대로 하나씩 닫는다 (UIWindowStack)
    ///  5. 열린 창이 하나도 없으면 환경설정 패널을 연다
    /// 패널이 열려있는 동안에는 Time.timeScale을 0으로 만들어 게임을 일시정지한다.
    /// </summary>
    public class UIEscMenuController : MonoBehaviour
    {
        [SerializeField] private UIOptionsPanel optionsPanel;

        private bool isOpen;

        private void Update()
        {
            // 창이 열린 순서를 매 프레임 따라가야 ESC를 누르는 순간 올바른 창이 맨 위로 잡힌다
            UIWindowStack.Refresh();

            if (!InputManager.IsCancel)
            {
                return;
            }

            // 키 가이드가 떠 있으면 먼저 그것만 닫는다 (환경설정으로 돌아가기)
            if (UIKeyGuideWindow.TryClose())
            {
                return;
            }

            // 환경설정이 열려 있으면 ESC는 그것을 닫는 데 쓴다
            if (isOpen)
            {
                Close();
                return;
            }

            // 수량 입력창과 시스템 메시지는 ESC를 스스로 처리한다. 같은 ESC로 뒤의 창까지 닫히거나 환경설정이 뜨면 안 된다.
            // 실행 순서에 따라 이 컨트롤러보다 먼저 닫혔을 수도 있어 "이번 프레임에 이미 썼는지"도 함께 본다
            if (UIQuantityDialog.IsOn || UISystemMessage.IsOn || UIWindowStack.EscapeConsumedThisFrame)
            {
                return;
            }

            // 열린 창이 있으면 하나만 닫는다. 다 닫힌 뒤의 다음 ESC부터 환경설정이 열린다
            if (UIWindowStack.TryCloseNext())
            {
                return;
            }

            OpenPanel();
        }

        private void Toggle()
        {
            if (isOpen)
            {
                Close();
            }
            else
            {
                OpenPanel();
            }
        }

        private void OpenPanel()
        {
            isOpen = true;
            optionsPanel.Open();
            Time.timeScale = 0f;
        }

        /// <summary>
        /// 패널 내부의 '닫기' 버튼에서도 호출할 수 있도록 공개 메서드로 제공한다.
        /// </summary>
        public void Close()
        {
            isOpen = false;
            optionsPanel.Close();
            Time.timeScale = 1f;
        }
    }
}
