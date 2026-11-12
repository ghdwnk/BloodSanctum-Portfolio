using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 대기 상태. 플레이어가 감지 범위에 들어오면 추격하거나 바로 공격한다
    /// </summary>
    public class BossIdleState : BossStateBase
    {
        protected override void Update()
        {
            base.Update();

            // 조우 대사 처리는 거리·안전지대 검사보다 먼저 해야 한다.
            // 대사 도중 플레이어가 감지 범위 밖으로 밀려나거나 타깃 불가가 되면, 아래 검사에서 먼저 return 해
            // 대사 종료를 감지하지 못하고 IsInEncounterDialogue가 true로 남는다. 그러면 대사 창은 닫혔는데
            // 보스는 전투를 시작하지 않고 플레이어 입력 차단(IsBlockingPlayerInput)도 영원히 풀리지 않는다
            if (manager.IsInEncounterDialogue)
            {
                manager.RotateToPlayer(manager.Data.chaseRotateSpeed);

                // 플레이어가 마지막 페이지에서 스페이스로 창을 닫으면 대사를 끝낸다 (이후 거리가 맞으면 바로 전투)
                if (!Dialogue.IsOpen)
                {
                    manager.EndEncounterDialogue();
                }

                return;
            }

            if (manager.IsPlayerUntargetable || manager.PlayerTransform == null)
            {
                return;
            }

            float distance = manager.DistanceToPlayer;

            // 감지 범위 밖이면 계속 대기
            if (distance > manager.Data.detectionRange)
            {
                return;
            }

            // 처음 마주치면 조우 대사부터 보여준다. 대사가 떠 있는 동안은 플레이어를 바라보기만 하고 공격하지 않는다
            if (manager.ShouldPlayEncounter)
            {
                manager.BeginEncounterDialogue();
                return;
            }

            // 사거리가 맞는 패턴이 있으면 즉시 공격, 없으면 접근한다
            if (!manager.TryStartAttack())
            {
                manager.SetState(BossStateManager.State.Chase);
            }
        }
    }
}
