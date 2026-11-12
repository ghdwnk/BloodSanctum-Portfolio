using System.Collections;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 패턴: 지속 장판 (자라톤 암흑 파동, 종말의 파멸)
    /// 준비 동작 동안 범위를 예고한 뒤, 그 자리에 장판을 남긴다. 장판 안에 서 있으면 일정 간격으로 피해를 입으므로
    /// 한 번 피하는 것이 아니라 범위 밖으로 벗어나 있어야 한다
    /// </summary>
    [CreateAssetMenu(fileName = "New Area Dot Pattern", menuName = "Scriptable Objects/Boss Pattern/Area Dot")]
    public class AreaDotPatternData : BossPatternData
    {
        public override bool IsGroundZone => true;

        [Header("장판")]
        [Tooltip("예고가 차오르는 시간. 다 차면 장판이 생긴다")]
        public float windUpTime = 1.6f;
        [Tooltip("시전이 시작되고 바닥 예고가 나타나기까지 늦추는 시간. 장판이 펴지는 시각은 그대로(windUpTime)라 예고가 보이는 시간만 그만큼 짧아진다. 0이면 시전과 동시에 보인다")]
        public float telegraphDelay = 0f;
        public float zoneRadius = 7f;
        [Tooltip("장판이 남아 있는 시간")]
        public float zoneDuration = 4f;
        [Tooltip("피해가 들어가는 간격")]
        public float tickInterval = 1f;
        // 장판이 펴지는 순간 범위 안의 대상에게 한 번 주는 피해: 대상의 현재 체력의 이 비율 (0이면 없음, 방어력 무시).
        // 이 첫 피해 뒤로 장판 위에 서 있는 동안 tickInterval마다 지속 피해가 들어간다
        [Range(0f, 0.95f)] public float activateHpRatio = 0f;

        [Header("카메라 흔들림 (m, 0이면 없음)")]
        [Tooltip("예고가 차오르는 동안 0.25배에서 이 값까지 점점 세지는 잔떨림")]
        public float windUpShake = 0f;
        [Tooltip("장판이 생기는 순간의 흔들림과 그 지속 시간")]
        public float activateShake = 0f;
        public float activateShakeDuration = 0.4f;

        // 틱 한 번의 피해는 보스 공격력 x damageMultiplier. (한 번에 주는 패턴과 달리 틱 수만큼 곱해져 들어간다)
        // 총 피해 = 공격력 x damageMultiplier x (zoneDuration / tickInterval)

        public override void Execute(BossPatternContext context)
        {
            context.Caster.StartCoroutine(ZoneRoutine(context));
        }

        private IEnumerator ZoneRoutine(BossPatternContext context)
        {
            // 장판 중심은 시전 시작 시점의 보스 위치로 정해 바닥에 미리 보여 준다
            Vector3 center = context.BossTransform.position;
            // 예고는 telegraphDelay만큼 늦게 나타나고, 장판이 펴지는 시각(windUpTime)에 맞춰 차오른다
            float delay = Mathf.Clamp(telegraphDelay, 0f, windUpTime - 0.1f);
            BossTelegraphView telegraph = null;

            // 예고가 차오르는 동안 화면을 떨어 압박감을 준다. 시간이 갈수록 세져 발동 직전에 최대가 된다.
            // 예고(BossTelegraphView)와 같은 시간을 쓰도록 WaitForSeconds 대신 직접 시간을 센다
            float startTime = Time.time;
            while (Time.time - startTime < windUpTime)
            {
                if (telegraph == null && Time.time - startTime >= delay)
                {
                    telegraph = BossTelegraph.Circle(center, zoneRadius, windUpTime - delay);
                }

                if (windUpShake > 0f)
                {
                    float progress = Mathf.Clamp01((Time.time - startTime) / windUpTime);
                    CameraRig.AddShake(windUpShake * Mathf.Lerp(0.25f, 1f, progress), 0.15f);
                }

                yield return null;
            }

            // 장판이 생기는 순간 예고는 바로 치운다 (아래 Zone 표시가 그 자리를 이어받는다)
            if (telegraph != null)
            {
                telegraph.Dismiss();
            }

            CameraRig.AddShake(activateShake, activateShakeDuration);

            // 장판 자체는 보스 코루틴과 무관하게 스스로 동작하고 스스로 사라진다.
            // 바닥에 붉은 판을 따로 깔지 않는다 (예고와 같은 색이라 장판 이펙트 밑에서 예고가 남은 것처럼 겹쳐 보였다)
            SpawnEffect(effectPrefab, center, context.BossTransform.rotation, zoneDuration);
            PlaySound(context, center);
            BossDamageZone.Spawn(center, zoneRadius, zoneDuration, tickInterval,
                context.AttackPower * damageMultiplier, context.TargetLayer, activateHpRatio);
        }
    }
}
