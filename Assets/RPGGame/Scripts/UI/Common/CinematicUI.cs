using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 컷씬 연출 동안 화면의 모든 UI를 숨기고 나중에 되돌리는 정적 도우미.
    /// HUD뿐 아니라 NPC 이름표·상호작용 안내·체력바 같은 월드 스페이스 캔버스까지 모든 루트 캔버스를 끈다.
    /// 오브젝트를 비활성화하지 않고 Canvas 컴포넌트만 끄기 때문에 각 UI 스크립트의 상태와 구독이 그대로 유지된다.
    /// </summary>
    public static class CinematicUI
    {
        // 우리가 끈 캔버스. 원래부터 꺼져 있던 캔버스는 건드리지 않으므로 되돌릴 때 켜지는 일이 없다
        private static readonly List<Canvas> hidden = new();

        public static bool IsHidden => hidden.Count > 0;

        /// <summary>
        /// 켜져 있는 모든 루트 캔버스를 끈다. except에 넘긴 캔버스(연출용 검은 화면 등)는 그대로 둔다
        /// </summary>
        public static void HideAll(params Canvas[] except)
        {
            RestoreAll();

            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);

            foreach (Canvas canvas in canvases)
            {
                if (!canvas.isRootCanvas || !canvas.enabled || System.Array.IndexOf(except, canvas) >= 0)
                {
                    continue;
                }

                canvas.enabled = false;
                hidden.Add(canvas);
            }
        }

        /// <summary>
        /// 숨겨 둔 캔버스 하나만 다시 켠다 (결과 화면이 들어 있는 캔버스처럼 일부만 먼저 보여 줘야 할 때)
        /// </summary>
        public static void Show(Canvas canvas)
        {
            if (canvas == null)
            {
                return;
            }

            canvas.enabled = true;
            hidden.Remove(canvas);
        }

        /// <summary>
        /// 숨겼던 캔버스를 모두 되돌린다. 숨긴 적이 없으면 아무것도 하지 않는다
        /// </summary>
        public static void RestoreAll()
        {
            foreach (Canvas canvas in hidden)
            {
                if (canvas != null)
                {
                    canvas.enabled = true;
                }
            }

            hidden.Clear();
        }
    }
}
