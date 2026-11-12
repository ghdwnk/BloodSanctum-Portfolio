using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 장소마다 하나씩 놓는 텔레포트 포털. 앞에서 F키를 누르면 이동 목록 창(UITeleportWindow)을 연다.
    /// 가고 싶은 곳은 창에서 고르므로, 이 컴포넌트는 "누가 범위에 있는가"와 "여기가 어느 장소인가"만 안다.
    ///
    /// 마을에는 포털 1개(던전 5곳 목록), 각 던전에도 포털 1개(마을 + 나머지 던전 목록)를 둔다.
    /// 창은 포털이 열었으므로, 플레이어가 범위 밖으로 벗어나면 이 포털이 창을 닫는다
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TeleportPortal : MonoBehaviour, IInteractable
    {
        [Tooltip("이 포털이 있는 장소의 id (TeleportNetwork 목록의 id와 같은 값). 이 장소는 목록에서 빠진다")]
        [SerializeField] private string locationId;
        [Tooltip("F키 안내 프롬프트 (NPC·봉인 왕좌와 같은 컴포넌트)")]
        [SerializeField] private InteractionPromptView promptView;

        // 도착 직후 그 자리의 포털이 곧바로 반응하는 것을 막는 유예 시간.
        // 도착 지점 옆에 포털을 두는 구성을 허용하기 위한 안전장치
        private const float ArrivalGraceTime = 1.5f;

        private PlayerStateManager playerInRange;

        public string LocationId => locationId;

        // 이동 연출 중이거나 도착 직후라 포털이 반응하면 안 되는 상태
        private bool IsInTransition => UITeleportScreen.IsOn
                                       || Time.time < UITeleportScreen.LastTeleportEndTime + ArrivalGraceTime;

        public bool CanInteract => playerInRange != null
                                   && !playerInRange.IsPlayerDead
                                   && !IsInTransition;

        public string InteractionPrompt => "F : 이동하기";

        // 트리거 퇴장 이벤트가 오지 않아도(순간이동·CharacterController 껐다 켜기) 범위를 벗어난 것으로 보는 거리 (m의 제곱)
        private float sqrOutOfRangeDistance;

        private void Awake()
        {
            Collider trigger = GetComponent<Collider>();
            // 트리거가 아니면 플레이어가 포털에 막혀 버린다
            trigger.isTrigger = true;

            // 트리거 반지름 + 여유. 구형이 아니면 경계 상자의 가장 큰 변으로 어림한다
            SphereCollider sphere = trigger as SphereCollider;
            Vector3 scale = transform.lossyScale;
            float radius = sphere != null
                ? sphere.radius * Mathf.Max(scale.x, scale.y, scale.z)
                : trigger.bounds.extents.magnitude;
            float outOfRange = radius + 1.5f;
            sqrOutOfRangeDistance = outOfRange * outOfRange;
        }

        private void Update()
        {
            // 순간이동 등으로 트리거 퇴장 이벤트가 빠지면 멀리 있는 포털이 계속 F키를 받게 된다.
            // 그래서 매 프레임 거리를 한 번 더 확인해 범위 밖이면 범위 안 표시를 푼다
            if (playerInRange != null
                && (playerInRange.transform.position - transform.position).sqrMagnitude > sqrOutOfRangeDistance)
            {
                playerInRange = null;
                promptView?.Hide();
            }

            // 이 포털이 연 창은 플레이어가 범위를 벗어나거나 이동이 시작되면 닫는다
            if (UITeleportWindow.OpenedBy == this && (playerInRange == null || IsInTransition || playerInRange.IsPlayerDead))
            {
                UITeleportWindow.Close();
            }

            if (playerInRange == null)
            {
                return;
            }

            // 창이 떠 있는 동안과 이동 연출 중에는 프롬프트를 숨긴다
            if (!CanInteract || UITeleportWindow.IsOn)
            {
                promptView?.Hide();
                return;
            }

            promptView?.Show(InteractionPrompt);

            if (InputManager.IsInteract)
            {
                Interact();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // 태그가 아니라 컴포넌트로 플레이어를 판별한다.
            // "Player" 태그는 모델 자식(Kachujin)에 붙어 있고, 트리거에 실제로 들어오는 것은
            // 루트의 CharacterController(태그 없음)라서 태그 비교로는 걸러진다
            PlayerStateManager stateManager = other.GetComponentInParent<PlayerStateManager>();
            if (stateManager != null)
            {
                playerInRange = stateManager;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<PlayerStateManager>() != null)
            {
                playerInRange = null;
                promptView?.Hide();
            }
        }

        public void Interact()
        {
            if (!CanInteract)
            {
                return;
            }

            UITeleportWindow.Show(this, playerInRange.transform);
            promptView?.Hide();
        }

        private void OnDisable()
        {
            playerInRange = null;
            promptView?.Hide();

            if (UITeleportWindow.OpenedBy == this)
            {
                UITeleportWindow.Close();
            }
        }
    }
}
