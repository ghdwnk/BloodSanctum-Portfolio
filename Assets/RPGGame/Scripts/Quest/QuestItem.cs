using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// "이 몬스터를 잡으면 어떤 퀘스트 목표가 1 올라가는지"를 정해 두는 컴포넌트.
    ///
    /// 몬스터 프리팹(Vampire·Nightshade·Skeletonzombie·DrekMalos·Zyrathon)에 붙어 있고,
    /// HPController.OnDead 이벤트에 인스펙터로 OnCompleted가 연결돼 있다.
    /// 몬스터가 죽으면 쓰러진 위치와 함께 QuestManager.ProcessQuest로 알린다 (지역 지정 퀘스트 판정에 위치가 필요하다).
    /// </summary>
    public class QuestItem : MonoBehaviour
    {
        [Tooltip("이 대상이 올리는 퀘스트 종류 (몬스터는 Kill)")]
        [SerializeField] private QuestData.Type type = QuestData.Type.None;
        [Tooltip("이 대상의 종류. 몬스터 이름과 같다")]
        [SerializeField] private QuestData.TargetType targetType = QuestData.TargetType.None;

        /// <summary>
        /// 목표를 달성했을 때(몬스터가 죽었을 때) 부른다.
        /// HPController.OnDead에 인스펙터로 연결돼 있으므로 이름과 매개변수를 바꾸지 않는다.
        /// </summary>
        public void OnCompleted()
        {
            if (type == QuestData.Type.None || targetType == QuestData.TargetType.None || QuestManager.Instance == null)
            {
                return;
            }

            QuestManager.Instance.ProcessQuest(type, targetType, transform.position);
        }
    }
}
