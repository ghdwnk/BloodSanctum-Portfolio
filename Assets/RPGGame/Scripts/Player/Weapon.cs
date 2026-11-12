using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 플레이어 손에 들린 검. 기본 공격의 타격 판정과 손에 보이는 무기 모델 교체를 맡는다.
    ///
    /// 생성: WeaponController가 게임 시작 때 Weapon 프리팹(Weapon Sword14)을 손 뼈 아래에 만들고 Attach를 부른다.
    /// 타격 판정: PlayerAttackState가 공격 판정 구간의 시작/끝에 OnAttackBegin/OnAttackEnd를 부르고,
    ///   그 사이의 FixedUpdate마다 검날을 따라 놓인 캡슐(attackPoints 두 점)로 겹친 적을 찾는다.
    ///   피해가 실제로 들어가면 CombatFeedback.PlayHit으로 히트스톱·흔들림·스파크를 요청하고 MP를 조금 회복한다.
    /// 모델 교체: 장착한 무기 장비의 weaponModelPrefab으로 SetModel을 부르면 기본 모델을 숨기고 새 모델을 꽂는다.
    ///   모델 길이에 맞춰 판정 캡슐도 늘이거나 줄인다.
    /// </summary>
    public class Weapon : MonoBehaviour
    {
        // 한 번의 판정에서 받을 수 있는 최대 충돌체 수. 검 하나에 동시에 닿는 적은 이보다 훨씬 적다
        private const int MaxHitColliders = 16;
        // 모델 끝(칼끝)에서 판정 시작점을 더 내미는 거리. 기본 모델은 길이 0.9에 시작점 1.25다
        private const float AttackReachPadding = 0.3f;
        // 판정 끝점(손잡이 쪽)의 높이
        private const float AttackEndOffset = 0.05f;

        // 2타 애니메이터 상태의 해시 (2타만 판정 길이를 늘릴 때 쓴다)
        private static readonly int SecondComboStateHash = Animator.StringToHash("AttackCombo2");

        // 콤보 마지막 타격 애니메이터 상태의 해시. 타격마다 "AttackCombo4" 문자열을 만들지 않도록 미리 구해 둔다
        private static readonly int FinisherStateHash =
            Animator.StringToHash($"AttackCombo{(int)PlayerStateManager.AttackCombo.Length}");

        [Header("타격 판정")]
        [Tooltip("검날을 감싸는 판정 캡슐의 반지름(m)")]
        [SerializeField] private float radius = 0.1f;
        [Tooltip("2타에서 판정 캡슐의 칼끝을 칼날 방향으로 더 내미는 거리(m). 2타는 손이 1타보다 몸에서 덜 뻗어 실제로 맞는 거리가 짧아서 보정한다 (손 뻗는 거리 1타 0.89m, 2타 0.68m)")]
        [SerializeField] private float secondComboReachBonus = 0.25f;
        [Tooltip("판정 캡슐의 양 끝점. 0번이 칼끝 쪽, 1번이 손잡이 쪽이다")]
        [SerializeField] private Transform[] attackPoints;
        [Tooltip("타격할 수 있는 레이어 (몬스터)")]
        [SerializeField] private LayerMask attackTargetLayer;
        [Tooltip("플레이어 정면 기준 좌우 몇 도까지 맞을 수 있는지. 등 뒤의 적이 휘두르는 검에 맞는 것을 막는다")]
        [SerializeField, Range(10f, 180f)] private float attackHalfAngle = 80f;
        [Tooltip("맞은 적이 플레이어 반대 방향으로 밀려나는 거리(m). 0이면 넉백 없음")]
        [SerializeField] private float knockbackDistance = 0.7f;
        [Tooltip("맞은 위치에서 재생할 파티클 (무기 프리팹 안의 자식)")]
        [SerializeField] private ParticleSystem hitParticle;

        [Header("타격 시 MP 회복")]
        [Tooltip("기본 공격이 적중하면 회복하는 MP. 스윙 한 번에 한 번만 준다 (여러 적을 맞혀도 더 받지 않는다)")]
        [SerializeField] private float mpRecoverOnHit = 5f;
        [Tooltip("콤보 마지막 타격이 적중하면 회복하는 MP")]
        [SerializeField] private float mpRecoverOnFinisherHit = 10f;

        // ───── 공격 상태 ─────
        private bool isInAttack;
        // 이번 스윙에서 이미 MP를 회복했는지
        private bool hasRecoveredMP;
        // 이번 스윙에서 이미 때린 대상.
        // 판정 구간을 넉넉히 잡으면 같은 적이 여러 FixedUpdate에 걸쳐 겹치므로, 한 스윙에 한 번만 맞게 여기서 거른다.
        // 매 판정마다 포함 여부를 확인하므로 List(O(n)) 대신 HashSet(O(1))을 쓴다
        private readonly HashSet<Damageable> hitTargets = new HashSet<Damageable>();
        // OverlapCapsule은 호출마다 배열을 새로 만들므로, NonAlloc 판에 넘길 버퍼를 재사용한다
        private readonly Collider[] hitBuffer = new Collider[MaxHitColliders];

        // ───── 주인(플레이어) 컴포넌트 ─────
        // Attach 때 한 번 찾아 둔다
        private PlayerStats ownerStats;
        private Animator ownerAnimator;
        private PlayerAnimationController ownerAnimationController;
        private MPController ownerMP;

        // ───── 무기 모델 교체 ─────
        // 프리팹에 처음 들어 있던 모델(기본 모델)과, 지금 꽂혀 있는 다른 모델
        private GameObject defaultModel;
        private GameObject customModel;
        private GameObject customModelSource;
        // 기본 모델일 때의 판정 캡슐 위치. 기본 모델로 돌아갈 때 되돌린다
        private Vector3 defaultStartLocal;
        private Vector3 defaultEndLocal;
        private bool modelCached;

        /// <summary>타격 대상 레이어. 자동 조준(PlayerAttackAssist)이 같은 레이어로 적을 찾는다.</summary>
        public LayerMask AttackTargetLayer => attackTargetLayer;

        /// <summary>지금 손에 보이는 무기 모델. 강화 빛(WeaponEnhanceGlow)이 이 모델에 입혀진다.</summary>
        public GameObject CurrentModel => customModel != null ? customModel : defaultModel;

        // ───────────────────────── 장착 ─────────────────────────

        /// <summary>
        /// 손 뼈 아래에 붙이고 주인의 스탯·애니메이터·MP를 찾아 둔다. WeaponController가 한 번 부른다.
        /// </summary>
        public void Attach(Transform parentTransform)
        {
            Transform refTransform = transform;
            refTransform.SetParent(parentTransform);
            refTransform.localPosition = Vector3.zero;
            refTransform.localRotation = Quaternion.identity;

            ownerStats = GetComponentInParent<PlayerStats>();
            if (ownerStats != null)
            {
                ownerAnimator = ownerStats.GetComponent<Animator>();
                ownerAnimationController = ownerStats.GetComponentInChildren<PlayerAnimationController>();
                ownerMP = ownerStats.GetComponentInChildren<MPController>();
            }
        }

        // ───────────────────────── 타격 판정 ─────────────────────────

        /// <summary>공격 판정 구간이 시작될 때 PlayerAttackState가 부른다.</summary>
        public void OnAttackBegin()
        {
            isInAttack = true;

            // 스윙이 새로 시작될 때마다 중복 방지 기록을 비운다
            hitTargets.Clear();
            hasRecoveredMP = false;
        }

        /// <summary>공격 판정 구간이 끝날 때 PlayerAttackState가 부른다.</summary>
        public void OnAttackEnd()
        {
            isInAttack = false;
        }

        // 물리 판정은 물리 갱신 주기에 맞춰 FixedUpdate에서 한다
        private void FixedUpdate()
        {
            if (!isInAttack || attackPoints == null || attackPoints.Length < 2)
            {
                return;
            }

            Vector3 bladeStart = attackPoints[0].position;
            Vector3 bladeEnd = attackPoints[1].position;

            // 2타는 칼끝을 더 내밀어 1타와 비슷한 거리까지 맞게 한다
            if (secondComboReachBonus > 0f && IsSecondComboSwing())
            {
                Vector3 bladeDirection = (bladeStart - bladeEnd).normalized;
                bladeStart += bladeDirection * secondComboReachBonus;
            }

            int hitCount = Physics.OverlapCapsuleNonAlloc(bladeStart, bladeEnd, radius, hitBuffer, attackTargetLayer);
            if (hitCount == 0)
            {
                return;
            }

            // 방향·넉백의 기준은 무기가 아니라 플레이어 몸이다 (칼끝은 적 몸속까지 들어가 방향이 불안정하다)
            Transform owner = ownerStats != null ? ownerStats.transform : transform;
            Vector3 ownerPosition = owner.position;
            Vector3 ownerForward = owner.forward;
            float minDot = Mathf.Cos(attackHalfAngle * Mathf.Deg2Rad);

            for (int ix = 0; ix < hitCount; ++ix)
            {
                Collider target = hitBuffer[ix];
                Damageable damageable = target.GetComponent<Damageable>();
                if (damageable == null)
                {
                    continue;
                }

                // 플레이어가 바라보는 방향 밖(등 뒤)의 적은 맞지 않는다.
                // 검 판정 캡슐은 몸 가까이까지 오기 때문에 거리만으로는 뒤쪽 적도 걸린다.
                // 기록(hitTargets)에 남기지 않으므로, 스윙 중 몸을 돌려 정면에 들어오면 그때는 맞는다
                Vector3 toTarget = target.bounds.center - ownerPosition;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f && Vector3.Dot(ownerForward, toTarget.normalized) < minDot)
                {
                    continue;
                }

                // 이번 스윙에서 이미 때린 대상이면 건너뛴다
                if (!hitTargets.Add(damageable))
                {
                    continue;
                }

                // 플레이어 공격력과 치명타 판정은 PlayerStats가 계산한다 (장비 공격력도 거기에 합산돼 있다)
                // 콤보 마지막 타격(3타 찌르기)은 확률과 상관없이 항상 치명타다
                bool isFinisher = IsFinisherSwing();
                bool isCritical = false;
                float damage = ownerStats != null ? ownerStats.CalculateOutgoingDamage(out isCritical, isFinisher) : 0f;

                // 무적 시간에 막혀 피해가 들어가지 않았으면 연출도 붙이지 않는다
                if (!damageable.ReceiveHit(damage, ownerPosition, knockbackDistance, isCritical))
                {
                    continue;
                }

                Vector3 hitPoint = target.ClosestPoint(Vector3.Lerp(bladeStart, bladeEnd, 0.5f));

                if (hitParticle != null)
                {
                    hitParticle.transform.position = hitPoint;
                    hitParticle.Play();
                }

                CombatFeedback.PlayHit(hitPoint, damageable, ownerAnimator, isCritical, isFinisher);

                // 실제로 들어간 첫 타격에서만 MP를 회복한다 (몰이사냥으로 MP를 퍼 담는 것을 막는다)
                if (!hasRecoveredMP && ownerMP != null)
                {
                    hasRecoveredMP = true;
                    ownerMP.RestoreMP(isFinisher ? mpRecoverOnFinisherHit : mpRecoverOnHit);
                }
            }
        }

        // 지금 재생 중인 공격이 2타인지
        private bool IsSecondComboSwing()
        {
            return ownerAnimationController != null &&
                   ownerAnimationController.GetCurrentStateInfo().shortNameHash == SecondComboStateHash;
        }

        // 지금 재생 중인 공격이 콤보의 마지막 타격인지
        private bool IsFinisherSwing()
        {
            return ownerAnimationController != null &&
                   ownerAnimationController.GetCurrentStateInfo().shortNameHash == FinisherStateHash;
        }

        // ───────────────────────── 모델 교체 ─────────────────────────

        /// <summary>
        /// 손에 든 무기 모델을 바꾼다. null이면 기본 모델로 되돌린다.
        /// 모델 길이에 맞춰 판정 캡슐(attackPoints)도 옮겨서, 긴 검은 더 멀리 짧은 검은 짧게 맞는다.
        /// WeaponController가 무기 장비를 바꿀 때 부른다.
        /// </summary>
        public void SetModel(GameObject modelPrefab)
        {
            CacheDefaultModel();

            // 같은 모델이면 다시 만들지 않는다 (강화·스탯 변경으로 장비 갱신이 자주 불린다)
            if (modelPrefab == customModelSource)
            {
                return;
            }

            if (customModel != null)
            {
                Destroy(customModel);
                customModel = null;
            }

            customModelSource = modelPrefab;
            bool hasAttackPoints = attackPoints != null && attackPoints.Length >= 2;

            if (modelPrefab == null)
            {
                if (defaultModel != null)
                {
                    defaultModel.SetActive(true);
                }

                if (hasAttackPoints)
                {
                    attackPoints[0].localPosition = defaultStartLocal;
                    attackPoints[1].localPosition = defaultEndLocal;
                }

                return;
            }

            if (defaultModel != null)
            {
                defaultModel.SetActive(false);
            }

            customModel = Instantiate(modelPrefab, transform);
            customModel.transform.localPosition = Vector3.zero;
            customModel.transform.localRotation = Quaternion.identity;

            float tipY = FindModelTipHeight(customModel);
            if (hasAttackPoints && tipY > 0.1f)
            {
                attackPoints[0].localPosition = new Vector3(0f, tipY + AttackReachPadding, 0f);
                attackPoints[1].localPosition = new Vector3(0f, AttackEndOffset, 0f);
            }
        }

        // 모델 메시의 경계 상자 모서리 8개를 무기 로컬 좌표로 바꿔 가장 높은 지점(칼끝)의 높이를 구한다.
        // Renderer.bounds는 월드 축 기준이라 손이 회전해 있으면 로컬 높이로 쓸 수 없어서 메시 bounds를 직접 변환한다
        private float FindModelTipHeight(GameObject model)
        {
            float tipY = 0f;

            foreach (MeshFilter modelFilter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (modelFilter.sharedMesh == null)
                {
                    continue;
                }

                Bounds meshBounds = modelFilter.sharedMesh.bounds;
                Matrix4x4 toWeaponLocal = transform.worldToLocalMatrix * modelFilter.transform.localToWorldMatrix;

                for (int corner = 0; corner < 8; ++corner)
                {
                    // corner의 비트 3개로 x/y/z 각각 -/+ 방향을 골라 8개 모서리를 만든다
                    Vector3 sign = new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f);
                    Vector3 point = meshBounds.center + Vector3.Scale(meshBounds.extents, sign);
                    tipY = Mathf.Max(tipY, toWeaponLocal.MultiplyPoint3x4(point).y);
                }
            }

            return tipY;
        }

        // 프리팹에 처음 들어 있던 모델과 판정 위치를 한 번만 기억해 둔다
        private void CacheDefaultModel()
        {
            if (modelCached)
            {
                return;
            }

            modelCached = true;
            MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>(true);
            defaultModel = meshRenderer != null ? meshRenderer.gameObject : null;

            if (attackPoints != null && attackPoints.Length >= 2)
            {
                defaultStartLocal = attackPoints[0].localPosition;
                defaultEndLocal = attackPoints[1].localPosition;
            }
        }
    }
}
