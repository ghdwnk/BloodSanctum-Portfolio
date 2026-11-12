using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 긴급 회복 상태. 하던 행동을 멈추고 제자리에서 체력이 차오른다.
    /// 회복은 한 번에 채우지 않고 castDuration 동안 나눠 채운다. 한 번에 채우면 플레이어가 회복을 보고 대응할 틈이 없고,
    /// 체력바가 순간이동하듯 바뀐다. 회복 중에도 피해는 그대로 받으므로 화력을 쏟아 부으면 회복을 따라잡을 수 있다
    /// </summary>
    public class BossHealState : BossStateBase
    {
        private HPController hpController;
        private float targetHP;
        private float healPerSecond;
        private float endTime;

        protected override void OnEnable()
        {
            base.OnEnable();

            if (hpController == null)
            {
                hpController = GetComponentInChildren<HPController>();
            }

            // 진행 중이던 패턴 코루틴을 멈춘다 (회복하는 동안 이전 공격이 터지면 안 된다)
            manager.StopAllCoroutines();

            BossData.EmergencyHeal heal = manager.Data.emergencyHeal;
            float duration = Mathf.Max(0.1f, heal.castDuration);
            targetHP = hpController.MaxHP * heal.healToHPRatio;

            // 시작 시점의 체력 기준으로 초당 회복량을 정한다. 이미 목표보다 높으면 회복하지 않는다
            healPerSecond = Mathf.Max(0f, targetHP - hpController.CurrentHP) / duration;
            endTime = Time.time + duration;

            manager.PlayPatternAnimation(heal.animationTrigger);

            if (heal.effectPrefab != null)
            {
                // 보스를 따라다니도록 자식으로 붙인다. 이펙트가 남는 시간은 회복이 끝난 뒤 잔여 파티클이 사라질 만큼 더 준다
                GameObject effect = Instantiate(heal.effectPrefab, refTransform.position, Quaternion.identity, refTransform);
                Destroy(effect, duration + 2f);
            }

            BossSfx.Play(heal.sounds, refTransform.position, manager.Data.soundVolume * heal.volume);

            if (!string.IsNullOrEmpty(heal.dialogue))
            {
                manager.ShowCombatLine(heal.dialogue);
            }
        }

        protected override void Update()
        {
            base.Update();

            if (hpController != null && healPerSecond > 0f)
            {
                // 프레임 시간이 튀어도 목표 체력을 넘겨 채우지 않는다
                float amount = Mathf.Min(healPerSecond * Time.deltaTime, targetHP - hpController.CurrentHP);
                if (amount > 0f)
                {
                    hpController.OnHealed(amount);
                }
            }

            if (Time.time >= endTime)
            {
                manager.SetState(BossStateManager.State.Chase);
            }
        }
    }
}
