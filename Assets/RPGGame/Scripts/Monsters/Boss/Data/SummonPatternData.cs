using System.Collections;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 패턴: 악마 소환 (기획서 13-1절 패턴 3)
    /// 보스 주변 원 위에 일반 몬스터를 불러낸다. 소환 지점마다 먼저 이펙트가 터지고, 잠시 뒤 몬스터가 나타나 곧바로 달려든다
    /// </summary>
    [CreateAssetMenu(fileName = "New Summon Pattern", menuName = "Scriptable Objects/Boss Pattern/Summon")]
    public class SummonPatternData : BossPatternData
    {
        [Header("소환")]
        [Tooltip("스포너에 등록된 몬스터 ID(EnemyData.enemyId). 비우면 가중치 무작위로 뽑는다")]
        public string summonEnemyId = "Chomper";
        public int summonCount = 3;
        public int summonLevel = 1;
        [Tooltip("소환 모션이 끝날 때까지의 대기 시간")]
        public float windUpTime = 0.8f;

        [Header("소환 연출")]
        [Tooltip("보스 중심에서 소환 지점까지의 거리. 소환 지점은 이 반지름의 원 위에 같은 간격으로 놓인다")]
        public float summonRadius = 4.5f;
        [Tooltip("소환 지점마다 먼저 터뜨리는 이펙트")]
        public GameObject spawnEffectPrefab;
        [Tooltip("이펙트가 터진 뒤 몬스터가 나타나기까지의 시간")]
        public float spawnDelay = 0.45f;
        [Tooltip("몬스터가 하나씩 나타나는 간격")]
        public float spawnStagger = 0.15f;
        [Tooltip("이전 소환수가 남아 있어도 한꺼번에 이 수를 넘기지 않는다 (소환만 반복해 판이 몬스터로 가득 차는 것을 막는다)")]
        public int maxAliveMinions = 6;

        public override void Execute(BossPatternContext context)
        {
            context.Caster.StartCoroutine(SummonRoutine(context));
        }

        private IEnumerator SummonRoutine(BossPatternContext context)
        {
            yield return new WaitForSeconds(windUpTime);

            Transform boss = context.BossTransform;
            SpawnEffect(effectPrefab, boss.position, boss.rotation);

            BossStateManager manager = context.Caster as BossStateManager;
            int alive = manager != null ? manager.AliveMinionCount : 0;
            int count = Mathf.Min(summonCount, Mathf.Max(0, maxAliveMinions - alive));
            if (count <= 0)
            {
                yield break;
            }

            // 실제로 소환이 일어날 때만 재생한다 (소환수 상한에 걸려 아무도 안 나오면 소리도 내지 않는다)
            PlaySound(context, boss.position);

            // 소환 지점은 매번 다른 각도에서 시작해 패턴이 뻔하지 않게 한다
            float startAngle = Random.Range(0f, 360f);
            Vector3[] points = new Vector3[count];
            for (int ix = 0; ix < count; ++ix)
            {
                float angle = startAngle + ix * 360f / count;
                points[ix] = boss.position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * summonRadius;

                if (Util.SampleGroundHeight(points[ix], out float groundY))
                {
                    points[ix].y = groundY;
                }

                SpawnEffect(spawnEffectPrefab, points[ix], Quaternion.identity);
            }

            yield return new WaitForSeconds(spawnDelay);

            for (int ix = 0; ix < count; ++ix)
            {
                // 보스가 도중에 쓰러졌거나 리셋됐다면 더 부르지 않는다
                if (boss == null || (manager != null && manager.IsDead))
                {
                    yield break;
                }

                Vector3 toPlayer = context.PlayerTransform != null
                    ? context.PlayerTransform.position - points[ix]
                    : boss.forward;
                toPlayer.y = 0f;
                Quaternion rotation = toPlayer.sqrMagnitude > 0.001f ? Quaternion.LookRotation(toPlayer) : boss.rotation;

                MonsterStateManager minion = MonsterSpawner.SpawnMonsterAt(points[ix], rotation, summonLevel,
                    string.IsNullOrEmpty(summonEnemyId) ? null : summonEnemyId);

                if (minion != null)
                {
                    minion.AlertToPlayer();
                    manager?.RegisterMinion(minion);
                }

                yield return new WaitForSeconds(spawnStagger);
            }
        }
    }
}
