using System;
using UnityEngine;

public class PlayerInputRouter : MonoBehaviour
{
    public static PlayerInputRouter Instance {get; private set;}
    private PlayerKeyInput PKI;

    public Vector2 moveDir {get; private set;}

    public event Action OnInteraction;
    public event Action OnJump;
    public event Action OnJumpEnd;

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
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        PKI.Player.Move.performed += dir => moveDir = dir.ReadValue<Vector2>();
        PKI.Player.Move.canceled += _ => moveDir = Vector3.zero;

        PKI.Player.Interact.performed += _ => OnInteraction?.Invoke();

        PKI.Player.Jump.performed += _ => OnJump?.Invoke();
        PKI.Player.Jump.canceled += _ => OnJumpEnd?.Invoke();

        PKI.Enable();
    }

    void OnDisable()
    {
        if(Instance == this)
            PKI.Disable();
    }
}
