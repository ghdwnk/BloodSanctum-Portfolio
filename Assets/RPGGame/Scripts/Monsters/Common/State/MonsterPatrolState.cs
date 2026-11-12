using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 몬스터 배회 상태. 스폰 지점 주변의 임의 지점을 골라 걸어간다(이동은 걷기 애니메이션의 루트 모션).
    /// 도착하거나 제한 시간(patrolLegDuration)이 지나면 잠깐 쉬는 대기 상태로 넘어간다.
    /// </summary>
    public class MonsterPatrolState : MonsterStateBase
    {
        // 도착 판정 허용 거리(m). 루트 모션으로 걷기 때문에 한 프레임 이동량이 커서,
        // 너무 작게 잡으면 목적지를 스쳐 지나가 도착 판정이 나지 않는다
        private const float ArriveDistance = 1.2f;

        // 이미 배회 반경 밖에 있을 때 반경을 줄이는 비율. 목적지를 중심 가까이로 좁혀 스폰 지점 쪽으로 돌아오게 한다
        private const float ReturnHomeRadiusScale = 0.5f;

        private Vector3 patrolDestination;
        private float patrolStartTime;

        protected override void OnEnable()
        {
            base.OnEnable();

            // 배회 중심은 언제나 스폰 지점이다
            Vector3 center = manager.HomePosition;
            float radius = data.patrolRadius;

            // 추격하다 돌아온 경우처럼 반경 밖에 있으면 목적지를 중심 가까이로 좁혀 자연스럽게 되돌아오게 한다
            if (Util.HorizontalDistance(refTransform.position, center) > radius)
            {
                radius *= ReturnHomeRadiusScale;
            }

            if (Util.RandomPoint(center, radius, out patrolDestination))
            {
                patrolStartTime = Time.time;
            }
            else
            {
                // 목적지를 뽑지 못했으면(지형 밖 등) 쉬었다가 다시 시도한다
                manager.SetState(MonsterStateManager.State.Idle);
            }
        }

        protected override void Update()
        {
            base.Update();

            // 지형에 막혀 제한 시간 안에 도착하지 못했으면 목적지를 새로 뽑는다
            if (Time.time > patrolStartTime + data.patrolLegDuration)
            {
                manager.SetState(MonsterStateManager.State.Idle);
                return;
            }

            // 도착 판정은 수평 거리로만 한다. 스폰 직후 낙하 중이거나 경사면이면 높이 차이 때문에 도착하지 못한다
            if (Util.HorizontalDistance(refTransform.position, patrolDestination) < ArriveDistance)
            {
                manager.SetState(MonsterStateManager.State.Idle);
                return;
            }

            Vector3 direction = patrolDestination - refTransform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                refTransform.rotation = Quaternion.RotateTowards(refTransform.rotation,
                    Quaternion.LookRotation(direction), data.patrolRotateSpeed * Time.deltaTime);
            }
        }
    }
}
