using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 일반 몬스터 상태 컴포넌트(Idle·Patrol·Chase·Attack·Dead)의 공통 부모.
    ///
    /// MonsterStateManager가 지금 상태의 컴포넌트만 켜 두므로, 켜진 컴포넌트의 Update·OnAnimatorMove만 돈다.
    /// 어느 상태에서나 공통으로 하는 일:
    ///  - 지형 보정: 급경사에서 지형을 뚫고 빠진 몬스터를 지면 위로 되돌린다
    ///  - 중력: 매 프레임 아래로 내린다
    ///  - 간격 유지: 가까이 붙은 다른 몬스터와 서로 밀어내 겹치지 않게 한다
    ///  - 루트 모션: 애니메이션의 이동량을 CharacterController로 옮긴다 (대기 위치 고정 중이면 전진하지 않는다)
    /// </summary>
    public class MonsterStateBase : MonoBehaviour
    {
        // 지형보다 이만큼(m) 아래로 내려가면 지형을 뚫고 빠진 것으로 본다.
        // 경사면이나 지형 위 구조물에서 생기는 오차보다는 충분히 큰 값
        private const float FallThroughDepth = 3f;

        // 지형 목록 캐시. Terrain.activeTerrains는 부를 때마다 배열을 새로 만들 수 있어 모든 몬스터가
        // 매 프레임 부르면 GC가 쌓인다. 지형은 씬이 바뀌지 않는 한 그대로이므로 한 번만 가져와 두고,
        // 파괴된 항목이 보이면(씬 전환 등) 다시 가져온다
        private static Terrain[] cachedTerrains;

        protected Transform refTransform;
        protected Animator refAnimator;
        protected CharacterController characterController;
        protected MonsterStateManager manager;
        // 감지 범위·공격 사거리 등 종류별 수치 (MonsterStateManager가 Awake에서 넘겨준다)
        protected EnemyData data;

        /// <summary>MonsterStateManager가 종류 데이터를 넘겨줄 때 부른다.</summary>
        public void SetData(EnemyData enemyData)
        {
            data = enemyData;
        }

        protected virtual void OnEnable()
        {
            if (refTransform == null)
            {
                refTransform = transform;
            }

            if (refAnimator == null)
            {
                refAnimator = GetComponent<Animator>();
            }

            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            if (manager == null)
            {
                manager = GetComponent<MonsterStateManager>();
            }
        }

        protected virtual void Update()
        {
            // 죽은 몬스터는 CharacterController가 꺼져 있어 아래 처리를 건너뛴다
            if (!characterController.enabled)
            {
                return;
            }

            ClampToGround();
            characterController.Move(Physics.gravity * Time.deltaTime);
            ApplySeparation();
        }

        // 애니메이션의 이동량(루트 모션)을 CharacterController로 옮긴다
        protected virtual void OnAnimatorMove()
        {
            if (!characterController.enabled)
            {
                return;
            }

            // 자리를 못 잡고 대기 중인 몬스터는 전진하지 않는다 (겹쳐서 파고드는 것을 막는다).
            // 중력과 서로 밀어내기는 Update에서 계속 적용된다
            if (manager != null && manager.ShouldHoldPosition)
            {
                return;
            }

            characterController.Move(refAnimator.deltaPosition);
        }

        // ───────────────────────── 지형 보정 ─────────────────────────

        // 지형 아래로 빠진 몬스터를 지면 위로 되돌리는 마지막 안전장치.
        // 스폰 높이 문제는 스포너에서 해결했지만, 급경사에서 CharacterController가 지형을 뚫는 경우가 남아 있다.
        // 물리 레이캐스트 대신 지형 높이 표 조회(SampleHeight)를 쓰므로 모든 몬스터가 매 프레임 불러도 비용이 거의 없다
        private void ClampToGround()
        {
            Vector3 position = refTransform.position;

            // 맵이 여러 장의 지형으로 나뉘어 있어서 activeTerrain 한 장만 기준으로 삼으면
            // 다른 지형 위의 몬스터를 "빠졌다"고 오판해 엉뚱한 높이로 끌어올린다. 발밑의 지형을 골라 쓴다
            Terrain terrain = FindTerrainUnder(position);
            if (terrain == null)
            {
                return;
            }

            float groundY = terrain.SampleHeight(position) + terrain.transform.position.y;
            if (position.y >= groundY - FallThroughDepth)
            {
                return;
            }

            // CharacterController가 켜져 있으면 transform 대입이 무시되므로 잠시 끈다
            characterController.enabled = false;
            position.y = groundY + 0.1f;
            refTransform.position = position;
            characterController.enabled = true;
        }

        // 위치(XZ)를 덮고 있는 지형을 찾는다. 지형 밖이면 null (보정하지 않는다)
        private static Terrain FindTerrainUnder(Vector3 position)
        {
            if (cachedTerrains == null || cachedTerrains.Length == 0 || cachedTerrains[0] == null)
            {
                cachedTerrains = Terrain.activeTerrains;
            }

            for (int ix = 0; ix < cachedTerrains.Length; ++ix)
            {
                Terrain candidate = cachedTerrains[ix];
                Vector3 origin = candidate.transform.position;
                Vector3 size = candidate.terrainData.size;

                if (position.x >= origin.x && position.x <= origin.x + size.x &&
                    position.z >= origin.z && position.z <= origin.z + size.z)
                {
                    return candidate;
                }
            }

            return null;
        }

        // ───────────────────────── 간격 유지 ─────────────────────────

        // 몬스터끼리 겹치지 않도록 서로 밀어낸다. 모든 상태가 이 클래스를 상속하므로
        // 배회·추격·공격 어디서든 똑같이 적용된다
        private void ApplySeparation()
        {
            if (data == null || data.separationRadius <= 0f)
            {
                return;
            }

            float radius = data.separationRadius;
            float sqrRadius = radius * radius;
            Vector3 selfPosition = refTransform.position;
            Vector3 push = Vector3.zero;

            // foreach 대신 for와 정적 접근자를 쓴다. 모든 몬스터가 매 프레임 도는 루프라 열거자 할당을 피하기 위해서다
            int count = MonsterStateManager.AliveMonsterCount;

            for (int ix = 0; ix < count; ++ix)
            {
                MonsterStateManager other = MonsterStateManager.GetAliveMonster(ix);
                if (other == null || other == manager || other.IsDead)
                {
                    continue;
                }

                // 높이 차이는 무시한다. 경사면에서 위아래로 서 있어도 수평으로만 벌어지면 된다
                Vector3 diff = selfPosition - other.transform.position;
                diff.y = 0f;

                // 제곱 거리로 먼저 걸러 대부분의 후보에서 제곱근 계산을 생략한다
                float sqrDistance = diff.sqrMagnitude;
                if (sqrDistance >= sqrRadius)
                {
                    continue;
                }

                if (sqrDistance < 0.0001f)
                {
                    // 완전히 겹쳐 방향을 만들 수 없으면 두 몬스터의 ID로 고정된 방향을 만든다.
                    // 무작위로 뽑으면 매 프레임 방향이 바뀌어 떨린다
                    float angle = (manager.GetEntityId().GetHashCode() - other.GetEntityId().GetHashCode()) % 360;
                    push += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    continue;
                }

                // 가까울수록 강하게 민다 (거리가 radius면 0, 붙어 있으면 1)
                float distance = Mathf.Sqrt(sqrDistance);
                push += diff / distance * (1f - distance / radius);
            }

            if (push != Vector3.zero)
            {
                characterController.Move(push * (data.separationStrength * Time.deltaTime));
            }
        }
    }
}
