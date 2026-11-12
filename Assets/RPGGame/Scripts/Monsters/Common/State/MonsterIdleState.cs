using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 몬스터 대기 상태. 배회와 배회 사이의 짧은 휴식이다.
    /// 쉬는 시간은 들어올 때마다 EnemyData의 범위에서 새로 뽑아, 여러 마리가 같은 박자로 동시에 출발하지 않게 한다.
    /// 시간이 지나면 배회로 넘어간다. 플레이어를 발견하면 MonsterStateManager가 추격으로 바꾼다.
    /// </summary>
    public class MonsterIdleState : MonsterStateBase
    {
        private float elapsedTime;
        // 이번에 쉴 시간(초)
        private float waitTime;

        protected override void OnEnable()
        {
            base.OnEnable();

            elapsedTime = 0f;
            waitTime = Random.Range(data.patrolIdleTimeMin, data.patrolIdleTimeMax);
        }

        protected override void Update()
        {
            base.Update();

            elapsedTime += Time.deltaTime;

            if (elapsedTime > waitTime)
            {
                manager.SetState(MonsterStateManager.State.Patrol);
            }
        }
    }
}
