using System.Collections;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 패턴: 연계 공격
    /// 다른 패턴 여러 개를 순서대로 이어서 쓴다 (예: 돌진 -> 충격파).
    /// 각 단계는 자기 애니메이션을 재생하고, 단계마다 플레이어를 다시 바라본 뒤 실행한다.
    /// 이 패턴의 duration은 모든 단계의 duration 합 이상으로 맞춰 두어야 공격 상태가 중간에 끝나지 않는다
    /// </summary>
    [CreateAssetMenu(fileName = "New Combo Pattern", menuName = "Scriptable Objects/Boss Pattern/Combo")]
    public class ComboPatternData : BossPatternData
    {
        [Header("연계")]
        public BossPatternData[] steps;

        public override void Execute(BossPatternContext context)
        {
            context.Caster.StartCoroutine(ComboRoutine(context));
        }

        private IEnumerator ComboRoutine(BossPatternContext context)
        {
            BossStateManager boss = context.Caster as BossStateManager;

            for (int ix = 0; ix < steps.Length; ++ix)
            {
                BossPatternData step = steps[ix];
                if (step == null)
                {
                    continue;
                }

                // 첫 단계의 애니메이션은 BossStateManager.ExecutePattern이 이미 재생했으므로 그 뒤 단계만 재생한다
                if (ix > 0 && boss != null)
                {
                    boss.PlayPatternAnimation(step.animationTrigger, step.animationDelay);
                }

                // 단계가 시작될 때마다 플레이어 쪽으로 즉시 돌아선다
                boss?.RotateToPlayer(float.MaxValue);

                step.Execute(context);

                yield return new WaitForSeconds(step.duration);
            }
        }
    }
}
