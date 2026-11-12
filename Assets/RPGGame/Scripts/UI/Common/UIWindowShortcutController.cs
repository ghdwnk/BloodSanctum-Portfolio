using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// I(인벤토리) / J(퀘스트) / K(스킬) / U(캐릭터 상태창) 단축키로 각 UI 창을 여닫는다.
    /// </summary>
    public class UIWindowShortcutController : MonoBehaviour
    {
        private void Update()
        {
            if (InputManager.IsInventory)
            {
                ToggleInventory();
            }

            if (InputManager.IsQuest)
            {
                ToggleQuest();
            }

            if (InputManager.IsSkill)
            {
                ToggleSkill();
            }

            if (InputManager.IsCharacterStat)
            {
                ToggleCharacterStatus();
            }
        }

        private void ToggleInventory()
        {
            if (UIInventoryWindow.IsOn)
            {
                UIInventoryWindow.Close();
            }
            else
            {
                UIInventoryWindow.Show();
            }
        }

        private void ToggleQuest()
        {
            if (UIQuestWindow.IsOn)
            {
                UIQuestWindow.Close();
            }
            else
            {
                UIQuestWindow.Show();
            }
        }

        private void ToggleSkill()
        {
            if (UISkillWindow.IsOn)
            {
                UISkillWindow.Close();
            }
            else
            {
                UISkillWindow.Show();
            }
        }

        private void ToggleCharacterStatus()
        {
            if (UICharacterStatusWindow.IsOn)
            {
                UICharacterStatusWindow.Close();
            }
            else
            {
                UICharacterStatusWindow.Show();
            }
        }
    }
}
