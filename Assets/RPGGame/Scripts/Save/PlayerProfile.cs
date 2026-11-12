using System;

namespace RPGGame
{
    /// <summary>
    /// 플레이어가 로비에서 정한 닉네임을 들고 있는 정적 저장소.
    /// 로비 씬에서 게임 씬으로 넘어가도 유지돼야 하므로 정적으로 둔다.
    /// 이어하기를 하면 세이브 파일의 닉네임이 GameSaveLoader를 통해 다시 들어온다.
    /// </summary>
    public static class PlayerProfile
    {
        /// <summary>
        /// 닉네임이 없을 때(예전 세이브, 게임 씬을 바로 실행한 경우) 쓰는 이름.
        /// 대사의 [봉인자] 화자 태그와 같은 글자라, 이 화자는 화면에 닉네임으로 바뀌어 보인다.
        /// </summary>
        public const string DefaultName = "봉인자";
        public const int MinLength = 2;
        public const int MaxLength = 8;

        public static string Nickname { get; private set; } = DefaultName;

        /// <summary>닉네임이 바뀌면 알린다. 머리 위 이름표처럼 곧바로 따라가야 하는 쪽이 구독한다.</summary>
        public static event Action<string> NicknameChanged;

        /// <summary>닉네임을 바꾼다. 비어 있으면 기본 이름을 쓰고, 앞뒤 공백은 지운다.</summary>
        public static void SetNickname(string value)
        {
            string next = string.IsNullOrWhiteSpace(value) ? DefaultName : value.Trim();

            if (next == Nickname)
            {
                return;
            }

            Nickname = next;
            NicknameChanged?.Invoke(Nickname);
        }

        /// <summary>
        /// 닉네임으로 쓸 수 있는지 길이와 글자 종류를 검사한다.
        /// 공백·기호를 막는 이유: 이름이 대사의 [화자] 태그나 리치텍스트 태그와 섞여 대사가 깨지기 때문이다.
        /// </summary>
        /// <param name="reason">쓸 수 없을 때 사용자에게 보여 줄 이유</param>
        public static bool Validate(string value, out string reason)
        {
            string trimmed = value == null ? string.Empty : value.Trim();

            if (trimmed.Length < MinLength || trimmed.Length > MaxLength)
            {
                reason = $"{MinLength}~{MaxLength}글자로 입력해 주세요.";
                return false;
            }

            foreach (char c in trimmed)
            {
                if (!char.IsLetterOrDigit(c))
                {
                    reason = "한글, 영문, 숫자만 사용할 수 있어요.";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>대사 화자 이름을 화면에 보여 줄 이름으로 바꾼다. 플레이어 화자([봉인자])만 닉네임이 된다.</summary>
        public static string ToDisplayName(string speaker)
        {
            return speaker == DefaultName ? Nickname : speaker;
        }
    }
}
