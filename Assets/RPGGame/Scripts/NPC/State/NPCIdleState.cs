namespace RPGGame
{
    /// <summary>
    /// NPC 대기 상태. 플레이어가 대화 거리 안에 들어오면 외곽선과 "F : 대화하기" 안내를 켜고,
    /// F키를 누르면 NPCStateManager.Interact로 대화(또는 상점·강화 창)를 시작한다.
    /// 거리 안에 들어왔다고 저절로 말을 걸지는 않는다.
    /// </summary>
    public class NPCIdleState : NPCStateBase
    {
        protected override void Update()
        {
            base.Update();

            bool inRange = CanTalk();

            // 외곽선과 안내는 같은 조건이라 함께 처리한다.
            // SetHighlight는 상태가 바뀔 때만 실제로 머티리얼을 바꾸므로 매 프레임 불러도 된다
            manager.SetHighlight(inRange);

            if (!inRange)
            {
                manager.HidePrompt();
                return;
            }

            manager.ShowPrompt();

            if (InputManager.IsInteract)
            {
                manager.Interact();
            }
        }
    }
}
