using System;

namespace UnityToolKit.EventTrigger
{
    using UnityEngine;
    using System.Collections;
    using UnityEngine.EventSystems;

    public class EventTriggerListener : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerEnterHandler,
        IPointerExitHandler, IPointerUpHandler, IDragHandler
    {
        public event Action<GameObject, PointerEventData> OnClick;
        public event Action<GameObject, PointerEventData> OnDown;
        public event Action<GameObject, PointerEventData> OnEnter;
        public event Action<GameObject, PointerEventData> OnExit;
        public event Action<GameObject, PointerEventData> OnUp;
        public event Action<GameObject, PointerEventData> OnDragging;

        public static EventTriggerListener Get(GameObject go)
        {
            EventTriggerListener listener = go.GetComponent<EventTriggerListener>();
            if (listener == null) listener = go.AddComponent<EventTriggerListener>();
            return listener;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (OnClick != null) OnClick(gameObject, eventData);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (OnDown != null) OnDown(gameObject, eventData);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (OnEnter != null) OnEnter(gameObject, eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (OnExit != null) OnExit(gameObject, eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (OnUp != null) OnUp(gameObject, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (OnDragging != null) OnDragging(gameObject, eventData);
        }

        public void SetCommonClick(Action<GameObject, PointerEventData> onPointDown,
            Action<GameObject, PointerEventData> onPointUp)
        {
            this.OnDown = onPointDown;
            this.OnUp = onPointUp;
        }
    }
}