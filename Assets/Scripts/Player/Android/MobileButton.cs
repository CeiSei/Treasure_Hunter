using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class MobileButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public event Action OnPointerDownEvent;
    public event Action OnPointerUpEvent;

    public void OnPointerDown(PointerEventData eventData)
    {
        // New Input System의 performed 와 동일한 타이밍
        OnPointerDownEvent?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // New Input System의 canceled 와 동일한 타이밍
        OnPointerUpEvent?.Invoke();
    }
}