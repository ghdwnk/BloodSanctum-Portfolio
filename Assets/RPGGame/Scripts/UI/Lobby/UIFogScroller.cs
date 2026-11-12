using UnityEngine;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 반복되는 안개 텍스처를 천천히 흘려 보내는 연출. RawImage의 UV를 이동시킨다.
    /// 속도와 반복 횟수가 다른 층을 겹치면 안개가 깊이를 가지고 흐르는 것처럼 보인다
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class UIFogScroller : MonoBehaviour
    {
        // 초당 UV 이동량 (1이면 텍스처 한 장 분량)
        [SerializeField] private Vector2 speed = new Vector2(0.01f, 0f);
        // 화면에 텍스처가 몇 번 반복돼 보이는지. 클수록 안개 덩어리가 작아진다
        [SerializeField] private Vector2 tiling = new Vector2(1.5f, 1f);

        private RawImage image;
        private Vector2 offset;

        private void Awake()
        {
            image = GetComponent<RawImage>();
        }

        private void Update()
        {
            offset += speed * Time.unscaledDeltaTime;
            // 값이 계속 커지면 부동소수점 정밀도가 떨어지므로 한 바퀴마다 되돌린다
            offset.x -= Mathf.Floor(offset.x);
            offset.y -= Mathf.Floor(offset.y);

            image.uvRect = new Rect(offset, tiling);
        }
    }
}
