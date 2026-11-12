using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 상호작용 범위에 들어온 NPC에 노란 외곽선을 입히는 컴포넌트.
    ///
    /// NPC 프리팹에 붙어 있고, NPCStateManager가 플레이어가 범위에 들어오고 나갈 때 SetHighlight(true/false)를 부른다.
    /// 외곽선 셰이더를 따로 돌리지 않고, 각 렌더러의 머티리얼 목록 끝에 외곽선 머티리얼을 하나 덧붙였다 빼는 방식이다.
    /// (Player/WeaponEnhanceGlow는 강화 무기에 단계별 마법 이펙트 프리팹을 입힌다)
    /// </summary>
    public class InteractionHighlight : MonoBehaviour
    {
        [Tooltip("덧그릴 외곽선 머티리얼")]
        [SerializeField] private Material outlineMaterial;

        private Renderer[] targetRenderers;
        // 렌더러별 원래 머티리얼 목록과, 외곽선을 덧붙인 목록.
        // 켜고 끌 때마다 배열을 새로 만들면 GC가 생기므로 Awake에서 두 벌을 미리 만들어 두고 바꿔 끼우기만 한다
        private Material[][] originalMaterials;
        private Material[][] highlightedMaterials;
        private bool isOn;

        private void Awake()
        {
            targetRenderers = GetComponentsInChildren<Renderer>(true);

            originalMaterials = new Material[targetRenderers.Length][];
            highlightedMaterials = new Material[targetRenderers.Length][];

            for (int ix = 0; ix < targetRenderers.Length; ++ix)
            {
                Material[] shared = targetRenderers[ix].sharedMaterials;
                originalMaterials[ix] = shared;

                // 서브메시 수보다 머티리얼이 많으면 유니티가 마지막 서브메시를 남는 머티리얼로 한 번 더 그린다.
                // 이 성질을 이용해 같은 메시 위에 외곽선 패스를 덧그린다
                Material[] highlighted = new Material[shared.Length + 1];
                System.Array.Copy(shared, highlighted, shared.Length);
                highlighted[shared.Length] = outlineMaterial;
                highlightedMaterials[ix] = highlighted;
            }
        }

        /// <summary>외곽선을 켜거나 끈다. 이미 그 상태면 아무것도 하지 않는다.</summary>
        public void SetHighlight(bool on)
        {
            if (isOn == on || outlineMaterial == null || targetRenderers == null)
            {
                return;
            }

            isOn = on;

            for (int ix = 0; ix < targetRenderers.Length; ++ix)
            {
                if (targetRenderers[ix] == null)
                {
                    continue;
                }

                // materials가 아닌 sharedMaterials로 넣어야 렌더러마다 머티리얼 사본이 생기지 않는다
                targetRenderers[ix].sharedMaterials = on ? highlightedMaterials[ix] : originalMaterials[ix];
            }
        }

        private void OnDisable()
        {
            // 외곽선이 켜진 채로 꺼졌다가 다시 켜지면 외곽선이 남아 있으므로 꺼질 때 되돌린다
            SetHighlight(false);
        }
    }
}
