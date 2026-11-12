using System.Collections;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 패턴: 투사체 발사
    /// 준비 동작 동안 날아갈 경로를 바닥에 예고하고, 끝나면 투사체를 쏜다.
    /// 방향은 예고를 띄우는 순간 정해지므로 플레이어가 옆으로 움직이면 피할 수 있다.
    /// 투사체 개수와 퍼지는 각도를 바꿔 한 발 / 부채꼴 연발을 같은 클래스로 표현한다
    /// </summary>
    [CreateAssetMenu(fileName = "New Projectile Pattern", menuName = "Scriptable Objects/Boss Pattern/Projectile")]
    public class ProjectilePatternData : BossPatternData
    {
        [Header("투사체")]
        public MonsterProjectile projectilePrefab;
        public int projectileCount = 1;
        [Tooltip("가장 바깥 두 발 사이의 각도. 한 발이면 쓰지 않는다")]
        public float spreadAngle = 0f;
        // 보스 기준 로컬 발사 위치
        public Vector3 spawnOffset = new Vector3(0f, 1.4f, 1.5f);
        public float windUpTime = 0.6f;

        [Header("예고")]
        public float telegraphLength = 14f;
        public float telegraphWidth = 1.4f;

        [Header("화상")]
        // 투사체에 맞으면 걸리는 화상. 연발을 여러 발 맞아도 쌓이지 않고 지속 시간만 갱신된다
        public BossBurnSettings burn = new BossBurnSettings();

        public override void Execute(BossPatternContext context)
        {
            context.Caster.StartCoroutine(FireRoutine(context));
        }

        private IEnumerator FireRoutine(BossPatternContext context)
        {
            Vector3 toPlayer = context.PlayerTransform.position - context.BossTransform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.0001f)
            {
                toPlayer = context.BossTransform.forward;
            }

            toPlayer.Normalize();

            // 예고 선의 시작점. 투사체도 반드시 여기서 출발해야 예고한 선 위로 날아간다
            Vector3 origin = context.BossTransform.position;

            int count = Mathf.Max(1, projectileCount);
            Vector3[] directions = new Vector3[count];
            BossTelegraphView[] telegraphs = new BossTelegraphView[count];

            for (int ix = 0; ix < count; ++ix)
            {
                float angle = count == 1 ? 0f : Mathf.Lerp(-spreadAngle * 0.5f, spreadAngle * 0.5f, ix / (float)(count - 1));
                directions[ix] = Quaternion.Euler(0f, angle, 0f) * toPlayer;

                telegraphs[ix] = BossTelegraph.Line(origin, directions[ix], telegraphLength, telegraphWidth, windUpTime);
            }

            yield return new WaitForSeconds(windUpTime);

            // 투사체가 나가는 순간 경로 예고는 바로 치운다
            foreach (BossTelegraphView telegraph in telegraphs)
            {
                if (telegraph != null)
                {
                    telegraph.Dismiss();
                }
            }

            if (projectilePrefab == null)
            {
                yield break;
            }

            // 연발이어도 소리는 발사 순간에 한 번만 낸다
            PlaySound(context, origin + Quaternion.LookRotation(toPlayer) * spawnOffset);

            for (int ix = 0; ix < count; ++ix)
            {
                // 발사 위치는 보스가 지금 바라보는 쪽(BossTransform.forward)이 아니라 이 투사체의 예고 선 방향으로 밀어낸다.
                // 준비 동작 중 보스가 돌아서 있으면 보스 정면 기준으로는 예고 선의 옆에서 출발해 빗나가 보였다
                Vector3 spawnPosition = origin + Quaternion.LookRotation(directions[ix]) * spawnOffset;
                MonsterProjectile projectile = MonsterProjectile.Get(projectilePrefab, spawnPosition, Quaternion.LookRotation(directions[ix]));
                // 명중해서 피해가 실제로 들어갔을 때 투사체가 이 콜백으로 화상을 건다
                float attackPower = context.AttackPower;
                System.Action<Damageable> onHit = null;
                if (burn.Enabled)
                {
                    onHit = target => burn.Apply(target, attackPower);
                }

                projectile.Launch(attackPower * damageMultiplier, context.TargetLayer, onHit);
            }
        }
    }
}
