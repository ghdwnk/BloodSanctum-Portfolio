using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 인게임에서 마우스 커서의 표시 여부를 관리한다.
    /// 캐릭터를 조작하는 동안에는 커서를 감추고(카메라가 마우스 이동량으로 회전하므로 Locked),
    /// 인벤토리·퀘스트·스킬·상태창·옵션·퀘스트 수락 대화가 열리면 다시 보여준다.
    /// 이 컴포넌트가 없는 씬(로비)은 기본값 그대로 커서가 보인다.
    /// </summary>
    public class CursorController : MonoBehaviour
    {
        // 마지막으로 적용한 상태. 매 프레임 Cursor에 값을 다시 쓰지 않도록 비교용으로 들고 있다
        private bool? isCursorShown;

        private void OnEnable()
        {
            Apply(UIWindowState.IsAnyWindowOpen);
        }

        private void Update()
        {
            Apply(UIWindowState.IsAnyWindowOpen);
        }

        private void OnDisable()
        {
            // 로비로 돌아가거나 씬이 바뀔 때 커서가 잠긴 채로 남지 않게 되돌린다
            isCursorShown = null;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.Confined;
        }

        private void Apply(bool showCursor)
        {
            if (isCursorShown == showCursor)
            {
                return;
            }

            isCursorShown = showCursor;
            Cursor.visible = showCursor;
            // Locked는 커서를 화면 중앙에 고정하고 숨긴다. 창을 열었을 때는 게임뷰 밖으로
            // 나가지 않도록 Confined를 쓴다
            Cursor.lockState = showCursor ? CursorLockMode.Confined : CursorLockMode.Locked;
        }
    }
}
