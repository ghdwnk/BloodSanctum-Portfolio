using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 플레이어에게 걸리는 이동속도 감소(둔화) 상태 이상. 걸려 있는 동안 몸이 푸르게 물들어 보인다.
    /// 다시 맞으면 겹쳐서 더 느려지지 않고 지속 시간만 새로 채워진다 (BurnEffect와 같은 방식).
    /// 이동 속도는 PlayerMoveState가 MoveSpeedMultiplier를 곱해서 쓴다.
    /// 색은 머티리얼을 복제하지 않고 MaterialPropertyBlock으로만 바꾸므로 끝나면 원래 모습 그대로 돌아온다
    /// </summary>
    public class PlayerSlowEffect : MonoBehaviour
    {
        // 색이 원래대로 돌아오는 데 걸리는 마지막 구간 (초). 끝나는 순간 색이 툭 바뀌지 않게 한다
        private const float FadeOutTime = 0.6f;

        // 둔화가 걸려 있는 동안 닉네임 위에 계속 띄우는 문구
        private const string StatusKey = "slow";
        private const string SlowText = "느려짐!";
        private static readonly Color SlowTextColor = new Color(0.35f, 0.7f, 1f, 1f);

        private static readonly List<PlayerSlowEffect> active = new List<PlayerSlowEffect>();
        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorID = Shader.PropertyToID("_Color");

        // 머티리얼 하나당 원래 색과 색을 덮어쓸 프로퍼티 블록
        private class Slot
        {
            public Renderer renderer;
            public int materialIndex;
            public int colorID;
            public Color originalColor;
            public MaterialPropertyBlock block;
        }

        private readonly List<Slot> slots = new List<Slot>();
        private HPController hp;
        private UIPlayerNameTag nameTag;
        private PlayerAnimationController animationController;
        // 남은 지속 시간(초). 0 이하면 걸려 있지 않은 것이다
        private float remaining;
        private Color tint = Color.white;
        private bool isTinted;

        /// <summary>현재 이동속도 배율. 걸려 있지 않으면 1.</summary>
        public float MoveSpeedMultiplier { get; private set; } = 1f;

        /// <summary>
        /// 대상 플레이어에게 둔화를 건다. multiplier는 이동속도 배율(0.5면 절반), tint는 몸에 곱해 줄 색이다.
        /// 이미 걸려 있으면 더 강한 쪽 배율을 쓰고 지속 시간을 새 값으로 갱신한다
        /// </summary>
        public static void Apply(Damageable target, float multiplier, float duration, Color tint)
        {
            if (target == null || duration <= 0f)
            {
                return;
            }

            // 태그가 아니라 컴포넌트로 플레이어를 찾는다 (Damageable은 모델 자식에 붙어 있다)
            PlayerStateManager player = target.GetComponentInParent<PlayerStateManager>();
            if (player == null)
            {
                return;
            }

            PlayerSlowEffect effect = player.GetComponent<PlayerSlowEffect>();
            if (effect == null)
            {
                effect = player.gameObject.AddComponent<PlayerSlowEffect>();
            }

            effect.Begin(multiplier, duration, tint);
        }

        /// <summary>모든 둔화를 푼다 (보스 사망·전투 리셋 때).</summary>
        public static void ClearAll()
        {
            for (int ix = active.Count - 1; ix >= 0; --ix)
            {
                if (active[ix] != null)
                {
                    active[ix].End();
                }
            }

            active.Clear();
        }

        private void Begin(float multiplier, float duration, Color tint)
        {
            if (hp == null)
            {
                hp = GetComponentInChildren<HPController>();

                // 죽으면 둔화도 풀어야 한다. 그대로 두면 마을에서 부활한 뒤에도 파랗고 느린 채로 남는다
                if (hp != null)
                {
                    hp.SubscribeOnDead(End);
                }
            }

            bool wasActive = remaining > 0f;
            MoveSpeedMultiplier = wasActive ? Mathf.Min(MoveSpeedMultiplier, multiplier) : multiplier;
            remaining = duration;
            this.tint = tint;

            if (!wasActive)
            {
                active.Add(this);
            }

            if (slots.Count == 0)
            {
                CacheRenderers();
            }

            ApplyTint(1f);

            // 달리는 애니메이션도 이동속도 배율만큼 느리게 한다
            RefreshAnimationSpeed();

            // 닉네임 위의 문구는 둔화가 끝날 때(End) 치운다. 다시 맞아 갱신돼도 이미 떠 있으므로 그대로 둔다
            if (nameTag == null)
            {
                nameTag = GetComponentInChildren<UIPlayerNameTag>(true);
            }

            if (nameTag != null)
            {
                nameTag.SetStatus(StatusKey, SlowText, SlowTextColor);
            }
        }

        // 플레이어 몸의 렌더러를 처음 걸릴 때 한 번만 모아 두고, 머티리얼별 원래 색을 기억한다
        private void CacheRenderers()
        {
            foreach (Renderer rend in GetComponentsInChildren<Renderer>(true))
            {
                if (!(rend is SkinnedMeshRenderer) && !(rend is MeshRenderer))
                {
                    continue;
                }

                Material[] materials = rend.sharedMaterials;
                for (int ix = 0; ix < materials.Length; ++ix)
                {
                    Material material = materials[ix];
                    if (material == null)
                    {
                        continue;
                    }

                    int colorID = material.HasProperty(BaseColorID) ? BaseColorID : material.HasProperty(ColorID) ? ColorID : 0;
                    if (colorID == 0)
                    {
                        continue;
                    }

                    slots.Add(new Slot
                    {
                        renderer = rend,
                        materialIndex = ix,
                        colorID = colorID,
                        originalColor = material.GetColor(colorID),
                        block = new MaterialPropertyBlock()
                    });
                }
            }
        }

        // strength 1이면 tint 색 그대로, 0이면 원래 색. 원래 색에 곱해서 칠한다
        private void ApplyTint(float strength)
        {
            Color multiplier = Color.Lerp(Color.white, tint, strength);

            foreach (Slot slot in slots)
            {
                if (slot.renderer == null)
                {
                    continue;
                }

                slot.renderer.GetPropertyBlock(slot.block, slot.materialIndex);
                slot.block.SetColor(slot.colorID, slot.originalColor * multiplier);
                slot.renderer.SetPropertyBlock(slot.block, slot.materialIndex);
            }

            isTinted = true;
        }

        private void RestoreColors()
        {
            if (!isTinted)
            {
                return;
            }

            foreach (Slot slot in slots)
            {
                if (slot.renderer != null)
                {
                    // 이 렌더러에 다른 색 덮어쓰기가 없으므로 블록을 비워 원래 머티리얼 색으로 되돌린다
                    slot.block.Clear();
                    slot.renderer.SetPropertyBlock(slot.block, slot.materialIndex);
                }
            }

            isTinted = false;
        }

        private void Update()
        {
            if (remaining <= 0f)
            {
                return;
            }

            remaining -= Time.deltaTime;

            if (remaining <= 0f)
            {
                End();
                return;
            }

            // 마지막 FadeOutTime 동안 색이 서서히 돌아온다
            if (remaining < FadeOutTime)
            {
                ApplyTint(Mathf.Clamp01(remaining / FadeOutTime));
            }
        }

        // 애니메이션 재생 속도는 상태가 바뀔 때만 정해지므로, 둔화가 걸리거나 풀리는 순간에 다시 계산시킨다
        private void RefreshAnimationSpeed()
        {
            if (animationController == null)
            {
                animationController = GetComponentInChildren<PlayerAnimationController>(true);
            }

            if (animationController != null)
            {
                animationController.ApplyAnimationSpeed();
            }
        }

        // 둔화를 끝내고 속도와 색을 원래대로 돌린다
        private void End()
        {
            remaining = 0f;
            MoveSpeedMultiplier = 1f;
            RestoreColors();
            RefreshAnimationSpeed();
            if (nameTag != null)
            {
                nameTag.ClearStatus(StatusKey);
            }
            active.Remove(this);
        }

        private void OnDestroy()
        {
            active.Remove(this);
        }
    }
}
