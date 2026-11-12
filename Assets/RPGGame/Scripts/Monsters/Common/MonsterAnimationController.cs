using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 몬스터 상태와 피격을 애니메이터에 전달하는 컴포넌트. 몬스터 프리팹의 자식(Animation Controller)에 붙어 있다.
    ///
    /// - 상태가 바뀌면 애니메이터의 State 값을 바꾼다 (MonsterStateManager 이벤트를 코드로 구독)
    /// - 맞으면 Hit 트리거로 피격 모션을 재생한다 (Damageable.OnDamageReceived에 OnDamaged가 인스펙터로 연결돼 있다)
    ///   단, 공격 모션을 끊지 않도록 공격 중에는 재생하지 않는다 (준비 동작 중에만 끊을 수 있다)
    /// </summary>
    public class MonsterAnimationController : MonoBehaviour
    {
        private static readonly int StateHash = Animator.StringToHash("State");
        private static readonly int HitHash = Animator.StringToHash("Hit");

        [Header("공격 준비 동작 끊기")]
        [Tooltip("타격 판정이 나가기 전(준비 동작)에 맞으면 공격이 끊기고 피격 모션이 재생된다")]
        [SerializeField] private bool canBeInterruptedWhileWindingUp = true;
        [Tooltip("한 번 끊긴 뒤 이 시간(초) 동안은 다시 끊기지 않는다. 없으면 계속 때리는 것만으로 몬스터가 영원히 공격하지 못한다")]
        [SerializeField] private float interruptCooldown = 3f;

        private Animator refAnimator;
        private MonsterStateManager manager;
        private MonsterAttackState attackState;
        private float nextInterruptTime;

        private void Awake()
        {
            if (refAnimator == null)
            {
                refAnimator = GetComponentInParent<Animator>();
            }

            manager = GetComponentInParent<MonsterStateManager>();
            attackState = GetComponentInParent<MonsterAttackState>();
            if (manager != null)
            {
                manager.SubscribeOnStateChanged(OnStateChanged);
            }
        }

        /// <summary>상태가 바뀔 때 (MonsterStateManager에서 코드로 구독한다).</summary>
        public void OnStateChanged(MonsterStateManager.State state)
        {
            if (refAnimator == null)
            {
                return;
            }

            refAnimator.SetInteger(StateHash, (int)state);

            if (state == MonsterStateManager.State.Dead)
            {
                // 트리거는 그 트리거를 쓰는 전환이 실제로 일어나기 전까지 계속 남아 있다.
                // 남은 Hit 트리거를 지우지 않으면 사망 직후 Any State -> Hit 전환이 발동해
                // 사망 애니메이션이 피격 모션으로 덮어써지고 다시 Idle로 돌아가 버린다
                ResetAllTriggers();
            }
        }

        /// <summary>맞았을 때 (Damageable.OnDamageReceived에 인스펙터로 연결돼 있다. 이름을 바꾸지 않는다).</summary>
        public void OnDamaged()
        {
            if (refAnimator == null)
            {
                return;
            }

            // 이미 죽은 몬스터의 피격은 무시한다.
            // 치명타로 죽는 마지막 일격도 여기에 해당한다 (HPController의 사망 처리가 먼저 실행되므로
            // 이 시점에는 이미 Dead 상태다)
            if (manager != null && manager.IsDead)
            {
                return;
            }

            // 공격 중에는 피격 모션을 재생하지 않는다.
            // Hit 트리거는 Any State -> Hit 전환을 타기 때문에 공격 모션을 그 자리에서 끊고,
            // 끊긴 공격은 AttackBegin/AttackEnd 애니메이션 이벤트가 실행되지 않아 판정이 아예 나가지 않는다.
            // 결과적으로 플레이어가 계속 때리기만 하면 몬스터는 한 번도 공격하지 못했다.
            // 피해 자체는 HPController가 별도로 받으므로 체력은 정상적으로 깎이고, 죽으면 위의 IsDead 경로로 빠진다
            if (manager != null && manager.IsAttacking)
            {
                // 예외: 아직 판정이 나가기 전의 준비 동작이면 끊는다 (선딜을 때려서 끊는 맛).
                // 판정이 시작된 뒤에는 끊지 않는다 - AttackEnd 이벤트가 안 와서 판정이 켜진 채 남기 때문
                bool isWindingUp = attackState != null && !attackState.IsSwinging;
                if (!canBeInterruptedWhileWindingUp || !isWindingUp || Time.time < nextInterruptTime)
                {
                    return;
                }

                nextInterruptTime = Time.time + interruptCooldown;
            }

            refAnimator.SetTrigger(HitHash);
        }

        // 대기 중인 트리거를 모두 지우는 함수.
        // 파라미터 이름을 하드코딩하지 않고 훑는 이유는, 몬스터마다 컨트롤러의 트리거 구성이 달라도
        // 이 코드를 고치지 않아도 되게 하기 위함
        private void ResetAllTriggers()
        {
            foreach (AnimatorControllerParameter parameter in refAnimator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    refAnimator.ResetTrigger(parameter.nameHash);
                }
            }
        }
    }
}
