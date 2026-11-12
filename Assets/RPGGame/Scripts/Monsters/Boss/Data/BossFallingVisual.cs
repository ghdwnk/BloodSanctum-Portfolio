using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 하늘에서 떨어지는 연출용 오브젝트를 스스로 움직이고 스스로 지운다.
    /// 보스의 코루틴으로 움직이면, 보스가 페이즈 전환·리셋·사망으로 코루틴을 끊을 때
    /// 하늘에 멈춘 채 영영 남는다. 자기 Update로 움직이면 그런 일이 없다
    /// </summary>
    public class BossFallingVisual : MonoBehaviour
    {
        private Vector3 start;
        private Vector3 target;
        private float duration;
        private float elapsed;

        public void Initialize(Vector3 start, Vector3 target, float duration)
        {
            this.start = start;
            this.target = target;
            this.duration = Mathf.Max(0.05f, duration);
            transform.position = start;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;

            // 처음엔 느리고 땅에 가까울수록 빨라지게 해서 "떨어지는" 느낌을 낸다
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(start, target, t * t);

            if (elapsed >= duration)
            {
                Destroy(gameObject);
            }
        }
    }
}
