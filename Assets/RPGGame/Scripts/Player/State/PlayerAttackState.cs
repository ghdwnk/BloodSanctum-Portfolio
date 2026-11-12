using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace RPGGame
{
    /// <summary>
    /// 플레이어 기본 공격 상태. 공격 애니메이션 클립에 심어 둔 애니메이션 이벤트를 받아 무기 판정과 콤보를 진행한다.
    ///
    /// 애니메이션 이벤트 (클립에서 이름으로 부르므로 함수 이름을 바꾸지 않는다):
    ///  - AttackStart    : 무기 판정 시작 → Weapon.OnAttackBegin
    ///  - AttackCheckEnd : 무기 판정 끝   → Weapon.OnAttackEnd
    ///  - ComboCheck     : 예약된 다음 콤보 단계를 애니메이터에 넘긴다
    ///  - AttackEnd      : 공격 애니메이션 끝 → 대기 상태로
    /// </summary>
    public class PlayerAttackState : PlayerStateBase
    {
        [Tooltip("1타 동작이 끝난 뒤에도 이 시간(초) 동안은 대기 상태로 넘어가지 않고 마지막 자세로 기다린다. 이 안에 좌클릭하면 2타로 이어진다")]
        [SerializeField] private float firstComboInputGrace = 0f;

        private static readonly int Combo1Hash = Animator.StringToHash("AttackCombo1");
        private Coroutine pendingEnd;

        // 코드에서만 구독하므로 인스펙터에 노출하지 않는다 (WeaponController, PlayerStateManager가 구독)
        private readonly UnityEvent onAttackBegin = new UnityEvent();
        private readonly UnityEvent onAttackCheckEnd = new UnityEvent();
        private readonly UnityEvent onAttackEnd = new UnityEvent();

        public void SubscribeOnAttackBegin(UnityAction listener) => onAttackBegin.AddListener(listener);
        public void SubscribeOnAttackCheckEnd(UnityAction listener) => onAttackCheckEnd.AddListener(listener);
        public void SubscribeOnAttackEnd(UnityAction listener) => onAttackEnd.AddListener(listener);

        // ───── 애니메이션 이벤트 ─────

        private void AttackStart()
        {
            onAttackBegin.Invoke();
        }

        private void AttackCheckEnd()
        {
            onAttackCheckEnd.Invoke();
        }

        private void ComboCheck()
        {
            animationController.SetAttackComboState((int)manager.NextAttackCombo);
        }

        private void AttackEnd()
        {
            // 1타가 끝났을 때 곧바로 대기로 넘기지 않고, 잠깐 더 기다려 늦게 누른 클릭도 2타로 이어 준다
            if (animationController.GetCurrentStateInfo().shortNameHash == Combo1Hash)
            {
                // 2타가 이미 예약돼 애니메이터가 넘어가는 중이면 이 종료 이벤트는 무시한다 (블렌드 도중 Idle로 끊기는 것 방지)
                if (manager.NextAttackCombo >= PlayerStateManager.AttackCombo.Combo2)
                {
                    return;
                }

                if (firstComboInputGrace > 0f && pendingEnd == null)
                {
                    pendingEnd = StartCoroutine(EndAfterGrace());
                    return;
                }
            }

            FinishAttack();
        }

        private IEnumerator EndAfterGrace()
        {
            yield return new WaitForSeconds(firstComboInputGrace);
            pendingEnd = null;

            // 그 사이 클릭이 들어왔으면(2타 예약) 애니메이터가 2타로 넘어가므로 끝내지 않는다
            if (manager.NextAttackCombo < PlayerStateManager.AttackCombo.Combo2)
            {
                FinishAttack();
            }
        }

        private void FinishAttack()
        {
            if (pendingEnd != null)
            {
                StopCoroutine(pendingEnd);
                pendingEnd = null;
            }

            onAttackEnd.Invoke();
            manager.SetState(PlayerStateManager.State.Idle);
        }

        /// <summary>
        /// 공격을 도중에 끊는다 (대쉬·스킬). 애니메이션 이벤트(AttackCheckEnd/AttackEnd)가 오지 않아도
        /// 무기 판정을 끄고 콤보와 상태를 정리한다. 정리는 멱등이라 이벤트가 뒤늦게 와도 안전하다.
        /// </summary>
        public void CancelAttack()
        {
            onAttackCheckEnd.Invoke();
            FinishAttack();
        }
    }
}
