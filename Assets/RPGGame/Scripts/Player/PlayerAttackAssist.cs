using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 기본 공격이 시작될 때 가까운 적 쪽으로 몸을 돌리고 살짝 파고들게 하는 보조 기능.
    /// 정면 판정(등 뒤는 못 때림)과 짝을 이뤄, 조금 빗나가게 휘둘러도 적을 향하게 해 준다.
    /// 공격 상태를 직접 건드리지 않고, 애니메이터가 콤보 상태(AttackCombo1~3)로 새로 들어오는 순간을 지켜보다가 반응한다.
    /// 플레이어 루트에 붙어 있다.
    /// </summary>
    public class PlayerAttackAssist : MonoBehaviour
    {
        [Header("대상 탐색")]
        [Tooltip("이 거리(m) 안의 적을 찾는다")]
        [SerializeField] private float assistRange = 3.5f;
        [Tooltip("정면 기준 좌우 몇 도 안의 적까지 향해 줄지")]
        [SerializeField, Range(10f, 180f)] private float assistHalfAngle = 90f;

        [Header("몸 돌리기")]
        [Tooltip("적 쪽으로 도는 데 걸리는 시간(초)")]
        [SerializeField] private float turnTime = 0.06f;

        [Header("공격 중 방향 전환")]
        [Tooltip("켜면 공격 중 마우스로 카메라를 돌린 만큼 캐릭터도 따라 돈다 (카메라 정면으로 순간 이동하지 않아 튀지 않는다)")]
        [SerializeField] private bool followCameraWhileAttacking = true;

        [Header("전진")]
        [Tooltip("공격을 시작할 때 적 쪽으로 파고드는 최대 거리(m)")]
        [SerializeField] private float lungeDistance = 0.45f;
        [Tooltip("파고드는 데 걸리는 시간(초)")]
        [SerializeField] private float lungeTime = 0.1f;
        [Tooltip("적과 이 거리(m)보다 가까우면 더 파고들지 않는다 (몸이 겹치는 것을 막는다)")]
        [SerializeField] private float stopDistance = 1.0f;

        // 대상 탐색 버퍼. 탐색마다 배열을 새로 만들지 않도록 재사용한다
        private static readonly Collider[] buffer = new Collider[16];

        private Animator animator;
        private CharacterController characterController;
        private WeaponController weaponController;
        private PlayerStateManager stateManager;
        private Transform cameraTransform;
        private float lastCameraYaw;
        private int[] comboHashes;
        private int lastStateHash;

        private Quaternion turnFrom;
        private Quaternion turnTo;
        private float turnElapsed = -1f;

        private Vector3 lungeDirection;
        private float lungeRemainingDistance;
        private float lungeElapsed = -1f;
        private float lungeTotalDistance;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            characterController = GetComponent<CharacterController>();
            weaponController = GetComponentInChildren<WeaponController>();
            stateManager = GetComponent<PlayerStateManager>();

            int count = (int)PlayerStateManager.AttackCombo.Length;
            comboHashes = new int[count];
            for (int ix = 0; ix < count; ++ix)
            {
                comboHashes[ix] = Animator.StringToHash($"AttackCombo{ix + 1}");
            }
        }

        private void Update()
        {
            if (animator == null)
            {
                return;
            }

            // 콤보 상태로 "새로 들어온" 프레임에만 보조를 시작한다
            int hash = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            if (hash != lastStateHash)
            {
                lastStateHash = hash;

                if (System.Array.IndexOf(comboHashes, hash) >= 0)
                {
                    BeginAssist();
                }
                else
                {
                    // 공격이 아닌 상태로 넘어가면 진행 중인 보조를 멈춘다
                    turnElapsed = -1f;
                    lungeElapsed = -1f;
                }
            }

            FollowCameraYaw();
            UpdateTurn();
            UpdateLunge();
        }

        // 공격 중 카메라가 좌우로 돈 만큼 캐릭터도 돌린다.
        // 평소에도 매 프레임 각도를 기록해 두어야, 공격이 시작되는 순간 그동안 쌓인 회전이 한꺼번에 적용되지 않는다
        private void FollowCameraYaw()
        {
            if (cameraTransform == null)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    return;
                }

                cameraTransform = mainCamera.transform;
                lastCameraYaw = cameraTransform.eulerAngles.y;
            }

            float yaw = cameraTransform.eulerAngles.y;
            float delta = Mathf.DeltaAngle(lastCameraYaw, yaw);
            lastCameraYaw = yaw;

            if (!followCameraWhileAttacking || stateManager == null || stateManager.CurrentState != PlayerStateManager.State.Attack)
            {
                return;
            }

            if (Mathf.Approximately(delta, 0f))
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(0f, delta, 0f);
            transform.rotation = rotation * transform.rotation;

            // 조준 보정으로 몸을 돌리는 중이면 목표도 같이 돌려야 카메라 회전이 사라지지 않는다
            if (turnElapsed >= 0f)
            {
                turnFrom = rotation * turnFrom;
                turnTo = rotation * turnTo;
            }
        }

        private void BeginAssist()
        {
            Transform target = FindTarget();
            if (target == null)
            {
                return;
            }

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 0.0001f)
            {
                return;
            }

            turnFrom = transform.rotation;
            turnTo = Quaternion.LookRotation(toTarget);
            turnElapsed = 0f;

            float lunge = Mathf.Min(lungeDistance, toTarget.magnitude - stopDistance);
            if (lunge > 0.01f)
            {
                lungeDirection = toTarget.normalized;
                lungeTotalDistance = lunge;
                lungeRemainingDistance = lunge;
                lungeElapsed = 0f;
            }
        }

        // 정면 각도 안에서 가장 가까운 살아 있는 적을 찾는다
        private Transform FindTarget()
        {
            LayerMask layer = weaponController != null && weaponController.EquippedWeapon != null
                ? weaponController.EquippedWeapon.AttackTargetLayer
                : (LayerMask)(-1);

            int count = Physics.OverlapSphereNonAlloc(transform.position, assistRange, buffer, layer);
            Transform best = null;
            float bestSqrDistance = float.MaxValue;
            float minDot = Mathf.Cos(assistHalfAngle * Mathf.Deg2Rad);

            for (int ix = 0; ix < count; ++ix)
            {
                Collider candidate = buffer[ix];
                if (candidate.GetComponent<Damageable>() == null || IsDead(candidate))
                {
                    continue;
                }

                Vector3 diff = candidate.bounds.center - transform.position;
                diff.y = 0f;

                float sqrDistance = diff.sqrMagnitude;
                if (sqrDistance < 0.0001f || sqrDistance >= bestSqrDistance)
                {
                    continue;
                }

                if (Vector3.Dot(transform.forward, diff.normalized) < minDot)
                {
                    continue;
                }

                best = candidate.transform;
                bestSqrDistance = sqrDistance;
            }

            return best;
        }

        private static bool IsDead(Collider candidate)
        {
            MonsterStateManager monster = candidate.GetComponentInParent<MonsterStateManager>();
            if (monster != null)
            {
                return monster.IsDead;
            }

            BossStateManager boss = candidate.GetComponentInParent<BossStateManager>();
            return boss != null && boss.IsDead;
        }

        private void UpdateTurn()
        {
            if (turnElapsed < 0f)
            {
                return;
            }

            turnElapsed += Time.deltaTime;
            float t = turnTime > 0f ? Mathf.Clamp01(turnElapsed / turnTime) : 1f;
            transform.rotation = Quaternion.Slerp(turnFrom, turnTo, t);

            if (t >= 1f)
            {
                turnElapsed = -1f;
            }
        }

        private void UpdateLunge()
        {
            if (lungeElapsed < 0f || characterController == null || !characterController.enabled)
            {
                return;
            }

            lungeElapsed += Time.deltaTime;
            float t = lungeTime > 0f ? Mathf.Clamp01(lungeElapsed / lungeTime) : 1f;

            // 빠르게 나가서 끝에서 느려지는 곡선 (넉백과 같은 ease-out)
            float moved = lungeTotalDistance * (1f - (1f - t) * (1f - t));
            float step = moved - (lungeTotalDistance - lungeRemainingDistance);

            if (step > 0f)
            {
                characterController.Move(lungeDirection * step);
                lungeRemainingDistance -= step;
            }

            if (t >= 1f)
            {
                lungeElapsed = -1f;
            }
        }
    }
}
