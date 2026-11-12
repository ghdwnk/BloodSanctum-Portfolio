using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 추격 상태. 플레이어를 바라보며 접근하다가 패턴 사거리에 들어오면 공격으로 전환한다
    /// </summary>
    public class BossChaseState : BossStateBase
    {
        protected override void Update()
        {
            base.Update();

            if (manager.IsPlayerUntargetable || manager.PlayerTransform == null)
            {
                manager.SetState(BossStateManager.State.Idle);
                return;
            }

            manager.RotateToPlayer(manager.Data.chaseRotateSpeed);

            float distance = manager.DistanceToPlayer;

            // 추격을 포기할 만큼 멀어지면 대기로 복귀 (먼 거리 순간이동 패턴이 있으면 그 거리까지는 계속 쫓는다)
            if (distance > manager.ChaseGiveUpRange)
            {
                manager.SetState(BossStateManager.State.Idle);
                return;
            }

            // 쓸 수 있는 패턴이 생기면 공격으로 전환
            if (manager.TryStartAttack())
            {
                return;
            }

            // 아직 공격할 수 없으면 계속 접근한다.
            // 이동은 루트 모션으로 처리되므로 여기서는 데이터의 배율만 적용한다
            if (characterController != null && characterController.enabled && distance > manager.Data.ChaseStopDistance)
            {
                Vector3 direction = manager.PlayerTransform.position - refTransform.position;
                direction.y = 0f;
                direction.Normalize();

                characterController.Move(direction * (manager.Data.moveSpeed * Time.deltaTime));
            }
        }
    }
}
