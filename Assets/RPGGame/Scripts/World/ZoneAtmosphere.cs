using UnityEngine;
using UnityEngine.Rendering;

namespace RPGGame
{
    /// <summary>
    /// 플레이어가 특정 지역에 있는 동안만 안개·환경광·태양빛·후처리를 바꿔서 그 지역만 다른 분위기로 보이게 한다.
    /// 씬의 조명 설정(RenderSettings)은 전 지역이 공유하는 하나뿐이라, 지역마다 따로 둘 수 없어서
    /// 시작 때 원래 값을 기억해 두고 지역에 들어오면 목표 값으로, 나가면 원래 값으로 천천히 섞는다.
    /// 이 컴포넌트를 끄거나 지우면 지역은 원래 분위기 그대로다 (되돌리기 쉽게 하려는 목적).
    /// </summary>
    public class ZoneAtmosphere : MonoBehaviour
    {
        [SerializeField] private ZoneArea zone;
        [Tooltip("지역에 들어오고 나갈 때 분위기가 바뀌는 데 걸리는 시간")]
        [SerializeField] private float transitionTime = 1.5f;

        [Header("안개")]
        [SerializeField] private Color fogColor = new Color(0.04f, 0.03f, 0.06f, 1f);
        [SerializeField] private float fogDensity = 0.02f;

        [Header("빛")]
        // 환경광 세기 (스카이박스 환경광에 곱해진다). 실내라 하늘빛이 들어오지 않게 낮춘다
        [SerializeField, Range(0f, 1f)] private float ambientIntensity = 0.12f;
        // 반사 세기. 금속·물이 하늘을 비추지 않게 낮춘다
        [SerializeField, Range(0f, 1f)] private float reflectionIntensity = 0.1f;
        // 태양빛 세기 배율. 실내이므로 0에 가깝게 둔다
        [SerializeField, Range(0f, 1f)] private float sunMultiplier = 0f;
        [Tooltip("지역 안에서 태양빛이 띠는 색 (달빛·마법광 같은 느낌을 낼 때)")]
        [SerializeField] private Color sunColor = Color.white;
        [Tooltip("비워 두면 씬의 첫 번째 방향광(RenderSettings.sun 또는 검색)을 쓴다")]
        [SerializeField] private Light sun;

        [Header("후처리")]
        [Tooltip("이 지역 전용 후처리. 가중치를 0~1로 섞는다 (비워 두면 후처리는 바꾸지 않는다)")]
        [SerializeField] private Volume volume;

        private Color baseFogColor;
        private float baseFogDensity;
        private float baseAmbient;
        private float baseReflection;
        private float baseSun;
        private Color baseSunColor;
        private bool baseFogEnabled;
        private float weight;
        private bool captured;

        private void OnEnable()
        {
            Capture();
        }

        private void OnDisable()
        {
            // 컴포넌트를 끄면 씬의 원래 분위기로 되돌린다
            if (captured)
            {
                Apply(0f);
            }
        }

        private void Capture()
        {
            if (captured)
            {
                return;
            }

            if (sun == null)
            {
                sun = RenderSettings.sun;
            }

            if (sun == null)
            {
                foreach (Light light in FindObjectsByType<Light>())
                {
                    if (light.type == LightType.Directional)
                    {
                        sun = light;
                        break;
                    }
                }
            }

            baseFogEnabled = RenderSettings.fog;
            baseFogColor = RenderSettings.fogColor;
            baseFogDensity = RenderSettings.fogDensity;
            baseAmbient = RenderSettings.ambientIntensity;
            baseReflection = RenderSettings.reflectionIntensity;
            baseSun = sun != null ? sun.intensity : 0f;
            baseSunColor = sun != null ? sun.color : Color.white;
            captured = true;
        }

        private void Update()
        {
            if (!captured || zone == null)
            {
                return;
            }

            // 지역 판정은 ZoneTracker가 이미 0.3초 간격으로 하고 있으므로 결과만 읽는다
            float target = ZoneTracker.CurrentZone == zone ? 1f : 0f;

            if (Mathf.Approximately(weight, target))
            {
                return;
            }

            weight = Mathf.MoveTowards(weight, target, Time.deltaTime / Mathf.Max(0.01f, transitionTime));
            Apply(weight);
        }

        private void Apply(float t)
        {
            t = Mathf.SmoothStep(0f, 1f, t);

            RenderSettings.fog = true;
            RenderSettings.fogColor = Color.Lerp(baseFogColor, fogColor, t);
            RenderSettings.fogDensity = Mathf.Lerp(baseFogDensity, fogDensity, t);
            RenderSettings.ambientIntensity = Mathf.Lerp(baseAmbient, ambientIntensity, t);
            RenderSettings.reflectionIntensity = Mathf.Lerp(baseReflection, reflectionIntensity, t);

            if (sun != null)
            {
                sun.intensity = Mathf.Lerp(baseSun, baseSun * sunMultiplier, t);
                sun.color = Color.Lerp(baseSunColor, sunColor, t);
            }

            if (volume != null)
            {
                volume.weight = t;
            }

            // 지역을 완전히 벗어났으면 안개 사용 여부도 원래대로 돌려 둔다
            if (t <= 0f)
            {
                RenderSettings.fog = baseFogEnabled;
            }
        }
    }
}
