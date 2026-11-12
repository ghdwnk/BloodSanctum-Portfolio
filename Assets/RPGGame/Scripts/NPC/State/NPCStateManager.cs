using UnityEngine;
using UnityEngine.Events;

namespace RPGGame
{
    /// <summary>
    /// NPC 한 명의 중심 컴포넌트. NPC 프리팹(NPC1 Akai·NPC2 Arissa·NPC3 Paladin) 루트에 붙어 있다.
    ///
    /// 하는 일:
    ///  - 상태 전환: 대기(NPCIdleState) ↔ 대화(NPCTalkState). 지금 상태의 컴포넌트만 켜 둔다
    ///  - 상호작용: F키 안내 문구, 외곽선, 말을 걸었을 때 대화를 할지 상점·강화 창을 열지 결정
    ///  - 데이터: NPC Data 에셋에서 자기 번호(npcID)의 이름·역할·대화 거리·잡담을 꺼내 둔다
    ///
    /// 상인·대장장이는 service로 구분한다. 볼일(퀘스트 제안·완료 보고)이 있으면 대화가 먼저이고,
    /// 없거나 퀘스트 진행 중이면 F키로 바로 상점·강화 창을 연다.
    /// </summary>
    public class NPCStateManager : MonoBehaviour, IInteractable
    {
        /// <summary>NPC 상태. 정수 값이 애니메이터의 State 파라미터로 그대로 들어간다 (0 대기, 1 대화).</summary>
        public enum State { None = -1, Idle, Talk }

        /// <summary>NPC가 제공하는 기능. None이면 대화만 한다.</summary>
        public enum ServiceType { None, Shop, Enhance }

        [Tooltip("NPC 번호 (NPC Data 에셋의 순서, 1부터)")]
        [SerializeField] private int npcID;

        [Tooltip("머리 위 'F : 대화하기' 안내 UI")]
        [SerializeField] private InteractionPromptView promptView;

        [Tooltip("이 NPC가 여는 기능 창. Shop이면 상점, Enhance면 장비 강화")]
        [SerializeField] private ServiceType service = ServiceType.None;

        private State state = State.None;
        // State 값을 번호로 쓰는 상태 컴포넌트 표 (0 대기, 1 대화)
        private NPCStateBase[] states;
        private InteractionHighlight highlight;

        // 상태가 바뀔 때마다 발행. NPCAnimationController가 애니메이터에 전달한다
        private readonly UnityEvent<State> onStateChanged = new();

        public int NPCID => npcID;

        /// <summary>NPC Data 에셋에서 꺼낸 이 NPC의 정보.</summary>
        public NPCData.Attribute Data { get; private set; }

        /// <summary>플레이어 위치 (Player 태그가 붙은 오브젝트).</summary>
        public Transform PlayerTransform { get; private set; }

        /// <summary>현재 상태. 대화 중에는 머리 위 퀘스트 표식을 숨기는 데 쓴다.</summary>
        public State CurrentState => state;

        private void Awake()
        {
            PlayerTransform = GameObject.FindGameObjectWithTag("Player").transform;
            Data = DataManager.Instance.npcData.attributes[npcID - 1];

            // 머리 위 이름표에 "[역할] 이름"을 띄운다.
            // 인스펙터로 연결하지 않고 자식에서 찾아, 새 NPC 프리팹에도 컴포넌트만 붙이면 동작하게 한다
            UINPCNameTag nameTag = GetComponentInChildren<UINPCNameTag>(true);
            if (nameTag != null)
            {
                nameTag.SetInfo(Data.name, Data.role);
            }

            highlight = GetComponent<InteractionHighlight>();

            // 상태 컴포넌트는 모두 꺼 둔 채로 시작하고, OnEnable에서 대기 상태만 켠다
            states = new NPCStateBase[]
            {
                GetComponent<NPCIdleState>(),
                GetComponent<NPCTalkState>(),
            };

            foreach (NPCStateBase stateComponent in states)
            {
                stateComponent.enabled = false;
            }
        }

        private void OnEnable()
        {
            SetState(State.Idle);
        }

        // ───────────────────────── 상태 ─────────────────────────

        /// <summary>상태를 바꾼다. 이전 상태 컴포넌트를 끄고 새 상태 컴포넌트를 켠다.</summary>
        public void SetState(State newState)
        {
            if (state == newState)
            {
                return;
            }

            if (state != State.None)
            {
                states[(int)state].enabled = false;
            }

            states[(int)newState].enabled = true;
            state = newState;
            onStateChanged.Invoke(state);
        }

