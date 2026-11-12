using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 패턴에 "맞으면 화상"을 붙이는 설정 묶음. 운석처럼 필드를 패턴마다 따로 두지 않고
    /// 이 묶음 하나를 패턴에 넣으면 연속 균열·마력탄 같은 어느 패턴에서든 같은 방식으로 화상을 건다.
    /// 화상 자체(틱, 겹침 방지, 사망 시 해제)는 BurnEffect가 맡는다
    /// </summary>
    [System.Serializable]
    public class BossBurnSettings
    {
        [Tooltip("틱 한 번의 피해는 보스 공격력 x 이 값. 0이면 화상이 걸리지 않는다")]
        public float damageMultiplier = 0f;
        public float duration = 5f;
        public float tickInterval = 1f;
        [Tooltip("화상 중인 대상에게 붙일 이펙트 (비워 두면 표시 없음)")]
        public GameObject effectPrefab;

        public bool Enabled => damageMultiplier > 0f && duration > 0f;

        // 피해가 실제로 들어간 대상에게만 부른다 (무적 시간에 막힌 타격에는 걸지 않는다)
        public void Apply(Damageable target, float attackPower)
        {
            if (!Enabled)
            {
                return;
            }

            BurnEffect.Apply(target, attackPower * damageMultiplier, duration, tickInterval, effectPrefab);
        }
    }
}
