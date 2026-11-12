using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// UISound가 재생하는 UI 효과음 5개를 에셋 하나로 묶어 둔 데이터.
    /// 클립을 Resources 폴더에 둘 필요 없이 Audio/Sfx에 그대로 두고 여기서 직접 참조한다.
    /// 클립이 에셋에 직렬화되어 참조되므로 Resources.Load 시 함께 로드된다 (Preload Audio Data 설정도 그대로 적용)
    /// </summary>
    [CreateAssetMenu(fileName = "UI Sound Set", menuName = "RPGGame/UI Sound Set")]
    public class UISoundSet : ScriptableObject
    {
        public AudioClip toggle;
        public AudioClip click;
        public AudioClip levelUp;
        public AudioClip questClear;
        public AudioClip potion;
        [Tooltip("퀘스트 제안을 받을 수 있는(물음표) NPC와 대화를 시작할 때, 그리고 수락 버튼을 눌렀을 때")]
        public AudioClip questStart;
        public AudioClip questAccept;
    }
}
