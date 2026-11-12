using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 공격 상태. 예약된 패턴을 한 번 실행하고, 패턴 길이만큼 대기한 뒤 추격으로 돌아간다
    /// </summary>
    public class BossAttackState : BossStateBase
    {
        private float stateEndTime;

        protected override void OnEnable()
        {
            base.OnEnable();

            BossPatternData pattern = manager.PendingPattern;

            if (pattern == null)
            {
                manager.SetState(BossStateManager.State.Idle);
                return;
            }

            // 패턴 시작 시점에 플레이어를 정면으로 바라보게 한다
            manager.RotateToPlayer(float.MaxValue);

            manager.ExecutePattern(pattern);

            stateEndTime = Time.time + pattern.duration;
        }

        protected override void Update()
        {
            base.Update();

            // 패턴 연출이 끝나면 다음 행동을 정하러 추격 상태로 돌아간다
            if (Time.time >= stateEndTime)
            {
                manager.SetState(BossStateManager.State.Chase);
            }
        }
    }
}
