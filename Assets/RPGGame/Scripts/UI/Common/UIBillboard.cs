using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 월드 공간 UI(몬스터 머리 위 체력바·이름표·데미지 숫자 등)를 항상 카메라 정면으로 향하게 하는 컴포넌트.
    /// 카메라가 움직인 뒤에 맞춰야 한 프레임 늦게 따라가며 떨리지 않으므로 LateUpdate에서 처리한다.
    /// </summary>
    public class UIBillboard : MonoBehaviour
    {
        private Transform refTransform;
        private Camera mainCamera;

        private void Awake()
        {
            refTransform = transform;
            mainCamera = Camera.main;
        }

        private void LateUpdate()
        {
            // 카메라가 아직 없거나 사라졌으면(씬 전환 중) 다시 찾아 본다
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    return;
                }
            }

            // 카메라가 보는 방향을 그대로 따라 해서 글자가 뒤집히거나 기울지 않게 한다
            refTransform.forward = mainCamera.transform.forward;
        }
    }
}
