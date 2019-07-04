namespace UnityToolKit.Utils
{
    using UnityEngine.EventSystems;
    using UnityEngine;
    using System.Collections.Generic;

    /// <summary>
    /// 输入控制,判断当前点击是否在UGUI上面.
    /// </summary>
    public class InputUtils
    {
        //手动控制
        public static bool isOnUI = false;

        private static List<RaycastResult> m_list = new List<RaycastResult>();

        /// <summary>
        /// 判断鼠标是否在UGUI上面
        /// </summary>
        /// <returns>如果在UGUI上，返回true.</returns>
        public static bool CheckMouseOnUI()
        {
            if (isOnUI) return true;
            return IsPointerOverGameObject();
        }


        /// <summary>
        /// 判断是否在ugui上面
        /// </summary>
        /// <returns><c>true</c>, if mouse on UGU was checked, <c>false</c> otherwise.</returns>
        static bool IsPointerOverGameObject()
        {
            if (EventSystem.current)
            {
                if (Input.touchCount > 0)
                {
                    for (int i = 0; i < Input.touchCount; ++i)
                    {
                        if (EventSystem.current.IsPointerOverGameObject(Input.GetTouch(i).fingerId))
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    return EventSystem.current.IsPointerOverGameObject();
                }
                PointerEventData eventData = new PointerEventData(EventSystem.current);
                eventData.pressPosition = Input.mousePosition;
                eventData.position = Input.mousePosition;

                m_list.Clear();
                EventSystem.current.RaycastAll(eventData, m_list);
                return m_list.Count > 0;
            }
            return false;
        }

        public static bool IsPointerOverGameObject(Canvas canvas, Vector2 screenPosition)
        {
            if (isOnUI) return true;

            if (EventSystem.current == null) return false;

            PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
            eventDataCurrentPosition.position = screenPosition;
            UnityEngine.UI.GraphicRaycaster uiRaycaster =
                canvas.gameObject.GetComponent<UnityEngine.UI.GraphicRaycaster>();

            m_list.Clear();
            uiRaycaster.Raycast(eventDataCurrentPosition, m_list);

            return m_list.Count > 0;
        }
    }
}