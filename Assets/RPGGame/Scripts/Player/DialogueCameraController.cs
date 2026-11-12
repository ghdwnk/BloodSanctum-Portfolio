using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// NPC·보스와 대화하는 동안 카메라를 대상 얼굴 쪽으로 당겼다가, 대화가 끝나면 원래 자리로 되돌린다.
    ///
    /// NPCTalkState(대화 시작/끝)와 보스 조우 대사가 Focus / Release를 부른다.
    /// 줌인하는 동안에는 CameraRig를 잠시 꺼서 서로 카메라를 밀어내지 않게 하고, 카메라 월드 좌표를 직접 보간한다.
    /// 돌아올 때는 리그가 만들던 원래 로컬 배치로 보간한 뒤 리그를 다시 켠다.
    /// </summary>
    public class DialogueCameraController : MonoBehaviour
    {
        private static DialogueCameraController instance;

        [Tooltip("줌인하는 동안 끌 카메라 리그. 비워 두면 찾는다")]
        [SerializeField] private CameraRig cameraRig;

        [Header("연출 (대상 기준, NPC 크기 1일 때)")]
        [Tooltip("대상 정면으로부터의 거리(m)")]
        [SerializeField] private float distance = 2.6f;
        [Tooltip("카메라 높이(m)")]
        [SerializeField] private float height = 1.75f;
        [Tooltip("옆으로 비켜서는 거리(m). 정면에서 살짝 비껴야 대화하는 그림이 나온다")]
        [SerializeField] private float sideOffset = 0.9f;
        [Tooltip("카메라가 바라볼 높이(m, 얼굴 근처)")]
        [SerializeField] private float lookHeight = 1.5f;
        [Tooltip("줌인 빠르기")]
        [SerializeField] private float moveSpeed = 4.5f;
        [Tooltip("원래 자리로 돌아오는 빠르기")]
        [SerializeField] private float returnSpeed = 5.5f;
        [Tooltip("줌인했을 때의 시야각. 기본값보다 좁히면 인물이 커 보인다")]
        [SerializeField] private float zoomFieldOfView = 40f;

        private Camera targetCamera;
        private Transform cameraTransform;
        private Transform focusTarget;
        // 리그가 만들어내던 원래 로컬 배치. 되돌릴 때 목표로 삼는다
        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private float originalFieldOfView;
        private bool isFocusing;
        private bool isReturning;
        // 이번 줌인의 거리/높이 배율
        private float focusScale = 1f;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(this);
                return;
            }

            targetCamera = Camera.main;
            if (targetCamera != null)
            {
                cameraTransform = targetCamera.transform;
                originalLocalPosition = cameraTransform.localPosition;
                originalLocalRotation = cameraTransform.localRotation;
                originalFieldOfView = targetCamera.fieldOfView;
            }

            if (cameraRig == null)
            {
                cameraRig = FindAnyObjectByType<CameraRig>();
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        /// <summary>NPC 크기 기준으로 대상에게 줌인한다.</summary>
        public static void Focus(Transform npc)
        {
            Focus(npc, 1f);
        }

        /// <summary>대상에게 줌인한다. scale은 덩치 배율(NPC = 1). 보스처럼 큰 대상은 거리·높이를 그만큼 벌려야 얼굴이 잡힌다.</summary>
        public static void Focus(Transform target, float scale)
        {
            if (instance != null)
            {
                instance.focusScale = Mathf.Max(0.1f, scale);
                instance.BeginFocus(target);
            }
        }

        /// <summary>줌인을 풀고 원래 자리로 되돌아가기 시작한다.</summary>
        public static void Release()
        {
            if (instance != null)
            {
                instance.EndFocus();
            }
        }

        private void BeginFocus(Transform npc)
        {
            if (cameraTransform == null || npc == null)
            {
                return;
            }

            focusTarget = npc;
            isFocusing = true;
            isReturning = false;

            // 리그가 계속 따라다니면 카메라가 서로 밀어내므로 잠시 멈춘다
            if (cameraRig != null)
            {
                cameraRig.enabled = false;
            }
        }

        private void EndFocus()
        {
            focusTarget = null;

            if (isFocusing)
            {
                isFocusing = false;
                isReturning = true;
                return;
            }

            // 줌인 중이 아닌데 Release가 들어온 경우(안전장치).
            // 여기서 그냥 빠져나가면 리그가 꺼진 채로 남아 카메라가 플레이어를 따라가지 않는다
            RestoreRig();
        }

        // 리그에 조종을 돌려주고 카메라를 원래 배치로 되돌린다
        private void RestoreRig()
        {
            isFocusing = false;
            isReturning = false;

            if (cameraTransform != null)
            {
                cameraTransform.localPosition = originalLocalPosition;
                cameraTransform.localRotation = originalLocalRotation;
            }

            if (targetCamera != null)
            {
                targetCamera.fieldOfView = originalFieldOfView;
            }

            if (cameraRig != null)
            {
                cameraRig.enabled = true;
            }
        }

        // 이 컴포넌트가 꺼지거나 사라질 때 리그가 꺼진 채 남지 않도록 한다
        private void OnDisable()
        {
            RestoreRig();
        }

        private void LateUpdate()
        {
            if (cameraTransform == null)
            {
                return;
            }

            if (isFocusing && focusTarget != null)
            {
                MoveToFocus();
            }
            else if (isReturning)
            {
                MoveBack();
            }
        }

        private void MoveToFocus()
        {
            // NPC 정면에서 옆으로 비켜선 자리
            Vector3 lookPoint = focusTarget.position + Vector3.up * (lookHeight * focusScale);
            Vector3 target = focusTarget.position
                             + focusTarget.forward * (distance * focusScale)
                             + focusTarget.right * (sideOffset * focusScale)
                             + Vector3.up * (height * focusScale);

            cameraTransform.position = Vector3.Lerp(cameraTransform.position, target, moveSpeed * Time.deltaTime);
            cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation,
                Quaternion.LookRotation(lookPoint - cameraTransform.position), moveSpeed * Time.deltaTime);

            if (targetCamera != null)
            {
                targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, zoomFieldOfView, moveSpeed * Time.deltaTime);
            }
        }

        private void MoveBack()
        {
            // 리그가 있었다면 만들었을 배치로 되돌린다
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, originalLocalPosition,
                returnSpeed * Time.deltaTime);
            cameraTransform.localRotation = Quaternion.Slerp(cameraTransform.localRotation, originalLocalRotation,
                returnSpeed * Time.deltaTime);

            if (targetCamera != null)
            {
                targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, originalFieldOfView,
                    returnSpeed * Time.deltaTime);
            }

            // 충분히 가까워지면(2cm) 정확히 맞추고 리그에 조종을 돌려준다
            if ((cameraTransform.localPosition - originalLocalPosition).sqrMagnitude < 0.02f * 0.02f)
            {
                RestoreRig();
            }
        }
    }
}
