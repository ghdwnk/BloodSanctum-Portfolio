using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 플레이어가 피해를 받을 때 카메라를 짧게 흔드는 컴포넌트.
    ///
    /// 플레이어의 Damageable.OnDamageReceived 이벤트에 인스펙터로 Play()가 연결돼 있다.
    /// 실제 흔들림은 CameraRig가 처리한다. CameraRig는 매 LateUpdate마다
    /// "기본 위치 + 줌 + 흔들림"으로 카메라 위치를 새로 조립하므로, 여기서 카메라 위치를 직접 바꾸면
    /// 같은 프레임에 덮어써져 아무 효과가 없다. 그래서 흔들림 요청만 CameraRig에 넘긴다.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        [Tooltip("흔들리는 시간(초). 시간이 지날수록 약해지다 멈춘다")]
        [SerializeField] private float shakeTime = 0.4f;

        [Tooltip("흔들림 세기(m). 카메라가 원래 자리에서 최대 이만큼 벗어난다")]
        [SerializeField] private float shakeAmount = 0.05f;

        /// <summary>
        /// 흔들림을 시작한다. UnityEvent(피격 이벤트)에서 호출되므로 이름과 매개변수를 바꾸지 않는다.
        /// 더 강한 흔들림이 진행 중이면 CameraRig가 약한 요청을 무시한다.
        /// </summary>
        public void Play()
        {
            CameraRig.AddShake(shakeAmount, shakeTime);
        }
    }
}
