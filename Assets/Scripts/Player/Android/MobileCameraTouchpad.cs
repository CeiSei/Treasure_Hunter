using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;

public class MobileCameraTouchpad : OnScreenControl, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Tooltip("Input System으로 보낼 가상 경로 (기본값: 게임패드 우측 스틱)")]
    [InputControl(layout = "Vector2")]
    [SerializeField] private string m_ControlPath = "<Gamepad>/rightStick";
    
    [Tooltip("모바일 화면 스와이프 감도")]
    [SerializeField] private float sensitivity = 0.5f;

    private Vector2 lastDelta;
    private bool isDragging = false;

    // OnScreenControl이 신호를 보낼 경로를 설정하는 필수 프로퍼티
    protected override string controlPathInternal
    {
        get => m_ControlPath;
        set => m_ControlPath = value;
    }

    void Update()
    {
        // 드래그가 멈췄을 때(OnDrag가 호출되지 않는 프레임) 델타 값을 0으로 리셋하여 시점이 계속 도는 현상 방지
        if (!isDragging && lastDelta != Vector2.zero)
        {
            lastDelta = Vector2.zero;
            SendValueToControl(lastDelta);
        }
        
        // 매 프레임 초기화 (손가락이 움직이고 있다면 OnDrag에서 다시 true로 덮어씌움)
        isDragging = false; 
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isDragging = true;
        lastDelta = Vector2.zero;
        SendValueToControl(lastDelta);
    }

    public void OnDrag(PointerEventData eventData)
    {
        isDragging = true;
        // UI 드래그 델타 값을 가져와 감도를 곱한 후 Input System으로 전송
        lastDelta = eventData.delta * sensitivity;
        SendValueToControl(lastDelta);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
        lastDelta = Vector2.zero;
        SendValueToControl(lastDelta);
    }
}