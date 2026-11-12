using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// NPC 상태(대기/대화)가 바뀔 때 애니메이터의 State 정수 파라미터를 맞춰 주는 컴포넌트.
    ///
    /// NPC 프리팹 루트 아래 "Animation Controller" 자식에 붙어 있고, 루트의 Animator와 NPCStateManager를 찾아 쓴다.
    /// 애니메이터 컨트롤러는 State 값(0 = 대기, 1 = 대화)으로 대기 모션과 대화 모션을 오간다.
    /// </summary>
    public class NPCAnimationController : MonoBehaviour
    {
        private static readonly int StateHash = Animator.StringToHash("State");

        private Animator refAnimator;

        private void Awake()
        {
            refAnimator = GetComponentInParent<Animator>();

            NPCStateManager stateManager = GetComponentInParent<NPCStateManager>();
            if (stateManager != null)
            {
                // 같은 오브젝트에서 두 번 구독하지 않도록 OnEnable이 아니라 Awake에서 한 번만 등록한다
                stateManager.SubscribeOnStateChanged(OnStateChanged);
            }
        }

        private void OnStateChanged(NPCStateManager.State state)
        {
            if (refAnimator != null)
            {
                refAnimator.SetInteger(StateHash, (int)state);
            }
        }
    }
}
