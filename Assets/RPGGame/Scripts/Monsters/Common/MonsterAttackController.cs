using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 몬스터의 공격 판정. 몬스터 프리팹의 손/무기 위치 자식 오브젝트에 붙어 있다.
    ///
    /// 공격 상태(MonsterAttackState)의 OnAttackBegin / OnAttackEnd가 인스펙터로 이 컴포넌트에 연결돼 있다.
    ///  - 근접: 타격 구간 동안 구 모양 범위 안의 플레이어에게 피해를 한 번 준다
    ///  - 원거리: projectilePrefab이 있으면 타격 시점에 투사체를 발사한다 (피해는 투사체가 맞는 순간 들어간다)
    /// 피해량은 레벨에 따라 MonsterStateManager가 SetAttack으로 넣어 준다.
    /// </summary>
    public class MonsterAttackController : MonoBehaviour
    {
        [Header("근접 판정")]
        [Tooltip("판정 구의 반지름(m)")]
        [SerializeField] private float radius = 0.5f;
        [Tooltip("공격이 맞을 수 있는 레이어 (플레이어)")]
        [SerializeField] private LayerMask attackTargetLayer;

        [Header("공격 이펙트")]
        [Tooltip("타격 시점에 생성할 이펙트. 몬스터마다 다른 프리팹을 꽂는다")]
        [SerializeField] private GameObject attackEffectPrefab;
        [Tooltip("판정 위치 기준 이펙트 위치. 몬스터 덩치에 맞춘다")]
        [SerializeField] private Vector3 effectOffset = Vector3.zero;
        [SerializeField] private float effectScale = 1f;
        [Tooltip("이펙트를 지우기까지의 시간(초)")]
        [SerializeField] private float effectLifetime = 2f;

        [Header("원거리 공격")]
        [Tooltip("채워져 있으면 근접 판정 대신 투사체를 발사한다. 공격 애니메이션 이벤트가 이미 OnAttackBegin에 연결돼 있어 별도 컴포넌트 없이 같은 배선을 쓴다")]
        [SerializeField] private MonsterProjectile projectilePrefab;
        [Tooltip("투사체 발사 위치 (이 오브젝트 기준)")]
        [SerializeField] private Vector3 projectileSpawnOffset = Vector3.zero;

        [Header("공격 소리")]
        [Tooltip("공격이 나가는 순간(근접은 휘두를 때, 원거리는 발사할 때) 틀 소리들. 매번 하나를 무작위로 고른다. 비우면 소리를 내지 않는다")]
        [SerializeField] private AudioClip[] attackSounds;
        [SerializeField, Range(0f, 1f)] private float attackSoundVolume = 0.8f;
        [Tooltip("음높이를 흔드는 폭. 0.1이면 0.9 ~ 1.1배 사이라 같은 소리가 반복돼도 덜 단조롭다")]
        [SerializeField, Range(0f, 0.3f)] private float attackSoundPitchRange = 0.1f;
        [Tooltip("이 거리(m) 안에서는 소리가 줄지 않는다. 플레이어와 카메라가 몬스터에서 떨어져 있으므로 너무 작으면 거의 들리지 않는다")]
        [SerializeField] private float attackSoundMinDistance = 6f;

        private float attackAmount;
        // 공격 소리를 내는 3D 소스. 소리를 쓰는 몬스터만 처음 공격할 때 만든다
        private AudioSource attackSoundSource;
        // 지금 타격 구간(판정이 켜진 동안)인지
        private bool isInAttack;
        private Transform refTransform;
        // 근접 판정용 재사용 버퍼. 공격 중에는 FixedUpdate마다 검사가 반복되므로
        // 매번 새 배열을 할당하는 대신 이 버퍼를 공유해서 쓴다
        private static readonly Collider[] hitBuffer = new Collider[8];

        private void Awake()
        {
            refTransform = transform;
        }

        /// <summary>한 번 맞았을 때의 피해량 (레벨 데이터에서 MonsterStateManager가 넣어 준다).</summary>
        public void SetAttack(float amount)
        {
            attackAmount = amount;
        }

        private void FixedUpdate()
        {
            if (!isInAttack)
            {
                return;
            }

            // 공격 판정 진행
            // 공격 위치, 반지름을 사용해 충돌 감지
            int hitCount = Physics.OverlapSphereNonAlloc(refTransform.position, radius, hitBuffer, attackTargetLayer);
            if (hitCount == 0)
            {
                return;
            }

            // 충돌 감지 과정에서 충돌한 충돌체가 있으면 데미지 전달
            for (int ix = 0; ix < hitCount; ++ix)
            {
                // 피해를 받을 수 있는 대상인지 확인
                Damageable damageable = hitBuffer[ix].GetComponent<Damageable>();

                // 피해를 주고 이번 타격은 끝낸다 (한 번의 공격에 한 번만 맞는다)
                if (damageable != null)
                {
                    damageable.ReceiveDamage(attackAmount);
                    isInAttack = false;
                    return;
                }
            }
        }

        /// <summary>타격 구간이 시작될 때 (MonsterAttackState에 인스펙터로 연결돼 있다. 이름을 바꾸지 않는다).</summary>
        public void OnAttackBegin()
        {
            // 원거리 몬스터는 근접 판정을 쓰지 않는다. 피해는 투사체가 맞은 시점에 들어간다
            if (projectilePrefab != null)
            {
                FireProjectile();
            }
            else
            {
                isInAttack = true;
            }

            SpawnAttackEffect();
            PlayAttackSound();
        }

        // 공격 소리 하나를 무작위로 골라 몬스터 위치에서 튼다.
        // PlayOneShot이라 연달아 공격해도 앞 소리가 끊기지 않고, 소리가 없는 몬스터는 소스를 만들지 않는다
        private void PlayAttackSound()
        {
            if (attackSounds == null || attackSounds.Length == 0)
            {
                return;
            }

            if (attackSoundSource == null)
            {
                attackSoundSource = gameObject.AddComponent<AudioSource>();
                attackSoundSource.playOnAwake = false;
                attackSoundSource.spatialBlend = 1f;
                attackSoundSource.rolloffMode = AudioRolloffMode.Logarithmic;
                attackSoundSource.minDistance = attackSoundMinDistance;
                attackSoundSource.maxDistance = 60f;
            }

            attackSoundSource.pitch = 1f + Random.Range(-attackSoundPitchRange, attackSoundPitchRange);
            attackSoundSource.PlayOneShot(attackSounds[Random.Range(0, attackSounds.Length)], attackSoundVolume);
        }

        // 투사체를 몬스터가 바라보는 방향으로 발사하는 함수
        private void FireProjectile()
        {
            Vector3 position = refTransform.TransformPoint(projectileSpawnOffset);
            // 매번 새로 생성/파괴하지 않고 프리팹별 재사용 대기열에서 꺼내 쓴다
            MonsterProjectile projectile = MonsterProjectile.Get(projectilePrefab, position, refTransform.rotation);

            projectile.Launch(attackAmount, attackTargetLayer);
        }

        // 공격 이펙트를 생성하는 함수.
        // 몬스터를 따라다닐 필요가 없는 1회성 연출이라 부모를 두지 않고 월드에 두고,
        // 수명이 지나면 파괴한다
        private void SpawnAttackEffect()
        {
            if (attackEffectPrefab == null)
            {
                return;
            }

            Vector3 position = refTransform.TransformPoint(effectOffset);
            GameObject effect = Instantiate(attackEffectPrefab, position, refTransform.rotation);

            if (!Mathf.Approximately(effectScale, 1f))
            {
                effect.transform.localScale *= effectScale;
            }

            Destroy(effect, effectLifetime);
        }

        /// <summary>타격 구간이 끝날 때 (MonsterAttackState에 인스펙터로 연결돼 있다. 이름을 바꾸지 않는다).</summary>
        public void OnAttackEnd()
        {
            isInAttack = false;
        }

#if UNITY_EDITOR
        // 씬 뷰에서 판정 범위를 빨간 구로 보여 준다 (빌드에는 들어가지 않는다)
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
#endif
    }
}
