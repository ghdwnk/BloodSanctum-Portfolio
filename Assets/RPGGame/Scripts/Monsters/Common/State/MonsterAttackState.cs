using UnityEngine;
using UnityEngine.Events;

namespace RPGGame
{
    /// <summary>
    /// 몬스터 공격 상태. 플레이어를 바라본 채 공격 애니메이션을 반복하고, 사거리를 벗어나면 추격으로 돌아간다.
    ///
    /// 공격 모션 클립에 심어 둔 애니메이션 이벤트(AttackBegin / AttackEnd)가 타격 구간을 알려 준다.
    /// OnAttackBegin / OnAttackEnd는 인스펙터에서 MonsterAttackController의 판정 시작·끝에 연결돼 있다.
    /// </summary>
    public class MonsterAttackState : MonsterStateBase
    {
        [Tooltip("타격 구간이 시작될 때 (MonsterAttackController.OnAttackBegin이 연결돼 있다)")]
        [SerializeField] private UnityEvent OnAttackBegin;
        [Tooltip("타격 구간이 끝날 때 (MonsterAttackController.OnAttackEnd가 연결돼 있다)")]
        [SerializeField] private UnityEvent OnAttackEnd;

        [Header("공격 확정")]
        [Tooltip("공격 모션이 한 번 끝나야 추격으로 돌아갈 수 있다. 사거리를 벗어나는 즉시 보내면 플레이어가 조금만 움직여도 " +
                 "타격 프레임에 닿기 전에 모션이 끊겨 판정이 나가지 않는다. 모션 한 바퀴가 이 시간(초)을 넘으면 확정을 풀고 거리로만 판단한다")]
        [SerializeField] private float maxCommitTime = 3f;
        [Tooltip("추격으로 돌아가는 거리 배율 (공격 사거리 x 이 값). 진입 거리와 다르게 두어 사거리 경계에서 떨리는 것을 막는다")]
        [SerializeField] private float exitRangeMultiplier = 1.3f;

        [Header("선딜 중 따라붙기")]
        [Tooltip("타격 전 준비 동작 중 플레이어가 사거리의 이 비율보다 멀어지면 조금씩 붙는다. " +
                 "걸어서 움직이는 플레이어는 맞고, 대쉬나 크게 이동하는 플레이어는 피할 수 있게 하는 값")]
        [SerializeField, Range(0.3f, 1f)] private float approachStartRatio = 0.8f;
        [Tooltip("따라붙는 속도(m/s). 추격 달리기보다 한참 느리게 둔다")]
        [SerializeField] private float approachSpeed = 1.5f;

        // 이번 공격 모션 한 바퀴가 시작된 시각. 들어올 때와 AttackEnd마다 갱신한다
        private float cycleStartTime;
        // AttackEnd를 받았지만 아직 이탈 여부를 판단하지 않았는지
        private bool cycleEnded;

        /// <summary>타격 판정이 나가는 구간(AttackBegin ~ AttackEnd)인지. 이 구간 전의 준비 동작에서만 피격으로 공격이 끊긴다.</summary>
        public bool IsSwinging { get; private set; }

        protected override void OnEnable()
        {
            base.OnEnable();

            IsSwinging = false;
            cycleEnded = false;
            cycleStartTime = Time.time;
        }

        protected override void Update()
        {
            base.Update();

            // 공격하다 플레이어가 죽었거나 안전지대로 들어갔으면 멈춘다
            if (manager.IsPlayerUntargetable)
            {
                manager.SetState(MonsterStateManager.State.Idle);
                return;
            }

            // 공격 상태에서는 천천히 도는 대신 한 번에 플레이어를 바라본다
            Vector3 direction = manager.PlayerTransform.position - refTransform.position;
            direction.y = 0f;
            bool hasDirection = direction.sqrMagnitude > 0.0001f;

            if (hasDirection)
            {
                refTransform.rotation = Quaternion.LookRotation(direction);
            }

            float distance = Vector3.Distance(refTransform.position, manager.PlayerTransform.position);
            float exitRange = data.attackRange * exitRangeMultiplier;

            // 모션 한 바퀴가 끝난 시점에만 이탈 여부를 판단한다. 아직 이탈 거리 안이면 다음 공격으로 이어 간다
            if (cycleEnded)
            {
                cycleEnded = false;

                if (distance > exitRange)
                {
                    manager.SetState(MonsterStateManager.State.Chase);
                    return;
                }
            }

            // 모션 끝 이벤트가 오지 않아도 영원히 갇히지 않게, 확정 시간이 지나면 거리로만 판단한다
            if (Time.time >= cycleStartTime + maxCommitTime && distance > exitRange)
            {
                manager.SetState(MonsterStateManager.State.Chase);
                return;
            }

            // 타격 전 준비 동작 중에는 멀어진 플레이어에게 조금씩 붙는다
            if (!IsSwinging && hasDirection && distance > data.attackRange * approachStartRatio
                && characterController != null && characterController.enabled)
            {
                characterController.Move(direction.normalized * (approachSpeed * Time.deltaTime));
            }
        }

        // ───── 애니메이션 이벤트 (클립에서 이름으로 부르므로 함수 이름을 바꾸지 않는다) ─────

        public void AttackBegin()
        {
            IsSwinging = true;
            OnAttackBegin?.Invoke();
        }

        public void AttackEnd()
        {
            IsSwinging = false;
            cycleEnded = true;
            cycleStartTime = Time.time;
            OnAttackEnd?.Invoke();
        }
    }
}
