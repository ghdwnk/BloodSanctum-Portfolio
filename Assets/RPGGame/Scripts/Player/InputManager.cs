using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RPGGame
{
    /// <summary>
    /// 키보드·마우스 입력을 매 프레임 읽어 정적 프로퍼티로 공개하는 컴포넌트. 게임 씬에 하나만 둔다.
    ///
    /// 입력은 새 Input System의 프로젝트 액션(Assets/InputSystem_Actions)에서 이름으로 찾는다.
    /// 다른 스크립트는 InputManager.IsAttack처럼 값만 읽으면 되고, 액션을 직접 다루지 않는다.
    /// 실행 순서를 -1로 앞당겨, 같은 프레임의 다른 Update가 항상 이번 프레임 입력을 읽게 한다.
    ///
    /// 입력을 막는 경우 (모든 값을 비운다):
    ///  - IsBlocked가 켜진 동안 (봉인 왕좌 연출 등 컷씬)
    ///  - 텔레포트 로딩 화면, 상점 수량 입력 창이 떠 있는 동안
    ///  - 보스 대사 중 (대사를 넘기는 스페이스만 남긴다)
    ///
    /// 스페이스바 하나가 "대화 넘기기"와 "대쉬"를 함께 맡는다. 대화창이 입력을 쓴 프레임에는 ConsumeDash로 대쉬를 끈다.
    /// 구르기는 미완성이라 스페이스에 연결하지 않았다 (IsRoll은 항상 false).
    /// </summary>
    [DefaultExecutionOrder(-1)]
    public class InputManager : MonoBehaviour
    {
        /// <summary>퀵슬롯 칸 수 (Q, E, R, 1, 2, 3).</summary>
        public const int QuickSlotKeyCount = 6;

        // ───────────────────────── 공개 입력 값 ─────────────────────────

        /// <summary>컷씬 연출 중 모든 게임 입력을 막는다. 연출이 끝나면 반드시 false로 되돌려야 한다.</summary>
        public static bool IsBlocked { get; set; }

        /// <summary>이동 입력 (WASD). x = 좌우, y = 앞뒤.</summary>
        public static Vector2 Movement { get; private set; }
        /// <summary>마우스 이동량 (카메라 회전).</summary>
        public static Vector2 MouseMove { get; private set; }

        /// <summary>구르기. 미완성이라 항상 false다.</summary>
        public static bool IsRoll { get; private set; }
        /// <summary>대화창 넘기기 (스페이스).</summary>
        public static bool IsDialogueSkip { get; private set; }
        /// <summary>대쉬 (스페이스). 대화창이 같은 프레임에 스페이스를 쓰면 ConsumeDash로 꺼진다.</summary>
        public static bool IsDash { get; private set; }
        /// <summary>기본 공격 (마우스 왼쪽). UI 위에서 누르면 false.</summary>
        public static bool IsAttack { get; private set; }
        /// <summary>ESC. 열린 창을 닫거나 환경설정을 연다.</summary>
        public static bool IsCancel { get; private set; }
        /// <summary>F. NPC·게이트 등과 상호작용.</summary>
        public static bool IsInteract { get; private set; }

        /// <summary>I: 인벤토리 창.</summary>
        public static bool IsInventory { get; private set; }
        /// <summary>J: 퀘스트 창.</summary>
        public static bool IsQuest { get; private set; }
        /// <summary>K: 스킬 창.</summary>
        public static bool IsSkill { get; private set; }
        /// <summary>U: 캐릭터 정보 창.</summary>
        public static bool IsCharacterStat { get; private set; }

        // 퀵슬롯 키(Q, E, R, 1, 2, 3)가 이번 프레임에 눌렸는지 (퀵슬롯 사용). IsQuickSlotPressed로 읽는다
        private static readonly bool[] quickSlotPressed = new bool[QuickSlotKeyCount];

        /// <summary>퀵슬롯 index번(0부터) 키가 이번 프레임에 눌렸는지.</summary>
        public static bool IsQuickSlotPressed(int index)
        {
            return index >= 0 && index < QuickSlotKeyCount && quickSlotPressed[index];
        }

        // ───────────────────────── 입력 액션 ─────────────────────────

        private InputAction moveAction;
        // 스페이스. 액션 이름은 에셋에 저장된 "Jump" 그대로 쓴다 (이름을 바꾸면 에셋에 저장되지 않는 문제가 있었다)
        private InputAction spaceAction;
        private InputAction attackAction;
        private InputAction lookAction;
        private InputAction cancelAction;
        private InputAction interactAction;
        private InputAction inventoryAction;
        private InputAction questAction;
        private InputAction skillWindowAction;
        private InputAction characterStatAction;
        private readonly InputAction[] quickSlotActions = new InputAction[QuickSlotKeyCount];

        private void Awake()
        {
            InputActionAsset actions = InputSystem.actions;

            moveAction = actions.FindAction("Move");
            spaceAction = actions.FindAction("Jump");
            attackAction = actions.FindAction("Attack");
            lookAction = actions.FindAction("Look");
            cancelAction = actions.FindAction("Cancel");
            interactAction = actions.FindAction("Interact");
            inventoryAction = actions.FindAction("Inventory");
            questAction = actions.FindAction("Quest");
            skillWindowAction = actions.FindAction("Skill");
            characterStatAction = actions.FindAction("CharacterStat");

            for (int ix = 0; ix < QuickSlotKeyCount; ++ix)
            {
                quickSlotActions[ix] = actions.FindAction($"Skill{ix + 1}");
            }
        }

        private void OnEnable()
        {
            // 마우스 커서가 게임 창 밖으로 나가지 않게 가둔다
            Cursor.lockState = CursorLockMode.Confined;
        }

        /// <summary>이번 프레임의 스페이스 입력을 대화창이 이미 썼으니 대쉬로 쓰지 말라고 알린다.</summary>
        public static void ConsumeDash()
        {
            IsDash = false;
        }

        private void Update()
        {
            if (IsBlocked || UITeleportScreen.IsOn || UIQuantityDialog.IsOn)
            {
                // 텔레포트 화면이 가린 채로 이동하면 발소리가 나고,
                // 수량 창에 숫자를 칠 때 퀵슬롯이 발동하거나 I·J·K·U 창이 열리므로 모두 막는다
                ClearInput();
                return;
            }

            // 보스 대사 중(조우 대사·페이즈 전환 연출)에는 조작을 막되,
            // 대사를 넘기는 스페이스는 남겨 둬야 조우 대사를 닫고 전투를 시작할 수 있다
            if (BossStateManager.IsBlockingPlayerInput)
            {
                bool dialogueSkip = spaceAction.WasPressedThisFrame();
                ClearInput();
                IsDialogueSkip = dialogueSkip;
                return;
            }

            Movement = moveAction.ReadValue<Vector2>();
            MouseMove = lookAction.ReadValue<Vector2>();

            // 스페이스는 대화 넘기기 + 대쉬. 구르기는 미완성이라 연결하지 않는다
            // (구르기를 완성하면 IsRoll = spaceAction.WasPressedThisFrame()으로 되돌리고 대화창 처리만 추가하면 된다)
            IsRoll = false;
            IsDialogueSkip = spaceAction.WasPressedThisFrame();
            IsDash = IsDialogueSkip;

            IsCancel = cancelAction.WasPressedThisFrame();
            IsInteract = interactAction.WasPressedThisFrame();
            IsInventory = inventoryAction.WasPressedThisFrame();
            IsQuest = questAction.WasPressedThisFrame();
            IsSkill = skillWindowAction.WasPressedThisFrame();
            IsCharacterStat = characterStatAction.WasPressedThisFrame();

            for (int ix = 0; ix < QuickSlotKeyCount; ++ix)
            {
                quickSlotPressed[ix] = quickSlotActions[ix].WasPressedThisFrame();
            }

            // 마우스가 UI 위에 있으면 클릭은 UI의 몫이다 (창을 누르다 공격이 나가지 않게)
            bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            IsAttack = !pointerOverUI && attackAction.WasPressedThisFrame();
        }

        // 모든 입력 값을 비운다. 값이 정적이라 직접 비우지 않으면 마지막 프레임 값이 남는다
        private static void ClearInput()
        {
            Movement = Vector2.zero;
            MouseMove = Vector2.zero;
            IsRoll = false;
            IsDialogueSkip = false;
            IsDash = false;
            IsAttack = false;
            IsCancel = false;
            IsInteract = false;
            IsInventory = false;
            IsQuest = false;
            IsSkill = false;
            IsCharacterStat = false;
            System.Array.Clear(quickSlotPressed, 0, quickSlotPressed.Length);
        }
    }
}
