using System;

namespace UnityToolKit.EventTrigger
{
    using UnityEngine;
    using System.Collections;
    using UnityEngine.EventSystems;

    public class EventTriggerListener : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerEnterHandler,
        IPointerExitHandler, IPointerUpHandler, IDragHandler, IBeginDragHandler, IEndDragHandler

    {
        public event Action<GameObject, PointerEventData> OnClick;
        public event Action<GameObject, PointerEventData> OnDown;
        public event Action<GameObject, PointerEventData> OnEnter;
        public event Action<GameObject, PointerEventData> OnExit;
        public event Action<GameObject, PointerEventData> OnUp;
        public event Action<GameObject, PointerEventData> OnDragging, OnBeinDragCallback, OnEndDragCallback;

        private bool IsDragging;

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
            if (OnUp != null && !IsDragging) OnUp(gameObject, eventData);

            IsDragging = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            IsDragging = true;

            if (OnDragging != null) OnDragging(gameObject, eventData);
        }

        public void SetCommonClick(Action<GameObject, PointerEventData> onPointDown,
            Action<GameObject, PointerEventData> onPointUp)
        {
            this.OnDown = onPointDown;
            this.OnUp = onPointUp;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (OnBeinDragCallback != null) OnBeinDragCallback(gameObject, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (OnEndDragCallback != null) OnEndDragCallback(gameObject, eventData);
        }
    }
}