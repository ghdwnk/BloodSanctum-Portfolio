using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// BossTelegraph가 만든 예고 오브젝트를 실제로 차오르게 하고, 시간이 다 되면 스스로 지운다
    /// </summary>
    public class BossTelegraphView : MonoBehaviour
    {
        // 예고가 끝나기 이만큼 전에 사라진다 (초). 채움이 거의 다 찬 모습까지만 보여 준다
        private const float DismissLead = 0.06f;

        private Transform fill;
        private bool isLine;
        private float size;
        private float width;
        private float duration;
        private float elapsed;

        public void Initialize(Transform fill, bool isLine, float size, float width, float duration)
        {
            this.fill = fill;
            this.isLine = isLine;
            this.size = size;
            this.width = width;
            this.duration = Mathf.Max(0.01f, duration);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (fill != null)
            {
                fill.localScale = isLine
                    ? new Vector3(width, 1f, size * t)
                    : new Vector3(size * t, 1f, size * t);
            }

            // 다 차는 순간이 피해가 들어가는 순간이다. 발동 장면(장판·폭발 이펙트)과 한 프레임이라도 겹쳐 보이지 않도록
            // 끝나기 직전(DismissLead)에 먼저 치운다. 패턴이 발동 시점에 직접 Dismiss를 불러도 같은 결과다
            if (elapsed >= duration - DismissLead)
            {
                Dismiss();
            }
        }

        /// <summary>
        /// 예고를 즉시 치운다. 패턴이 공격을 발동하는 순간 직접 호출해, 예고 자신의 타이머와 패턴의 타이머가
        /// 한두 프레임 어긋나도 공격이 시작된 뒤에 예고가 남아 보이지 않게 한다.
        /// Destroy만 하면 그 프레임이 끝날 때까지 남으므로 먼저 꺼서 바로 안 보이게 한다
        /// </summary>
        public void Dismiss()
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            BossTelegraph.Unregister(this);
        }
    }
}
