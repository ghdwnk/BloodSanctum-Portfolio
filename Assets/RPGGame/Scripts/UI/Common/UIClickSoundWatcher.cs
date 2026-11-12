using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections.Generic;

namespace RPGGame
{
    /// <summary>
    /// 게임 어디서든 UI 버튼을 누르면 클릭음을 내는 전역 감시자.
    /// 버튼마다 컴포넌트를 붙이는 방식(UIButtonClickSound)은 창마다 AttachClick을 불러야 하고, 상점 목록처럼
    /// 나중에 생기는 버튼은 빠뜨리기 쉽다. 대신 마우스를 떼는 순간 포인터 밑의 버튼을 직접 찾아 소리를 내므로
    /// 새 버튼이나 새 창을 만들어도 따로 할 일이 없다.
    /// 게임 시작 때 한 번 만들어져 씬이 바뀌어도 남는다
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class UIClickSoundWatcher : MonoBehaviour
    {
        private static UIClickSoundWatcher instance;

        private readonly List<RaycastResult> results = new List<RaycastResult>();

        // 마우스를 누른 순간 포인터 밑에 있던 버튼. 누른 버튼과 뗀 버튼이 같을 때만 클릭으로 친다
        // (버튼 위에서 눌렀다가 밖에서 떼면 유니티도 클릭으로 처리하지 않는다)
        private Button pressedButton;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null)
            {
                return;
            }

            GameObject host = new GameObject("[UIClickSoundWatcher]");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<UIClickSoundWatcher>();
        }

        // 실행 순서를 EventSystem(-1000)보다 앞으로 둔 이유: 버튼을 눌러 창이 닫히거나 버튼이 꺼지면
        // EventSystem이 클릭을 처리한 뒤에는 포인터 밑에서 그 버튼을 더 이상 찾을 수 없다.
        // 그 전에 먼저 확인해야 닫기 버튼 같은 것도 소리가 난다
        private void Update()
        {
            Mouse mouse = Mouse.current;
            EventSystem eventSystem = EventSystem.current;

            if (mouse == null || eventSystem == null)
            {
                pressedButton = null;
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                pressedButton = FindButtonUnderPointer(eventSystem, mouse.position.ReadValue());
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                Button released = FindButtonUnderPointer(eventSystem, mouse.position.ReadValue());

                if (released != null && released == pressedButton)
                {
                    UISound.PlayClick();
                }

                pressedButton = null;
            }
        }

        // 포인터 밑에서 실제로 클릭을 받을 버튼을 찾는다. EventSystem과 같은 규칙으로, 위에서부터 보다가
        // 처음 만나는 클릭 처리기가 버튼이 아니면(다른 UI가 가리고 있으면) 그 뒤의 버튼은 무시한다.
        // 꺼져 있거나 눌러도 반응 없는(interactable 꺼짐) 버튼은 소리를 내지 않는다
        private Button FindButtonUnderPointer(EventSystem eventSystem, Vector2 position)
        {
            PointerEventData pointerData = new PointerEventData(eventSystem) { position = position };

            results.Clear();
            eventSystem.RaycastAll(pointerData, results);

            foreach (RaycastResult result in results)
            {
                GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(result.gameObject);
                if (handler == null)
                {
                    continue;
                }

                Button button = handler.GetComponent<Button>();

                // UIButtonClickSound가 이미 붙은 버튼은 스스로 소리를 내고, UIClickSoundSkip이 붙은 버튼은
                // 전용 소리가 따로 있으므로 둘 다 공용 클릭음이 겹쳐 울리지 않게 건너뛴다
                if (button != null && button.IsActive() && button.IsInteractable()
                    && button.GetComponent<UIButtonClickSound>() == null
                    && button.GetComponent<UIClickSoundSkip>() == null)
                {
                    return button;
                }

                return null;
            }

            return null;
        }
    }
}
