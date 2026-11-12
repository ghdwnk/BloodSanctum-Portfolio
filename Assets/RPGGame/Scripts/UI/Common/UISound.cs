using UnityEngine;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// UI와 알림 성격의 효과음(창 열고 닫기, 버튼 클릭, 레벨업, 퀘스트 완료, 물약)을 한 곳에서 재생하는 정적 도우미.
    /// 창마다 AudioSource를 배선하지 않아도 되도록 DontDestroyOnLoad 오브젝트의 채널 하나를 공용으로 쓴다.
    /// 효과음 볼륨/음소거는 GameSettings가 AudioListener.volume으로 처리하므로 여기서는 다루지 않는다
    /// </summary>
    public static class UISound
    {
        private enum Cue
        {
            Toggle,
            Click,
            LevelUp,
            QuestClear,
            Potion,
            QuestStart,
            QuestAccept
        }

        // 소리를 바꾸려면 이 에셋(Resources/Data/UI Sound Set)의 클립 슬롯만 교체하면 된다
        private const string SetPath = "Data/UI Sound Set";

        // 파일마다 원래 크기가 달라서, 비슷하게 들리도록 소리별로 음량을 다르게 둔다.
        // 창 열고 닫기는 원본이 커서 BGM 위에서 거슬리지 않게 크게 낮췄다 (0.3 → 0.12)
        private static readonly float[] Volumes =
        {
            0.12f,
            0.95f,
            0.65f,
            0.28f,
            0.75f,
            0.6f,
            0.6f
        };

        private static AudioSource source;
        private static readonly AudioClip[] clips = new AudioClip[Volumes.Length];
        private static UISoundSet soundSet;

        // 창 열고 닫는 소리
        public static void Play() => PlayCue(Cue.Toggle);

        public static void PlayClick() => PlayCue(Cue.Click);

        public static void PlayLevelUp() => PlayCue(Cue.LevelUp);

        // 퀘스트 완료음. 강화 성공음으로도 쓰이므로, 같은 프레임에 여러 곳이 불러도 한 번만 낸다
        public static void PlayQuestClear()
        {
            if (lastQuestClearFrame == Time.frameCount)
            {
                return;
            }

            lastQuestClearFrame = Time.frameCount;
            PlayCue(Cue.QuestClear);
        }

        private static int lastQuestClearFrame = -1;

        public static void PlayPotion() => PlayCue(Cue.Potion);

        // 퀘스트를 줄 수 있는(물음표) NPC와 대화를 시작할 때
        public static void PlayQuestStart() => PlayCue(Cue.QuestStart);

        // 퀘스트 수락 버튼을 눌렀을 때
        public static void PlayQuestAccept() => PlayCue(Cue.QuestAccept);

        private static void PlayCue(Cue cue)
        {
            int index = (int)cue;

            if (clips[index] == null)
            {
                clips[index] = GetClip(cue);
                if (clips[index] == null)
                {
                    return;
                }
            }

            if (source == null)
            {
                GameObject host = new GameObject("[UISound]");
                Object.DontDestroyOnLoad(host);
                source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }

            source.PlayOneShot(clips[index], Volumes[index]);
        }

        // 세트 에셋은 처음 한 번만 불러 두고, 이후에는 필드에서 바로 꺼낸다
        private static AudioClip GetClip(Cue cue)
        {
            if (soundSet == null)
            {
                soundSet = Resources.Load<UISoundSet>(SetPath);
                if (soundSet == null)
                {
                    Debug.LogWarning($"UISound: '{SetPath}' 에셋을 찾지 못해 UI 효과음을 재생하지 못한다.");
                    return null;
                }
            }

            switch (cue)
            {
                case Cue.Toggle: return soundSet.toggle;
                case Cue.Click: return soundSet.click;
                case Cue.LevelUp: return soundSet.levelUp;
                case Cue.QuestClear: return soundSet.questClear;
                case Cue.QuestStart: return soundSet.questStart;
                case Cue.QuestAccept: return soundSet.questAccept;
                default: return soundSet.potion;
            }
        }

        // 창을 켜고 끄면서, 실제로 상태가 바뀔 때만 소리를 낸다.
        // 이미 닫힌 창에 Close가 또 불리거나(상점 닫기 → 수량 창 닫기 등) 이미 열린 창에 Show가 다시 불려도 소리가 겹치지 않는다
        public static void SetActive(GameObject window, bool active)
        {
            if (window.activeSelf != active)
            {
                Play();
            }

            window.SetActive(active);
        }

        // root 아래의 모든 버튼(꺼져 있는 것 포함)이 눌릴 때 클릭음이 나게 한다.
        // 버튼마다 한 번만 붙이므로, 같은 버튼이 여러 범위에 겹쳐도(로비 전체 + 환경설정 패널) 소리가 두 번 나지 않는다
        public static void AttachClick(Transform root)
        {
            if (root == null)
            {
                return;
            }

            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.GetComponent<UIButtonClickSound>() == null)
                {
                    button.gameObject.AddComponent<UIButtonClickSound>();
                }
            }
        }
    }
}
