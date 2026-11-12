using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 여러 시스템이 함께 쓰는 위치·판정 도우미 모음.
    /// </summary>
    public static class Util
    {
        // 배회 목적지로 허용할 최대 높이 차이(m). 절벽 아래나 지붕 위가 목적지로 뽑히는 것을 막는다
        private const float MaxPatrolHeightDelta = 8f;
        // 배회 목적지를 뽑을 때 다시 시도하는 최대 횟수
        private const int RandomPointAttempts = 30;
        // 지면을 찾을 때 레이를 쏘기 시작하는 높이(기준점 위)와 레이 길이
        private const float GroundRayStartHeight = 60f;
        private const float GroundRayLength = 200f;

        // 지면 검사용 레이캐스트 결과를 담는 공용 버퍼.
        // RaycastAll은 호출할 때마다 새 배열을 만들어 GC 부담이 생기므로, 한 번 만든 버퍼를 계속 재사용한다.
        // 한 줄에 겹치는 콜라이더(지형·건물·나무·캐릭터)가 이보다 많을 일은 없다
        private static readonly RaycastHit[] groundHits = new RaycastHit[32];

        /// <summary>
        /// target이 self의 시야(정면 기준 sightAngle도 이내, sightRange 이내)에 있는지 판정한다.
        /// </summary>
        public static bool IsInSight(Transform self, Transform target, float sightAngle, float sightRange)
        {
            Vector3 toTarget = target.position - self.position;

            // 거리 비교는 제곱값으로 해서 제곱근 계산을 피한다
            if (toTarget.sqrMagnitude > sightRange * sightRange)
            {
                return false;
            }

            return Vector3.Angle(self.forward, toTarget) <= sightAngle;
        }

        /// <summary>
        /// center 주변 range 안에서 "걸어갈 수 있는 지면 위의 무작위 지점"을 구한다 (몬스터 배회 목적지).
        /// 몬스터는 NavMeshAgent가 아니라 CharacterController + 루트 모션으로 움직이므로 NavMesh 없이
        /// 지면 레이캐스트로 높이를 맞춘다. 기준 높이와 차이가 큰 지점(절벽 아래, 지붕 위)은 버린다.
        /// </summary>
        /// <returns>조건에 맞는 지점을 찾으면 true</returns>
        public static bool RandomPoint(Vector3 center, float range, out Vector3 result)
        {
            // 높이 비교의 기준은 중심점 아래의 지면 높이다. 스폰 직후처럼 몬스터가 공중에 떠 있어도 기준이 흔들리지 않는다
            float referenceY = SampleGroundHeight(center, out float centerGroundY) ? centerGroundY : center.y;

            for (int attempt = 0; attempt < RandomPointAttempts; ++attempt)
            {
                // 높이는 어차피 지면에 맞추므로 XZ 평면에서만 뽑는다
                Vector2 offset = Random.insideUnitCircle * range;
                Vector3 candidate = new Vector3(center.x + offset.x, referenceY, center.z + offset.y);

                if (SampleGroundHeight(candidate, out float groundY) && Mathf.Abs(groundY - referenceY) <= MaxPatrolHeightDelta)
                {
                    result = new Vector3(candidate.x, groundY, candidate.z);
                    return true;
                }
            }

            result = Vector3.zero;
            return false;
        }

        /// <summary>
        /// 지정한 XZ 위치의 지면 높이를 구한다.
        /// 위에서 아래로 레이를 쏘아, 지형(Terrain)이 걸리면 그것을 지면으로 본다.
        /// 지형이 없으면 캐릭터를 제외한 히트 중 가장 낮은 지점을 고른다.
        /// 가장 높은 지점을 고르면 나무 캐노피나 집 지붕이 지면으로 잡히기 때문이다.
        /// </summary>
        /// <param name="position">검사할 위치. 이 위치의 y보다 GroundRayStartHeight만큼 위에서 레이를 쏜다</param>
        /// <param name="groundY">찾은 지면 높이</param>
        /// <returns>지면을 찾았으면 true</returns>
        public static bool SampleGroundHeight(Vector3 position, out float groundY)
        {
            Vector3 origin = new Vector3(position.x, position.y + GroundRayStartHeight, position.z);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, GroundRayLength, ~0, QueryTriggerInteraction.Ignore);

            bool found = false;
            groundY = 0f;

            for (int ix = 0; ix < count; ++ix)
            {
                Collider hitCollider = groundHits[ix].collider;

                // 캐릭터(몬스터/플레이어)는 지면이 아니다
                if (hitCollider is CharacterController)
                {
                    continue;
                }

                // 지형이 걸렸다면 더 볼 것 없이 그것이 지면이다
                if (hitCollider is TerrainCollider)
                {
                    groundY = groundHits[ix].point.y;
                    return true;
                }

                if (!found || groundHits[ix].point.y < groundY)
                {
                    groundY = groundHits[ix].point.y;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// 높이를 무시한 수평 거리. 낙하 중이거나 경사면에 서 있어도 도착·이탈 판정이 흔들리지 않게 배회 로직에서 쓴다.
        /// </summary>
        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>
        /// CharacterController가 붙은 캐릭터를 순간이동시킨다.
        /// CharacterController가 켜져 있으면 transform.position 대입이 다음 이동에서 되돌려지므로 잠시 끄고 옮긴다.
        /// 플레이어 부활, 보스 리셋, 텔레포트가 모두 같은 절차를 쓰기 때문에 한곳에 모았다.
        /// </summary>
        public static void TeleportCharacter(Transform target, Vector3 position, Quaternion rotation)
        {
            if (target == null)
            {
                return;
            }

            CharacterController controller = target.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;

            if (wasEnabled)
            {
                controller.enabled = false;
            }

            target.SetPositionAndRotation(position, rotation);

            if (wasEnabled)
            {
                controller.enabled = true;
            }
        }
    }
}
