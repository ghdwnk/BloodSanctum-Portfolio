using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 페이즈 전환 상태 (기획서 13-3절).
    /// 현재 행동을 중단하고 연출을 재생하며, 연출이 끝나고 페이즈 대사 창이 닫히면 전투를 재개한다
    /// </summary>
    public class BossPhaseTransitionState : BossStateBase
    {
        private float stateEndTime;
        // 페이즈 대사를 대화창에 띄웠는지. 띄웠으면 플레이어가 창을 닫을 때까지 전투를 재개하지 않는다
        private bool waitingForDialogue;

        protected override void OnEnable()
        {
            base.OnEnable();

            // 진행 중이던 패턴 코루틴을 모두 멈춘다 (전환 중에 이전 페이즈의 공격이 터지면 안 된다)
            manager.StopAllCoroutines();

            waitingForDialogue = manager.PlayPhaseTransitionEffect();

            BossData.Phase phase = manager.CurrentPhase;
            stateEndTime = Time.time + (phase != null ? phase.transitionDuration : 0f);
        }

        protected override void Update()
        {
            base.Update();

            // 연출 동안에는 아무 행동도 하지 않는다
            if (Time.time < stateEndTime)
            {
                return;
            }

            // 페이즈 대사가 떠 있는 동안은 전투를 재개하지 않는다. 플레이어가 창을 닫으면 이어서 싸운다
            if (waitingForDialogue && Dialogue.IsOpen)
            {
                return;
            }

            manager.SetState(BossStateManager.State.Chase);
        }
    }
}
