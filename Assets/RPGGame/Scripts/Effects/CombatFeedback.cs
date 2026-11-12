using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 플레이어 기본 공격이 적중했을 때의 타격감 연출을 한 곳에서 처리하는 컴포넌트.
    ///
    /// 연출은 네 가지다.
    ///  1. 히트스톱   : 공격자와 피격자의 애니메이터만 아주 잠깐 멈춘다
    ///  2. 화면 흔들림 : CameraRig.AddShake로 카메라를 흔든다
    ///  3. 스파크     : 맞은 위치에 짧은 불꽃 이펙트를 띄운다 (오브젝트 풀로 재사용)
    ///  4. 피격 반짝임 : 맞은 몬스터에 MonsterHitFlash가 있으면 몸을 잠깐 하얗게 빛낸다
    ///
    /// 호출하는 곳: Player/Weapon이 타격이 성립한 순간 정적 함수 PlayHit을 부른다.
    /// 무기는 "맞았다"는 사실만 알리면 되고, 연출 세기는 전부 이 컴포넌트의 인스펙터에서 조절한다.
    /// 일반 피격음은 여기서 내지 않는다. 몬스터마다 붙은 AudioPlayer가 Damageable.OnDamageReceived 이벤트로 낸다.
    /// 치명타일 때만 여기서 criticalHitSound를 한 겹 더 재생한다.
    ///
    /// 게임 씬의 [Managers]/Combat Feedback 오브젝트에 하나만 둔다. 씬에 없으면 연출 없이 조용히 넘어간다.
    /// </summary>
    public class CombatFeedback : MonoBehaviour
    {
        // 풀에 들어가는 스파크 사본 하나.
        // 재생할 때마다 GetComponentsInChildren을 부르면 배열이 새로 할당되므로, 처음 만들 때 파티클 목록을 같이 저장해 둔다
        private sealed class Spark
        {
            public GameObject root;
            public ParticleSystem[] particles;
            public Queue<Spark> pool;   // 다 쓴 뒤 돌아갈 풀 (프리팹마다 따로 있다)
            public float returnTime;    // 풀로 돌려보낼 시각 (Time.time 기준)
        }

        private static CombatFeedback instance;

        [Header("히트스톱 (초)")]
        [Tooltip("일반 타격 때 애니메이션을 멈추는 시간")]
        [SerializeField] private float normalHitStop = 0.05f;
        [Tooltip("치명타 때 애니메이션을 멈추는 시간")]
        [SerializeField] private float criticalHitStop = 0.09f;
        [Tooltip("콤보 마지막 타격 때 애니메이션을 멈추는 시간")]
        [SerializeField] private float finisherHitStop = 0.11f;

        [Header("화면 흔들림")]
        [Tooltip("일반 타격 때 흔들림 세기(m)")]
        [SerializeField] private float normalShake = 0.03f;
        [Tooltip("치명타 때 흔들림 세기(m)")]
        [SerializeField] private float criticalShake = 0.07f;
        [Tooltip("콤보 마지막 타격 때 흔들림 세기(m)")]
        [SerializeField] private float finisherShake = 0.09f;
        [Tooltip("흔들림이 이어지는 시간(초)")]
        [SerializeField] private float shakeDuration = 0.12f;

        [Header("치명타 효과음")]
        [Tooltip("치명타로 맞혔을 때 겹쳐서 내는 타격음 (고철 피격음). 일반 타격은 몬스터 쪽 AudioPlayer가 내므로 여기서는 내지 않는다. 비워 두면 소리 없음")]
        [SerializeField] private AudioClip criticalHitSound;
        [Tooltip("치명타 타격음의 음량 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float criticalHitSoundVolume = 0.9f;

        [Header("타격 스파크")]
        [Tooltip("일반 타격 때 맞은 위치에 띄우는 이펙트")]
        [SerializeField] private GameObject hitSparkPrefab;
        [Tooltip("치명타·콤보 마지막 타격 때 띄우는 이펙트. 비어 있으면 일반 이펙트를 쓴다")]
        [SerializeField] private GameObject criticalSparkPrefab;
        [Tooltip("이펙트를 띄운 뒤 풀로 돌려보내기까지의 시간(초)")]
        [SerializeField] private float sparkLifetime = 0.5f;
        [Tooltip("파티클을 뿜는 시간(초). 원본 이펙트는 5초 반복 재생이라, 사본을 만들 때 이 값으로 줄인다")]
        [SerializeField] private float sparkEmitDuration = 0.1f;
        [Tooltip("파티클 하나의 최대 수명(초). 원본은 8초라, 사본을 만들 때 이 값 이하로 줄인다")]
        [SerializeField] private float sparkParticleLifetime = 0.35f;

        // ───── 히트스톱 상태 ─────
        // 멈춘 애니메이터와 멈추기 전의 재생 속도
        private readonly Dictionary<Animator, float> frozenAnimators = new Dictionary<Animator, float>();
        // 애니메이터를 다시 움직일 시각 (Time.unscaledTime 기준)
        private float freezeEndTime;

        // ───── 스파크 풀 ─────
        // 프리팹별 대기열. 타격마다 Instantiate/Destroy를 하면 연타 구간에서 GC가 쌓이므로 사본을 돌려 쓴다
        private readonly Dictionary<GameObject, Queue<Spark>> sparkPools = new Dictionary<GameObject, Queue<Spark>>();
        // 화면에 떠 있는 스파크. 수명이 모두 같아 먼저 띄운 것이 먼저 끝나므로 Queue 앞에서부터 확인하면 된다
        private readonly Queue<Spark> activeSparks = new Queue<Spark>();

        private void Awake()
        {
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void OnDisable()
        {
            // 멈춘 채로 컴포넌트가 꺼지면 애니메이터가 영영 멈춰 있게 되므로 바로 풀어 준다
            RestoreFrozenAnimators();
        }

        // 코루틴 대신 매 프레임 시각만 비교한다. 할 일이 없으면 두 줄의 비교로 끝난다
        private void Update()
        {
            // 히트스톱은 일시정지(timeScale 0) 중에도 풀려야 하므로 unscaledTime으로 잰다
            if (frozenAnimators.Count > 0 && Time.unscaledTime >= freezeEndTime)
            {
                RestoreFrozenAnimators();
            }

            ReturnFinishedSparks();
        }

        // ───────────────────────── 외부 호출 ─────────────────────────

        /// <summary>
        /// 기본 공격이 적중한 순간 Weapon이 호출한다.
        /// </summary>
        /// <param name="point">맞은 위치. 스파크가 여기에 뜬다</param>
        /// <param name="target">맞은 대상. 이 대상의 애니메이터를 멈추고 피격 반짝임을 켠다 (null 가능)</param>
        /// <param name="attackerAnimator">공격자의 애니메이터. 히트스톱으로 함께 멈춘다 (null 가능)</param>
        /// <param name="isCritical">치명타 여부</param>
        /// <param name="isFinisher">콤보 마지막 타격 여부</param>
        public static void PlayHit(Vector3 point, Damageable target, Animator attackerAnimator, bool isCritical, bool isFinisher)
        {
            if (instance != null)
            {
                instance.Play(point, target, attackerAnimator, isCritical, isFinisher);
            }
        }

        private void Play(Vector3 point, Damageable target, Animator attackerAnimator, bool isCritical, bool isFinisher)
        {
            // 강한 타격일수록 길게 멈추고 크게 흔든다 (콤보 마지막 > 치명타 > 일반)
            float hitStop = isFinisher ? finisherHitStop : isCritical ? criticalHitStop : normalHitStop;
            float shake = isFinisher ? finisherShake : isCritical ? criticalShake : normalShake;

            Animator targetAnimator = target != null ? target.GetComponentInParent<Animator>() : null;
            HitStop(hitStop, attackerAnimator, targetAnimator);

            CameraRig.AddShake(shake, shakeDuration);

            // 치명타일 때만 타격음을 한 겹 더 얹어 "크게 들어갔다"는 느낌을 준다 (null이면 SfxPlayer가 무시한다)
            if (isCritical)
            {
                SfxPlayer.Play(criticalHitSound, criticalHitSoundVolume);
            }

            bool strongHit = isCritical || isFinisher;
            SpawnSpark(strongHit && criticalSparkPrefab != null ? criticalSparkPrefab : hitSparkPrefab, point);

            if (target != null)
            {
                MonsterHitFlash flash = target.GetComponentInParent<MonsterHitFlash>();
                if (flash != null)
                {
                    flash.Flash(isCritical);
                }
            }
        }

        // ───────────────────────── 히트스톱 ─────────────────────────

        // 공격자와 피격자의 애니메이션만 멈춘다.
        // Time.timeScale을 낮추면 쿨타임·UI·다른 몬스터까지 같이 멈추므로, 맞은 두 캐릭터의 애니메이터만 얼린다
        private void HitStop(float duration, Animator attacker, Animator target)
        {
            if (duration <= 0f)
            {
                return;
            }

            Freeze(attacker);
            Freeze(target);

            // 연타로 겹치면 해제 시각만 뒤로 미룬다
            freezeEndTime = Mathf.Max(freezeEndTime, Time.unscaledTime + duration);
        }

        private void Freeze(Animator animator)
        {
            // 이미 멈춘 애니메이터를 다시 등록하면 원래 속도 대신 0이 저장되므로 건너뛴다
            if (animator == null || frozenAnimators.ContainsKey(animator))
            {
                return;
            }

            frozenAnimators.Add(animator, animator.speed);
            animator.speed = 0f;
        }

        private void RestoreFrozenAnimators()
        {
            foreach (KeyValuePair<Animator, float> pair in frozenAnimators)
            {
                // 멈춘 사이에 다른 코드가 속도를 바꿨으면(예: 공격이 끝나 이동 상태로 바뀌며 속도를 다시 계산함)
                // 그 값이 최신이므로 덮어쓰지 않는다
                if (pair.Key != null && pair.Key.speed == 0f)
                {
                    pair.Key.speed = pair.Value;
                }
            }

            frozenAnimators.Clear();
        }

        // ───────────────────────── 스파크 ─────────────────────────

        private void SpawnSpark(GameObject prefab, Vector3 point)
        {
            if (prefab == null)
            {
                return;
            }

            Spark spark = TakeSpark(prefab);

            spark.root.transform.position = point;
            spark.root.SetActive(true);

            // 풀에서 꺼낸 사본은 이전 재생의 파티클이 남아 있을 수 있으므로 지우고 처음부터 재생한다
            for (int ix = 0; ix < spark.particles.Length; ++ix)
            {
                spark.particles[ix].Clear(false);
                spark.particles[ix].Play(false);
            }

            spark.returnTime = Time.time + sparkLifetime;
            activeSparks.Enqueue(spark);
        }

        // 풀에 남은 사본을 꺼내고, 없으면 새로 만든다
        private Spark TakeSpark(GameObject prefab)
        {
            if (!sparkPools.TryGetValue(prefab, out Queue<Spark> pool))
            {
                pool = new Queue<Spark>();
                sparkPools.Add(prefab, pool);
            }

            while (pool.Count > 0)
            {
                Spark pooled = pool.Dequeue();

                // 씬 정리 등으로 사본이 파괴됐으면 버리고 다음 것을 본다
                if (pooled.root != null)
                {
                    return pooled;
                }
            }

            // 하이어라키가 어지럽지 않게 이 오브젝트 아래에 둔다 (이 오브젝트는 원점·회전 없음·크기 1이라 위치에 영향이 없다)
            GameObject root = Instantiate(prefab, transform);
            Spark created = new Spark
            {
                root = root,
                particles = root.GetComponentsInChildren<ParticleSystem>(true),
                pool = pool,
            };

            TrimSpark(created.particles);
            return created;
        }

        // 원본 이펙트의 반복 재생·긴 수명을 끄고, 타격 순간에만 짧게 터지는 이펙트로 바꾼다.
        // 원본 프리팹은 건드리지 않고 생성한 사본만 고친다
        private void TrimSpark(ParticleSystem[] particles)
        {
            foreach (ParticleSystem particle in particles)
            {
                // 재생 중에는 duration을 바꿀 수 없으므로 먼저 멈춘다
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);

                ParticleSystem.MainModule main = particle.main;
                main.loop = false;
                main.duration = sparkEmitDuration;

                ParticleSystem.MinMaxCurve lifetime = main.startLifetime;
                switch (lifetime.mode)
                {
                    case ParticleSystemCurveMode.Constant:
                        lifetime.constant = Mathf.Min(lifetime.constant, sparkParticleLifetime);
                        break;
                    case ParticleSystemCurveMode.TwoConstants:
                        lifetime.constantMax = Mathf.Min(lifetime.constantMax, sparkParticleLifetime);
                        lifetime.constantMin = Mathf.Min(lifetime.constantMin, lifetime.constantMax);
                        break;
                    default:
                        // 곡선 모드는 값을 일일이 줄이기 어려워 고정값으로 바꾼다
                        lifetime = new ParticleSystem.MinMaxCurve(sparkParticleLifetime);
                        break;
                }

                main.startLifetime = lifetime;
            }
        }

        // 수명이 다한 스파크를 꺼서 풀로 돌려보낸다.
        // 수명은 게임 시간(Time.time)으로 재므로 일시정지 중에는 스파크가 화면에 그대로 남는다
        private void ReturnFinishedSparks()
        {
            while (activeSparks.Count > 0 && Time.time >= activeSparks.Peek().returnTime)
            {
                Spark spark = activeSparks.Dequeue();

                if (spark.root != null)
                {
                    spark.root.SetActive(false);
                    spark.pool.Enqueue(spark);
                }
            }
        }
    }
}
