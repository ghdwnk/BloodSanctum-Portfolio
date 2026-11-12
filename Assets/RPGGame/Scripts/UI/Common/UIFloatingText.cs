using TMPro;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 월드에 잠깐 떠올랐다 사라지는 숫자/문구. 회복량 표시 등에 쓴다.
    /// 생성한 쪽에서 값만 넣어주면 스스로 떠오르고 사라진 뒤 파괴된다.
    /// </summary>
    public class UIFloatingText : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI text;
        [SerializeField] private float riseSpeed = 1.2f;
        [SerializeField] private float lifeTime = 1.1f;
        // 수명의 뒤쪽 이 비율 동안 서서히 사라진다
        [SerializeField, Range(0f, 1f)] private float fadeStartRatio = 0.45f;

        private Transform refTransform;
        private Camera mainCamera;
        private float elapsed;
        private Color baseColor;

        private void Awake()
        {
            refTransform = transform;
            mainCamera = Camera.main;
            baseColor = text != null ? text.color : Color.white;
        }

        public void Setup(string content, Color color)
        {
            if (text != null)
            {
                text.text = content;
                text.color = color;
            }

            baseColor = color;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;

            refTransform.position += Vector3.up * riseSpeed * Time.deltaTime;

            // 항상 카메라를 향하게 한다 (UIBillboard와 같은 방식)
            if (mainCamera != null)
            {
                refTransform.forward = mainCamera.transform.forward;
            }

            float fadeStart = lifeTime * fadeStartRatio;
            if (elapsed > fadeStart && text != null)
            {
                float t = Mathf.InverseLerp(fadeStart, lifeTime, elapsed);
                text.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f - t);
            }

            if (elapsed >= lifeTime)
            {
                Destroy(gameObject);
            }
        }
    }
}
