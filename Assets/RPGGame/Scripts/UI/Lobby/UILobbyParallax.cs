using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGGame
{
    /// <summary>
    /// 로비 배경에 깊이감을 주는 연출. 마우스 위치에 따라 레이어마다 다른 양으로 움직이고(시차),
    /// 배경은 숨 쉬듯 천천히 확대/축소하며 미세하게 흔들린다.
    /// 레이어를 움직일 때 가장자리가 드러나지 않도록 baseScale을 1보다 크게 잡아 둔다
    /// </summary>
    public class UILobbyParallax : MonoBehaviour
    {
        [System.Serializable]
        public class Layer
        {
            public RectTransform target;
            // 마우스가 화면 끝에 있을 때 움직이는 거리(px). 음수면 마우스 반대 방향으로 움직인다
            public float moveAmount = 20f;
            public float baseScale = 1f;
            // false면 크기는 건드리지 않는다 (로고처럼 다른 스크립트가 크기를 연출하는 레이어용)
            public bool controlScale = true;
            // 숨 쉬듯 커졌다 작아지는 정도(배율)와 한 주기(초)
            public float breathAmount = 0f;
            public float breathPeriod = 24f;
            // 마우스와 무관하게 천천히 떠도는 흔들림 거리(px)
            public float swayAmount = 0f;
        }

        [SerializeField] private Layer[] layers;
        [Tooltip("마우스를 따라가는 부드러움. 클수록 빨리 따라간다")]
        [SerializeField] private float followSpeed = 3f;

        private Vector2[] basePositions;
        private Vector2 current;

        private void Awake()
        {
            basePositions = new Vector2[layers.Length];

            for (int i = 0; i < layers.Length; ++i)
            {
                basePositions[i] = layers[i].target != null ? layers[i].target.anchoredPosition : Vector2.zero;
            }
        }

        private void Update()
        {
            Vector2 goal = Vector2.zero;

            if (Mouse.current != null)
            {
                Vector2 pointer = Mouse.current.position.ReadValue();
                goal.x = Mathf.Clamp(pointer.x / Screen.width * 2f - 1f, -1f, 1f);
                goal.y = Mathf.Clamp(pointer.y / Screen.height * 2f - 1f, -1f, 1f);
            }

            // 프레임 속도와 무관하게 같은 속도로 따라가도록 지수 보간을 쓴다
            current = Vector2.Lerp(current, goal, 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime));

            float time = Time.unscaledTime;

            for (int i = 0; i < layers.Length; ++i)
            {
                Layer layer = layers[i];
                if (layer.target == null)
                {
                    continue;
                }

                Vector2 sway = new Vector2(Mathf.Sin(time * 0.31f), Mathf.Cos(time * 0.23f)) * layer.swayAmount;
                layer.target.anchoredPosition = basePositions[i] + current * layer.moveAmount + sway;

                if (!layer.controlScale)
                {
                    continue;
                }

                float breath = layer.breathAmount > 0f
                    ? layer.breathAmount * (0.5f + 0.5f * Mathf.Sin(time * Mathf.PI * 2f / layer.breathPeriod))
                    : 0f;
                layer.target.localScale = Vector3.one * (layer.baseScale + breath);
            }
        }
    }
}
