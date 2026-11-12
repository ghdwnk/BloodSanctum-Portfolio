using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 원거리 몬스터(Nightshade 등)가 쏘는 투사체.
    ///
    /// MonsterAttackController가 타격 시점에 Get으로 꺼내 Launch로 피해량과 대상 레이어를 넘기므로,
    /// 프리팹 자체는 몬스터 종류와 무관하다. 앞으로 날아가며 매 프레임 이전 위치~현재 위치 사이를 훑어
    /// (빠른 투사체가 대상을 통과하지 않게) 처음 맞은 대상에게 피해를 주고 사라진다.
    /// 원거리 몬스터가 반복해서 쏘므로 매번 Instantiate/Destroy하면 GC가 쌓인다.
    /// 그래서 프리팹별로 재사용 대기열을 두고 비활성화/재활성화로 순환시킨다 (파괴하지 않는다).
    /// </summary>
    public class MonsterProjectile : MonoBehaviour
    {
        [Tooltip("비행 속도(m/s)")]
        [SerializeField] private float speed = 14f;
        [Tooltip("최대 비행 시간(초). 사거리(8m) / 속도(14m/s)의 두 배 정도면 충분하다")]
        [SerializeField] private float maxLifeTime = 1.5f;
        [Tooltip("맞았는지 판정하는 반지름(m)")]
        [SerializeField] private float hitRadius = 0.5f;

        [Header("명중 연출")]
        [Tooltip("명중했을 때 그 자리에 띄울 이펙트")]
        [SerializeField] private GameObject impactEffectPrefab;
        [SerializeField] private float impactEffectLifeTime = 2f;
        [Tooltip("빗나가 사거리 끝에서 사라질 때, 꼬리 파티클이 한 프레임에 사라지지 않도록 방출만 멈추고 이 시간(초) 뒤에 지운다. 명중했을 때는 기다리지 않는다")]
        [SerializeField] private float despawnDelayAfterHit = 1f;

        // 프리팹(원본)별 재사용 대기열. 몬스터 종류마다 투사체 프리팹이 달라도 서로 섞이지 않는다
        private static readonly Dictionary<MonsterProjectile, Queue<MonsterProjectile>> pools = new();
        // 비행 판정용 재사용 버퍼. 투사체마다 프레임마다 새 배열을 만들지 않기 위함
        private static readonly Collider[] hitBuffer = new Collider[8];

        // 이 인스턴스가 어느 프리팹에서 만들어졌는지 (반납할 대기열을 찾기 위해)
        private MonsterProjectile sourcePrefab;

        private Transform refTransform;
        private ParticleSystem[] particles;
        private float damage;
        private LayerMask targetLayer;
        private System.Action<Damageable> onHit;
        private float elapsedTime;
        private bool isFinished;

        // 에디터에서 도메인 리로드 없이 플레이를 다시 시작해도 이전 플레이의 (파괴된) 투사체가 대기열에 남지 않게 비운다
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPools()
        {
            pools.Clear();
        }

        private void Awake()
        {
            refTransform = transform;
            particles = GetComponentsInChildren<ParticleSystem>(true);
        }

        /// <summary>대기열에서 꺼내거나(있으면) 새로 만들어(없으면) 발사 위치·방향에 놓는다. Instantiate(prefab, position, rotation)를 그대로 대체할 수 있다.</summary>
        public static MonsterProjectile Get(MonsterProjectile prefab, Vector3 position, Quaternion rotation)
        {
            if (!pools.TryGetValue(prefab, out Queue<MonsterProjectile> queue))
            {
                queue = new Queue<MonsterProjectile>();
                pools[prefab] = queue;
            }

            while (queue.Count > 0)
            {
                MonsterProjectile pooled = queue.Dequeue();

                // 씬 전환 등으로 이미 파괴된 인스턴스는 건너뛴다
                if (pooled == null)
                {
                    continue;
                }

                pooled.refTransform.SetPositionAndRotation(position, rotation);
                pooled.gameObject.SetActive(true);
                pooled.elapsedTime = 0f;
                pooled.isFinished = false;

                foreach (ParticleSystem particle in pooled.particles)
                {
                    particle.Clear(true);
                    particle.Play(true);
                }

                return pooled;
            }

            MonsterProjectile instance = Instantiate(prefab, position, rotation);
            instance.sourcePrefab = prefab;
            return instance;
        }

        /// <summary>
        /// 발사한 쪽이 부른다. 피해량과 맞출 대상을 넘겨받는다.
        /// onHit은 피해가 실제로 들어간 대상에게만 불린다 (화상 같은 추가 효과용. 풀에서 재사용되므로 발사마다 새로 받는다).
        /// </summary>
        public void Launch(float damage, LayerMask targetLayer, System.Action<Damageable> onHit = null)
        {
            this.damage = damage;
            this.targetLayer = targetLayer;
            this.onHit = onHit;
            elapsedTime = 0f;
            isFinished = false;
        }

        private void Update()
        {
            if (isFinished)
            {
                return;
            }

            Vector3 previousPosition = refTransform.position;
            refTransform.position += refTransform.forward * (speed * Time.deltaTime);

            // 프레임마다 현재 위치만 검사하면 빠른 투사체가 대상을 그냥 통과한다.
            // 이전 위치와 현재 위치를 잇는 캡슐로 검사해 비행 경로 전체를 훑는다.
            // NonAlloc 버전 + 공유 버퍼를 써서 투사체가 날아가는 매 프레임마다 배열이 새로 생기지 않게 한다
            int hitCount = Physics.OverlapCapsuleNonAlloc(previousPosition, refTransform.position, hitRadius, hitBuffer, targetLayer);

            for (int ix = 0; ix < hitCount; ++ix)
            {
                Damageable damageable = hitBuffer[ix].GetComponent<Damageable>();

                if (damageable != null)
                {
                    if (damageable.ReceiveHit(damage, refTransform.position, 0f, false))
                    {
                        onHit?.Invoke(damageable);
                    }

                    Finish(true);
                    return;
                }
            }

            elapsedTime += Time.deltaTime;

            if (elapsedTime >= maxLifeTime)
            {
                // 빗나간 투사체는 명중 이펙트 없이 조용히 사라진다
                Finish(false);
            }
        }

        private void Finish(bool spawnImpactEffect)
        {
            isFinished = true;

            if (spawnImpactEffect)
            {
                if (impactEffectPrefab != null)
                {
                    GameObject impact = Instantiate(impactEffectPrefab, refTransform.position, refTransform.rotation);
                    Destroy(impact, impactEffectLifeTime);
                }

                // 명중한 투사체는 그 자리에서 바로 사라져야 한다. 방출만 멈추고 기다리면 이미 나온 꼬리 파티클과
                // 본체가 despawnDelayAfterHit 동안 플레이어 위치 공중에 멈춰 남는다 (폭발은 위 명중 이펙트가 대신 보여 준다)
                foreach (ParticleSystem particle in particles)
                {
                    particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }

                Release();
                return;
            }

            // 빗나가 사거리 끝에서 사라지는 투사체는 꼬리가 자연스럽게 흩어지도록 방출만 멈추고 잠시 뒤에 지운다
            foreach (ParticleSystem particle in particles)
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            StartCoroutine(ReleaseAfterDelay(despawnDelayAfterHit));
        }

        private IEnumerator ReleaseAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            Release();
        }

        // 파괴하지 않고 비활성화한 뒤, 같은 프리팹에서 나온 투사체끼리 모이는 대기열로 돌려보낸다.
        // 원본 프리팹 자체(sourcePrefab이 null)는 풀에 넣지 않는다
        private void Release()
        {
            gameObject.SetActive(false);

            if (sourcePrefab != null && pools.TryGetValue(sourcePrefab, out Queue<MonsterProjectile> queue))
            {
                queue.Enqueue(this);
            }
        }
    }
}
