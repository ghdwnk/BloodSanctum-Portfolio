using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 보스 스테이트의 공통 베이스. Grenadier 구조와 동일하게 컴포넌트 하나가 스테이트 하나를 담당한다
    /// </summary>
    public class BossStateBase : MonoBehaviour
    {
        protected BossStateManager manager;
        protected Transform refTransform;
        protected CharacterController characterController;

        protected virtual void OnEnable()
        {
            if (manager == null)
            {
                manager = GetComponent<BossStateManager>();
            }

            if (refTransform == null)
            {
                refTransform = transform;
            }

            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }
        }

        protected virtual void Update()
        {
            // 공중에 뜨지 않도록 항상 중력을 적용한다
            if (characterController != null && characterController.enabled)
            {
                characterController.Move(Physics.gravity * Time.deltaTime);
            }
        }

        protected virtual void OnDisable()
        {
        }
    }
}
