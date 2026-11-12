using System.Collections;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 맞은 몬스터를 잠깐 밝게 번쩍이게 한다.
    /// 머티리얼을 복제하지 않고 MaterialPropertyBlock만 쓰므로 몬스터가 많아도 메모리·드로우콜 부담이 없다.
    /// MonsterStateManager가 Awake에서 코드로 붙이고, 플레이어 타격(CombatFeedback.PlayHit)이 Flash를 부른다.
    /// </summary>
    public class MonsterHitFlash : MonoBehaviour
    {
        [Tooltip("번쩍이는 시간(초)")]
        [SerializeField] private float duration = 0.12f;
        [Tooltip("일반 타격 때 원래 색에 곱하는 값. 1보다 커야 밝아진다")]
        [SerializeField] private Color normalTint = new Color(3f, 3f, 3f, 1f);
        [Tooltip("치명타 때 곱하는 값 (붉은 기가 돈다)")]
        [SerializeField] private Color criticalTint = new Color(3.5f, 1.2f, 0.7f, 1f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private SkinnedMeshRenderer[] renderers;
        private Color[][] baseColors;
        private MaterialPropertyBlock block;
        private Coroutine routine;

        private void Awake()
        {
            // 몬스터 본체는 전부 스킨드 메시다. 이름표 화살표(Pointer) 같은 장식물은 번쩍일 필요가 없다
            renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            baseColors = new Color[renderers.Length][];
            block = new MaterialPropertyBlock();

            for (int ix = 0; ix < renderers.Length; ++ix)
            {
                Material[] materials = renderers[ix].sharedMaterials;
                baseColors[ix] = new Color[materials.Length];

                for (int jx = 0; jx < materials.Length; ++jx)
                {
                    Material material = materials[jx];
                    baseColors[ix][jx] = material != null && material.HasProperty(BaseColorId)
                        ? material.GetColor(BaseColorId)
                        : Color.white;
                }
            }
        }

        /// <summary>한 번 번쩍인다. 이미 번쩍이는 중이면 처음부터 다시 시작한다.</summary>
        public void Flash(bool isCritical)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }

            routine = StartCoroutine(FlashRoutine(isCritical ? criticalTint : normalTint));
        }

        private IEnumerator FlashRoutine(Color tint)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                // 처음에 가장 밝고 점점 원래 색으로 돌아온다
                float strength = 1f - elapsed / duration;
                Apply(tint, strength);

                yield return null;
                elapsed += Time.deltaTime;
            }

            Clear();
            routine = null;
        }

        private void Apply(Color tint, float strength)
        {
            for (int ix = 0; ix < renderers.Length; ++ix)
            {
                for (int jx = 0; jx < baseColors[ix].Length; ++jx)
                {
                    Color baseColor = baseColors[ix][jx];
                    block.SetColor(BaseColorId, Color.Lerp(baseColor, baseColor * tint, strength));
                    renderers[ix].SetPropertyBlock(block, jx);
                }
            }
        }

        private void Clear()
        {
            for (int ix = 0; ix < renderers.Length; ++ix)
            {
                for (int jx = 0; jx < baseColors[ix].Length; ++jx)
                {
                    renderers[ix].SetPropertyBlock(null, jx);
                }
            }
        }

        private void OnDisable()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
                Clear();
            }
        }
    }
}
