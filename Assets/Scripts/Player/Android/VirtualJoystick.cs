using System;
using UnityEngine;
using UnityEngine.EventSystems;

// 이 스크립트는 화면 절반을 덮는 투명 패널(Touch Area)에 부착해야 합니다.
[RequireComponent(typeof(RectTransform))]
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("UI References")]
    [Tooltip("실제 화면에 띄워질 조이스틱 배경(테두리) UI 객체")]
    [SerializeField] private RectTransform joystickBackground;
    [Tooltip("실제로 움직이는 조이스틱 손잡이 UI 객체")]
    [SerializeField] private RectTransform joystickHandle;

    [Header("Settings")]
    [Tooltip("핸들이 배경을 벗어날 수 있는 최대 거리 비율 (0 ~ 1)")]
    [SerializeField] private float handleRange = 0.8f;
    [Tooltip("터치하지 않을 때 조이스틱을 투명하게 숨길지 여부")]
    [SerializeField] private bool hideOnRelease = true;

    // PlayerInputRouter로 방향 벡터를 전달할 이벤트
    public event Action<Vector2> OnMoveEvent;

    private RectTransform touchArea; // 이 스크립트가 붙은 투명 패널
    private Vector2 joystickCenter;
    private float backgroundRadius;
    private CanvasGroup canvasGroup;
    private Vector2 originalPosition;

    void Start()
    {
        touchArea = GetComponent<RectTransform>();
        
        // CanvasGroup으로 투명도 제어 (없으면 자동 추가)
        canvasGroup = joystickBackground.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = joystickBackground.gameObject.AddComponent<CanvasGroup>();

        // 해상도에 따른 조이스틱 반경 계산
        backgroundRadius = (joystickBackground.sizeDelta.x / 2f) * handleRange;
        
        // 초기 앵커 포지션 저장
        originalPosition = joystickBackground.anchoredPosition;

        if (hideOnRelease) HideJoystick();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // 1. 터치한 스크린 좌표를 캔버스(touchArea) 기준의 로컬 좌표로 변환하여 해상도 대응
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            touchArea, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);
        
        // 2. 조이스틱 UI 전체를 터치한 위치로 이동
        joystickBackground.anchoredPosition = localPoint;
        
        // 3. 이동한 후의 월드 좌표(스크린 좌표계)를 새로운 중심으로 설정
        joystickCenter = joystickBackground.position; 

        if (hideOnRelease) ShowJoystick();

        // 터치하자마자 바로 드래그 판정 시작
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 touchPosition = eventData.position;
        Vector2 direction = touchPosition - joystickCenter;

        // 이동 반경을 배경 원 안으로 제한
        if (direction.magnitude > backgroundRadius)
        {
            direction = direction.normalized * backgroundRadius;
        }

        // 핸들 위치 갱신
        joystickHandle.position = joystickCenter + direction;
        
        // -1.0 ~ 1.0 사이의 정규화된 입력 벡터를 라우터로 전달
        OnMoveEvent?.Invoke(direction / backgroundRadius);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // 손잡이를 중심으로 원복하고 입력값 0으로 초기화
        joystickHandle.position = joystickCenter;
        OnMoveEvent?.Invoke(Vector2.zero);
        
        if (hideOnRelease)
        {
            HideJoystick();
            // 숨길 때 원래 위치로 되돌려 둡니다.
            joystickBackground.anchoredPosition = originalPosition; 
        }
    }

    private void HideJoystick()
    {
        canvasGroup.alpha = 0f;
    }

    private void ShowJoystick()
    {
        canvasGroup.alpha = 1f;
    }
}