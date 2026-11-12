namespace RPGGame
{
    /// <summary>
    /// F키로 상호작용할 수 있는 오브젝트가 공통으로 지키는 약속(인터페이스).
    ///
    /// 구현하는 쪽: NPCStateManager(대화·상점·강화), TeleportPortal(텔레포트), SealingThrone(엔딩).
    /// 각 오브젝트가 자기 트리거 범위 안에 플레이어가 있는지 스스로 판단하고,
    /// 범위 안이면 InteractionPrompt를 머리 위 안내(InteractionPromptView)로 띄운 뒤 F키 입력 때 Interact()를 부른다.
    /// 네 곳이 같은 이름·같은 의미로 쓰도록 맞춰 두는 용도라, 이 인터페이스로 묶어 부르는 곳은 따로 없다.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>지금 상호작용할 수 있는 상태인지. false면 안내 문구도 띄우지 않는다 (예: 이미 획득한 채집물).</summary>
        bool CanInteract { get; }

        /// <summary>머리 위 안내에 표시할 문구 (예: "F : 대화하기").</summary>
        string InteractionPrompt { get; }

        /// <summary>F키를 눌렀을 때 실행할 동작.</summary>
        void Interact();
    }
}