        public void SubscribeOnStateChanged(UnityAction<State> listener)
        {
            onStateChanged.AddListener(listener);
        }

        // ───────────────────────── 상호작용 (IInteractable) ─────────────────────────

        /// <summary>대기 중이고, 이 NPC의 기능 창이 이미 열려 있지 않을 때만 말을 걸 수 있다.</summary>
        public bool CanInteract => state == State.Idle && !IsServiceWindowOpen;

        /// <summary>머리 위 안내 문구. 지금 F키를 누르면 일어날 일을 보여 준다.</summary>
        public string InteractionPrompt
        {
            get
            {
                if (!ShouldOpenService)
                {
                    return "F : 대화하기";
                }

                return service == ServiceType.Shop ? "F : 상점 이용하기" : "F : 장비 강화하기";
            }
        }

        /// <summary>F키를 눌렀을 때 (NPCIdleState가 부른다). 기능 창을 열거나 대화 상태로 바꾼다.</summary>
        public void Interact()
        {
            if (!CanInteract)
            {
                return;
            }

            if (!ShouldOpenService)
            {
                SetState(State.Talk);
                return;
            }

            HidePrompt();

            if (service == ServiceType.Shop)
            {
                UIShopWindow.Show();
            }
            else
            {
                UIEnhanceWindow.Show();
            }

            // 이 NPC가 연 창이므로, 플레이어가 멀어지면 Update가 닫을 수 있게 표시해 둔다
            isServiceOpenedHere = true;
        }

        // 이 NPC가 기능 창(상점·강화)을 열어 둔 상태인지. 창을 연 NPC만 거리를 감시하려고 둔 표시다
        private bool isServiceOpenedHere;

        // 창을 연 뒤 플레이어가 대화 거리(interactionSight)보다 이만큼 더 멀어지면 창을 닫는다.
        // 경계선에서 한두 걸음만 흔들려도 창이 깜빡 닫히는 일을 막는 여유 거리(m)다
        private const float CloseDistanceMargin = 1f;

        // 열어 둔 창이 없으면 불리자마자 bool 한 번 검사하고 끝난다
        private void Update()
        {
            if (!isServiceOpenedHere)
            {
                return;
            }

            // 사용자가 닫기 버튼 등으로 이미 닫았으면 감시도 끝낸다
            if (!IsServiceWindowOpen)
            {
                isServiceOpenedHere = false;
                return;
            }

            float closeDistance = Data.interactionSight + CloseDistanceMargin;
            if ((PlayerTransform.position - transform.position).sqrMagnitude > closeDistance * closeDistance)
            {
                if (service == ServiceType.Shop)
                {
                    UIShopWindow.Close();
                }
                else
                {
                    UIEnhanceWindow.Close();
                }

                isServiceOpenedHere = false;
            }
        }

        private bool IsServiceWindowOpen =>
            (service == ServiceType.Shop && UIShopWindow.IsOn) ||
            (service == ServiceType.Enhance && UIEnhanceWindow.IsOn);

        // 지금 기능 창(상점·강화)을 열어야 하는지.
        // 퀘스트 제안(Offer)과 완료 보고(TurnIn)는 대화가 먼저다. 하지만 진행 중(Progress)까지 막으면
        // 퀘스트를 하는 동안 기능을 아예 못 쓰게 되므로 통과시킨다 (진행 상황은 퀘스트 HUD·퀘스트 창에서 볼 수 있다)
        private bool ShouldOpenService
        {
            get
            {
                if (service == ServiceType.None)
                {
                    return false;
                }

                if (QuestManager.Instance == null)
                {
                    return true;
                }

                QuestManager.NPCQuestInteraction.Kind kind = QuestManager.Instance.CheckNPCState(npcID).kind;

                return kind == QuestManager.NPCQuestInteraction.Kind.None ||
                       kind == QuestManager.NPCQuestInteraction.Kind.Progress;
            }
        }

        // ───────────────────────── 안내·외곽선 (상태 컴포넌트가 부른다) ─────────────────────────

        public void ShowPrompt()
        {
            if (promptView != null)
            {
                promptView.Show(InteractionPrompt);
            }
        }

        public void HidePrompt()
        {
            if (promptView != null)
            {
                promptView.Hide();
            }
        }

        /// <summary>상호작용 대상임을 알리는 외곽선을 켜고 끈다.</summary>
        public void SetHighlight(bool on)
        {
            if (highlight != null)
            {
                highlight.SetHighlight(on);
            }
        }
    }
}
