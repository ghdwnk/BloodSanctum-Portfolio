using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 플레이어 애니메이터에 값을 넣는 창구. 플레이어 루트 아래 자식에 붙어 있고 부모의 Animator를 쓴다.
    ///
    /// - OnStateChanged : PlayerStateManager.OnStateChanged에 인스펙터로 연결돼 State 파라미터를 바꾼다
    /// - SetAttackComboState : 다음 콤보 단계(AttackCombo 파라미터)
    /// - PlayTrigger : 스킬 애니메이션 트리거(Skill1~Skill4)
    /// - ApplyAnimationSpeed : 상태에 맞춰 재생 속도(공격 속도·둔화)를 정한다
    /// </summary>
    public class PlayerAnimationController : MonoBehaviour
    {
        // 애니메이터가 아예 멈춰 버리지 않도록 하는 최소 재생 속도
        private const float MinAnimatorSpeed = 0.1f;

        private static readonly int StateHash = Animator.StringToHash("State");
        private static readonly int AttackComboHash = Animator.StringToHash("AttackCombo");

        [Tooltip("플레이어 애니메이터. 비워 두면 부모에서 찾는다")]
        [SerializeField] private Animator refAnimator;

        // 지금 콤보 단계가 공격속도 보너스를 몇 배로 받는지 (PlayerAttackComboSMB가 콤보에 들어올 때 정한다)
        private float comboAttackSpeedBonusScale = 1f;

        private PlayerStats playerStats;
        private PlayerSlowEffect slowEffect;
        private PlayerStateManager.State currentState = PlayerStateManager.State.None;

        private void OnEnable()
        {
            if (refAnimator == null)
            {
                refAnimator = GetComponentInParent<Animator>();
            }
        }

        /// <summary>상태가 바뀔 때 (PlayerStateManager.OnStateChanged에 인스펙터로 연결돼 있다. 이름을 바꾸지 않는다).</summary>
        public void OnStateChanged(PlayerStateManager.State newState)
        {
            if (refAnimator == null)
            {
                return;
            }

            refAnimator.SetInteger(StateHash, (int)newState);

            currentState = newState;
            ApplyAnimationSpeed();
        }

        /// <summary>
        /// 상태에 따라 애니메이터 재생 속도를 정한다.
        ///  - 공격: 판정·콤보가 모두 애니메이션 이벤트로 돌아가므로, 속도를 올리면 같은 동작이 더 짧게 끝나 딜레이만 줄어든다
        ///  - 이동: 둔화가 걸려 있으면 이동속도 배율만큼 느리게 달린다 (이동은 느린데 발만 빠르면 미끄러져 보인다)
        ///  - 그 밖: 원래 속도. 스킬·대쉬는 시전할 때 대기 상태로 내려가므로 여기에 걸리지 않는다
        /// 상태가 바뀔 때, 장비가 바뀔 때, 둔화가 걸리고 풀릴 때만 부르면 되고 매 프레임 갱신하지 않는다.
        /// </summary>
        public void ApplyAnimationSpeed()
        {
            if (refAnimator == null)
            {
                return;
            }

            if (playerStats == null)
            {
                playerStats = GetComponentInParent<PlayerStats>();

                // 공격 도중 장비를 바꿔도 곧바로 반영되게 한다 (처음 찾을 때 한 번만 구독)
                if (playerStats != null)
                {
                    playerStats.SubscribeOnStatsChanged(ApplyAnimationSpeed);
                }
            }

            float speed = 1f;

            if (currentState == PlayerStateManager.State.Attack)
            {
                // 공격속도 보너스(배율 - 1)에 콤보별 증폭을 곱한다. 증폭이 2인 3타는 보너스 20%가 40%로 적용돼
                // 선딜(찌르기까지)과 후딜(찌른 뒤)이 1·2타보다 더 크게 줄어든다
                float bonus = playerStats != null ? playerStats.AttackSpeedMultiplier - 1f : 0f;
                speed = 1f + bonus * comboAttackSpeedBonusScale;
            }
            else if (currentState == PlayerStateManager.State.Move)
            {
                // 둔화 컴포넌트는 처음 맞을 때 런타임에 붙으므로 없으면 다시 찾는다
                if (slowEffect == null)
                {
                    slowEffect = GetComponentInParent<PlayerSlowEffect>();
                }

                speed = slowEffect != null ? slowEffect.MoveSpeedMultiplier : 1f;
            }

            refAnimator.speed = Mathf.Max(MinAnimatorSpeed, speed);
        }

        /// <summary>콤보 단계가 공격속도 보너스를 몇 배로 받을지 정하고 바로 재생 속도에 반영한다.</summary>
        public void SetComboAttackSpeedBonusScale(float scale)
        {
            comboAttackSpeedBonusScale = Mathf.Max(0f, scale);
            ApplyAnimationSpeed();
        }

        /// <summary>지금 애니메이터 재생 속도 (코루틴 지연 시간을 애니메이션 속도에 맞출 때 쓴다).</summary>
        public float CurrentAnimatorSpeed => refAnimator != null ? refAnimator.speed : 1f;

        public void SetAttackComboState(int attackCombo)
        {
            refAnimator.SetInteger(AttackComboHash, attackCombo);
        }

        public AnimatorStateInfo GetCurrentStateInfo()
        {
            return refAnimator.GetCurrentAnimatorStateInfo(0);
        }

        /// <summary>스킬 등 트리거를 재생한다. 같은 이름의 트리거 파라미터가 애니메이터에 있어야 한다.</summary>
        public void PlayTrigger(string triggerName)
        {
            if (refAnimator == null || string.IsNullOrEmpty(triggerName))
            {
                return;
            }

            refAnimator.SetTrigger(triggerName);
        }
    }
}
