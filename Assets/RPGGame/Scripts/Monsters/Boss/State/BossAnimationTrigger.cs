using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 보스의 Animator에 트리거를 전달하는 얇은 래퍼.
    /// 패턴 애셋은 트리거 "이름"만 알고 있으면 되므로, Animator 참조는 여기서만 다룬다
    /// </summary>
    public class BossAnimationTrigger : MonoBehaviour
    {
        [SerializeField] private Animator refAnimator;
        private BossStateManager manager;

        private void Awake()
        {
            if (refAnimator == null)
            {
                refAnimator = GetComponentInParent<Animator>();
            }

            // 상태가 바뀌면 애니메이터의 State 파라미터를 자동으로 맞춘다
            // (MonsterAnimationController와 같은 방식이라 인스펙터 배선이 필요 없다)
            manager = GetComponentInParent<BossStateManager>();
            if (manager != null)
            {
                manager.SubscribeOnStateChanged(OnStateChanged);
            }
        }

        private void OnStateChanged(BossStateManager.State state)
        {
            SetState((int)state);

            if (state == BossStateManager.State.Dead)
            {
                // 사망 시점에 남아 있던 패턴/피격 트리거를 모두 지운다.
                // 지우지 않으면 Any State -> 패턴 전환이 사망 애니메이션을 덮어써 버린다
                ResetAllTriggers();
            }
        }

        // 피격 시 호출 (Damageable의 OnDamageReceived에 연결)
        public void OnDamaged()
        {
            // 패턴 실행 중에는 피격 모션을 재생하지 않는다 (MonsterAnimationController와 같은 이유).
            // 보스는 패턴 코루틴이 별도로 돌아 피해 판정 자체는 나가지만, Hit가 Any State 전환으로
            // 패턴 모션을 끊어 버려 "공격도 안 했는데 맞았다"처럼 보였다. 페이즈 전환 연출도 같은 이유로 보호한다
            if (manager != null && manager.IsPerformingAction)
            {
                return;
            }

            PlayTrigger("Hit");
        }

        public void PlayTrigger(string triggerName)
        {
            if (refAnimator == null || string.IsNullOrEmpty(triggerName))
            {
                return;
            }

            // 죽은 뒤 들어온 트리거는 무시한다 (마지막 일격의 피격 트리거 포함)
            if (manager != null && manager.IsDead)
            {
                return;
            }

            refAnimator.SetTrigger(triggerName);
        }

        // 대기 중인 트리거를 모두 지우는 함수.
        // 보스마다 패턴 트리거 이름이 달라서 이름을 하드코딩하지 않고 파라미터 목록을 훑는다.
        // 플레이어 사망으로 보스전을 리셋할 때도 쓰므로 공개한다
        public void ResetAllTriggers()
        {
            if (refAnimator == null)
            {
                return;
            }

            foreach (AnimatorControllerParameter parameter in refAnimator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    refAnimator.ResetTrigger(parameter.nameHash);
                }
            }
        }

        public void SetState(int stateIndex)
        {
            if (refAnimator == null)
            {
                return;
            }

            refAnimator.SetInteger("State", stateIndex);
        }
    }
}
