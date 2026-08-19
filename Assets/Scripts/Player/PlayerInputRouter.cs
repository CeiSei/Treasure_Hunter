using System;
using System.Linq; // 배열 검사(Contains)를 위해 추가
using UnityEngine;
using UnityEngine.SceneManagement; // 씬 관리를 위해 추가
using UnityEngine.InputSystem; // InputAction 반환 및 에셋 활성화를 위해 추가

public class PlayerInputRouter : MonoBehaviour
{
    public static PlayerInputRouter Instance {get; private set;}
    private PlayerKeyInput PKI;

    public Vector2 moveDir {get; private set;}

    public event Action OnInteraction;
    public event Action OnJump;
    public event Action OnJumpEnd;

#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
    [Header("Mobile UI References")]
    [Tooltip("모바일 UI를 묶어둔 최상단 캔버스 객체")]
    [SerializeField] private GameObject mobileInputCanvas;
    [SerializeField] private VirtualJoystick virtualJoystick;
    [SerializeField] private MobileButton jumpButton;
    [SerializeField] private MobileButton interactButton;

    [Header("Editor Settings")]
    [Tooltip("유니티 에디터 환경에서도 모바일 UI 조작을 테스트하려면 체크하세요.")]
    [SerializeField] private bool testMobileUIInEditor = false;

    [Header("Scene Settings")]
    [Tooltip("모바일 UI가 켜질 씬 이름들 (정확히 입력해야 합니다)")]
    [SerializeField] private string[] activeMobileUIScenes = { "WaitingRoom", "PlayScene" };
#endif

    void Awake()
    {
        if(Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject); 
        PKI = new PlayerKeyInput(); 
    }

    void OnEnable()
    {
        // 씬 로드 완료 이벤트 구독
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        if(Instance == this)
        {
            PKI.Disable(); 
            SceneManager.sceneLoaded -= OnSceneLoaded;
            
#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
            if (virtualJoystick != null) virtualJoystick.OnMoveEvent -= HandleMobileMove;
            if (jumpButton != null)
            {
                jumpButton.OnPointerDownEvent -= HandleMobileJump;
                jumpButton.OnPointerUpEvent -= HandleMobileJumpEnd;
            }
            if (interactButton != null) interactButton.OnPointerDownEvent -= HandleMobileInteract;
#endif
        }
    }

    void Start()
    {
        // 핵심 해결책: 시네머신이 인식할 수 있도록 원본 에셋 자체를 활성화합니다.
        PKI.asset.Enable();

        PKI.Player.Move.performed += dir => moveDir = dir.ReadValue<Vector2>(); 
        PKI.Player.Move.canceled += _ => moveDir = Vector2.zero; 

        PKI.Player.Interact.performed += _ => OnInteraction?.Invoke(); 

        PKI.Player.Jump.performed += _ => OnJump?.Invoke(); 
        PKI.Player.Jump.canceled += _ => OnJumpEnd?.Invoke(); 

        PKI.Enable(); 

#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
        bool useMobile = false;
#if UNITY_ANDROID || UNITY_IOS
        useMobile = true;
#elif UNITY_EDITOR
        useMobile = testMobileUIInEditor;
#endif
        if (useMobile)
        {
            if (virtualJoystick != null) virtualJoystick.OnMoveEvent += HandleMobileMove;
            
            if (jumpButton != null)
            {
                jumpButton.OnPointerDownEvent += HandleMobileJump;
                jumpButton.OnPointerUpEvent += HandleMobileJumpEnd;
            }

            if (interactButton != null)
            {
                interactButton.OnPointerDownEvent += HandleMobileInteract;
            }
        }
#endif
        // 게임이 처음 켜질 때 현재 씬 검사
        UpdateMobileUIState(SceneManager.GetActiveScene().name);
    }

    // 시네머신 브릿지 스크립트(CinemachineInputFix)에서 런타임에 호출할 함수
    public InputAction GetLookAction()
    {
        return PKI.Player.Look;
    }

    // 씬이 전환될 때마다 실행되는 콜백 함수
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateMobileUIState(scene.name);
    }

    private void UpdateMobileUIState(string sceneName)
    {
#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
        bool useMobile = false;
#if UNITY_ANDROID || UNITY_IOS
        useMobile = true;
#elif UNITY_EDITOR
        useMobile = testMobileUIInEditor;
#endif
        if (mobileInputCanvas != null)
        {
            // 현재 활성화된 씬의 이름이 배열 안에 존재하는지 확인
            bool isAllowedScene = activeMobileUIScenes.Contains(sceneName);
            
            // 모바일 환경이면서 허용된 씬일 때만 캔버스를 켬
            mobileInputCanvas.SetActive(useMobile && isAllowedScene);
        }

        // UI가 꺼질 때 이전에 입력하던 조이스틱 값이 남아 캐릭터가 미끄러지는 버그 방지
        if (!useMobile || !activeMobileUIScenes.Contains(sceneName))
        {
            moveDir = Vector2.zero;
        }
#endif
    }

#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
    private void HandleMobileMove(Vector2 dir) => moveDir = dir;
    private void HandleMobileJump() => OnJump?.Invoke();
    private void HandleMobileJumpEnd() => OnJumpEnd?.Invoke();
    private void HandleMobileInteract() => OnInteraction?.Invoke();
#endif
}