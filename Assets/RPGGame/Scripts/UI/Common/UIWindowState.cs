namespace RPGGame
{
    /// <summary>
    /// 마우스 조작이 필요한 UI 창이 열려 있는지를 한곳에서 판단한다.
    /// 카메라 회전 차단과 마우스 커서 표시가 같은 기준을 쓰도록 모아 둔 것으로,
    /// 창이 새로 생기면 이 조건 하나만 고치면 된다.
    /// </summary>
    public static class UIWindowState
    {
        public static bool IsAnyWindowOpen =>
            UIInventoryWindow.IsOn ||
            UIQuestWindow.IsOn ||
            UISkillWindow.IsOn ||
            UICharacterStatusWindow.IsOn ||
            UIOptionsPanel.IsOn ||
            UIShopWindow.IsOn ||
            UIQuantityDialog.IsOn ||
            UIEnhanceWindow.IsOn ||
            UITeleportWindow.IsOn ||
            UISystemMessage.IsOn ||
            UIEndingScreen.IsOn ||
            // 대화창은 수락/거절 버튼이 떠 있을 때만 포함한다.
            // 짧은 안내 문구까지 포함하면 전투 중에 커서가 깜빡인다
            Dialogue.IsChoiceOn;
    }
}
