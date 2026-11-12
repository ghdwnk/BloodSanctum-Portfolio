using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 몬스터 추격 상태. 플레이어 쪽으로 몸을 돌리고(이동은 달리기 애니메이션의 루트 모션이 한다),
    /// 공격 사거리에 들어오면 공격 상태로 넘어간다.
    ///
    /// 동시에 플레이어를 때릴 수 있는 수에 제한이 있어(공격 슬롯), 자리를 못 잡은 몬스터는
    /// 사거리보다 한 겹 바깥에서 멈춰 서서 플레이어를 둘러싸는 모양이 된다.
    /// </summary>
    public class MonsterChaseState : MonsterStateBase
    {
        protected override void Update()
        {
            base.Update();

            // 플레이어가 죽었거나 안전지대로 들어갔으면 추격을 멈춘다
            if (manager.IsPlayerUntargetable)
            {
                manager.SetState(MonsterStateManager.State.Idle);
                return;
            }

            UpdateAttackSlot();
            TurnTowardsPlayer();
        }

        // 공격 슬롯을 잡았으면 사거리까지 계속 접근해 공격하고, 못 잡았으면 대기 거리에서 멈춘다
        private void UpdateAttackSlot()
        {
            float distance = Vector3.Distance(refTransform.position, manager.PlayerTransform.position);

            // 자리를 못 잡은 몬스터가 멈춰 서는 거리. 공격 사거리에서 멈추면 이미 서로 겹친 뒤라서
            // 한 겹 바깥에서 줄을 세운다
            float holdDistance = data.attackRange + data.separationRadius * 1.5f;

            if (distance <= holdDistance && manager.TryClaimAttackSlot())
            {
                manager.ShouldHoldPosition = false;

                if (distance <= data.attackRange)
                {
                    manager.SetState(MonsterStateManager.State.Attack);
                }

                return;
            }

            // 자리가 없으면 대기 거리 안으로 더 들어가지 않는다.
            // 회전과 서로 밀어내기는 그대로 동작해 플레이어를 둘러싸는 모양이 된다
            manager.ShouldHoldPosition = distance <= holdDistance;

            // 대기 거리 밖으로 밀려났으면 쥐고 있던 자리를 돌려준다
            if (distance > holdDistance)
            {
                manager.ReleaseAttackSlot();
            }
        }

        // 플레이어 쪽으로 서서히 돈다 (시야 판정이 급격한 회전에 흔들리지 않게 RotateTowards를 쓴다)
        private void TurnTowardsPlayer()
        {
            Vector3 direction = manager.PlayerTransform.position - refTransform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                refTransform.rotation = Quaternion.RotateTowards(refTransform.rotation,
                    Quaternion.LookRotation(direction), data.chaseRotateSpeed * Time.deltaTime);
            }
        }
    }
}
