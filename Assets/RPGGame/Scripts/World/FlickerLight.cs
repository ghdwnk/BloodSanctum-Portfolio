using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 횃불·화로 불빛이 타오르듯 밝기가 흔들리게 한다.
    /// 라이트마다 위상을 다르게 줘서 여러 개가 한꺼번에 깜빡이지 않는다
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class FlickerLight : MonoBehaviour
    {
        // 기준 밝기에서 흔들리는 폭 (0.2면 ±20%)
        [SerializeField, Range(0f, 1f)] private float amplitude = 0.25f;
        [SerializeField] private float speed = 3f;

        private Light target;
        private float baseIntensity;
        private float seed;

        private void Awake()
        {
            target = GetComponent<Light>();
            baseIntensity = target.intensity;
            seed = Random.value * 100f;
        }

        private void Update()
        {
            // 퍼린 노이즈를 시간에 따라 읽으면 무작위인데도 급격히 튀지 않는다
            float noise = Mathf.PerlinNoise(seed, Time.time * speed) * 2f - 1f;
            target.intensity = baseIntensity * (1f + noise * amplitude);
        }
    }
}
