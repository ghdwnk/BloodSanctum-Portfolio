using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 강화한 무기에 단계별 마법 이펙트(AC Little Enchant Mesh VFX)를 입힌다.
    ///  - 1~3강: Ice Lvl 1 / 4~6강: Fire Lvl 2 / 7~9강: Dark Lvl 3
    /// 이펙트 프리팹은 Resources/VFX/WeaponEnhance에 있다 (에셋 원본 프리팹 복사본).
    /// 프리팹의 파티클은 모양(Shape)이 "메시 렌더러"형이라, 지금 손에 든 무기 모델의 렌더러를 꽂아 주면
    /// 무기 표면에서 입자가 나오고 휘두를 때도 무기를 따라다닌다.
    ///
    /// 무기(Weapon) 오브젝트에 WeaponController가 처음 필요할 때 붙이고, 장비·강화가 바뀔 때마다 Refresh를 부른다.
    /// </summary>
    public class WeaponEnhanceGlow : MonoBehaviour
    {
        // 단계별 이펙트 프리팹 경로 (Resources 기준). 인덱스 0=1~3강, 1=4~6강, 2=7~9강
        private static readonly string[] TierPrefabPaths =
        {
            "VFX/WeaponEnhance/Enhance Tier1 Ice",
            "VFX/WeaponEnhance/Enhance Tier2 Fire",
            "VFX/WeaponEnhance/Enhance Tier3 Dark",
        };

        private GameObject currentModel;
        private int currentLevel;
        private int currentTier = -1;
        private GameObject effectInstance;
        // 한 번이라도 Refresh가 불렸는지. 같은 모델·같은 단계로 다시 불리면 아무것도 하지 않기 위해 쓴다
        private bool hasRefreshed;

        /// <summary>강화 수치(1~9)에 해당하는 이펙트 단계 번호(0~2). 강화하지 않았으면 -1.</summary>
        public static int GetTier(int level)
        {
            if (level <= 0)
            {
                return -1;
            }

            return Mathf.Min((level - 1) / 3, TierPrefabPaths.Length - 1);
        }

        /// <summary>현재 손에 든 모델과 강화 수치를 알려 주면 그에 맞는 이펙트를 켜거나 끈다.</summary>
        public void Refresh(GameObject model, int level)
        {
            bool modelChanged = model != currentModel;

            // 장비 갱신은 강화·스탯 변경 때마다 자주 불린다. 모델도 단계도 그대로면 이펙트를 다시 만들 필요가 없다
            if (hasRefreshed && !modelChanged && level == currentLevel)
            {
                return;
            }

            hasRefreshed = true;
            currentModel = model;
            currentLevel = level;

            int tier = GetTier(level);

            // 같은 모델에서 같은 단계 안(예: 1강→2강)이면 이펙트를 그대로 둔다
            if (!modelChanged && tier == currentTier && effectInstance != null)
            {
                return;
            }

            DestroyEffect();
            currentTier = tier;

            if (tier < 0 || model == null)
            {
                return;
            }

            BuildEffect(model, tier);
        }

        private void BuildEffect(GameObject model, int tier)
        {
            GameObject prefab = Resources.Load<GameObject>(TierPrefabPaths[tier]);
            if (prefab == null)
            {
                return;
            }

            Renderer meshSource = FindMainRenderer(model);
            if (meshSource == null)
            {
                return;
            }

            // 입자가 무기를 따라다니도록 무기 오브젝트 아래에 둔다
            effectInstance = Instantiate(prefab, transform);
            effectInstance.name = "Enhance Effect";
            effectInstance.transform.localPosition = Vector3.zero;
            effectInstance.transform.localRotation = Quaternion.identity;

            foreach (ParticleSystem particle in effectInstance.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                ParticleSystem.ShapeModule shape = particle.shape;
                if (!shape.enabled)
                {
                    continue;
                }

                // 무기 모델의 표면에서 입자가 나오게 렌더러를 꽂는다
                if (meshSource is SkinnedMeshRenderer skinned)
                {
                    shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
                    shape.skinnedMeshRenderer = skinned;
                }
                else
                {
                    shape.shapeType = ParticleSystemShapeType.MeshRenderer;
                    shape.meshRenderer = (MeshRenderer)meshSource;
                }
            }

            foreach (ParticleSystem particle in effectInstance.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Play(false);
            }
        }

        // 무기 모델에서 가장 큰 메시 렌더러(칼날)를 고른다. 입자·트레일 렌더러는 제외한다
        private static Renderer FindMainRenderer(GameObject model)
        {
            Renderer best = null;
            float bestVolume = -1f;

            foreach (Renderer rend in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!(rend is MeshRenderer) && !(rend is SkinnedMeshRenderer))
                {
                    continue;
                }

                Vector3 size = rend.bounds.size;
                float volume = size.x * size.y * size.z;
                if (volume > bestVolume)
                {
                    bestVolume = volume;
                    best = rend;
                }
            }

            return best;
        }

        private void DestroyEffect()
        {
            if (effectInstance != null)
            {
                Destroy(effectInstance);
                effectInstance = null;
            }
        }

        private void OnDestroy()
        {
            DestroyEffect();
        }
    }
}
