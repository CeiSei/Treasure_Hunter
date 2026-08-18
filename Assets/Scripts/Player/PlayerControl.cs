using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using Unity.Netcode;
using Unity.Cinemachine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class PlayerControl : NetworkBehaviour
{
    Vector2 InputDir;
    Vector3 MoveDir;

    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotateSpeed = 600f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float fallMultiplier = 2.5f;
    [SerializeField] private float lowJumpMultiplier = 2f;

    [Header("Coyote Time")]
    [SerializeField] private float coyoteTimeWindow = 0.15f;
    private float coyoteTimer;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private float jumpCoolDown = 0.3f;
    private bool isGrounded;
    private bool isJumpCooldown = false;
    private bool isJumpPushed = false;

    [Header("Interaction Settings")]
    [SerializeField] private float interactionRadius = 5f;
    [SerializeField] private LayerMask interactionLayer;

    [Header("Camera Settings")]
    [SerializeField] private Transform cameraTarget;

    CancellationTokenSource CTS;
    Rigidbody rb;
    Animator _animator;
    private NetworkPlayerState playerState;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        playerState = GetComponent<NetworkPlayerState>(); 
    }

    public int GetTeam()
    {
        if (playerState != null) return playerState.TeamIndex.Value;
        return 0; 
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            ModularMapGenerator.OnMapGenerated += TrySetSpawnPosition;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

            ModularMapGenerator mapGenerator = FindFirstObjectByType<ModularMapGenerator>();

            if (mapGenerator == null || ModularMapGenerator.IsMapReady)
            {
                TrySetSpawnPosition();
            }
            else
            {
                rb.isKinematic = true;
                transform.position = new Vector3(0, 20f, 0); 
            }

            AttachCamera();

            PlayerInputRouter.Instance.OnJump += Jump;
            PlayerInputRouter.Instance.OnJumpEnd += JumpEnd;
            PlayerInputRouter.Instance.OnInteraction += Interact;

            CTS = new CancellationTokenSource();
            Move(CTS.Token).Forget();
        }
        else
        {
            if (rb != null) rb.isKinematic = true;
            enabled = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            ModularMapGenerator.OnMapGenerated -= TrySetSpawnPosition;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void TrySetSpawnPosition()
    {
        List<Transform> spawnList = new List<Transform>();

        // 비활성화된 청크 내부의 SpawnPoint까지 모두 찾기
        GameObject mapHolder = GameObject.Find("ModularMap");
        if (mapHolder != null)
        {
            Transform[] allTransforms = mapHolder.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allTransforms)
            {
                if (t.CompareTag("SpawnPoint"))
                {
                    spawnList.Add(t);
                }
            }
        }
        else
        {
            GameObject[] activeSpawns = GameObject.FindGameObjectsWithTag("SpawnPoint");
            foreach (var spawn in activeSpawns)
            {
                spawnList.Add(spawn.transform);
            }
        }

        if (spawnList.Count > 0)
        {
            int seed = ModularMapGenerator.CurrentMapSeed != 0 ? ModularMapGenerator.CurrentMapSeed : (int)OwnerClientId;
            System.Random rnd = new System.Random(seed);

            for (int i = 0; i < spawnList.Count; i++)
            {
                int randomIndex = rnd.Next(i, spawnList.Count);
                Transform temp = spawnList[i];
                spawnList[i] = spawnList[randomIndex];
                spawnList[randomIndex] = temp;
            }

            int spawnIndex = (int)(OwnerClientId % (ulong)spawnList.Count);
            transform.position = spawnList[spawnIndex].position;
        }
        else
        {
            transform.position = new Vector3(0, 3f, 0); 
        }
        
        if (rb != null) rb.isKinematic = false;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AttachCamera();

        ModularMapGenerator mapGenerator = FindFirstObjectByType<ModularMapGenerator>();
        
        if (mapGenerator != null && !ModularMapGenerator.IsMapReady)
        {
            if (rb != null) rb.isKinematic = true;
            transform.position = new Vector3(0, 20f, 0);
        }
        else
        {
            TrySetSpawnPosition();
        }
    }

    private void AttachCamera()
    {
        CinemachineCamera vCam = FindFirstObjectByType<CinemachineCamera>();
        if (vCam != null)
        {
            Transform targetTransform = cameraTarget != null ? cameraTarget : this.transform;
            vCam.Follow = targetTransform;
            vCam.LookAt = targetTransform;
        }
    }

    void Update()
    {
        if(!IsOwner) return;

        isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer);
        
        if (isGrounded)
        {
            if(!isJumpPushed) _animator.SetBool("Jump", false);
            coyoteTimer = coyoteTimeWindow;
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
        }
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;
        ApplyBetterGravity();
    }

    private async UniTaskVoid Move(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            InputDir = PlayerInputRouter.Instance.moveDir;
            MoveDir = Camera.main.transform.forward * InputDir.y + Camera.main.transform.right * InputDir.x;
            MoveDir.y = 0;
            
            transform.position += MoveDir.normalized * moveSpeed * Time.deltaTime;

            if(InputDir != Vector2.zero)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(MoveDir), Time.deltaTime * rotateSpeed);
            }
            
            _animator.SetFloat("MoveSpeed", MoveDir.normalized.magnitude);

            await UniTask.Yield(PlayerLoopTiming.Update);
        }
    }

    void Jump()
    {
        if(coyoteTimer <= 0f) return;
        if(isJumpCooldown) return;
        
        isJumpPushed = true;
        JumpCooldown(jumpCoolDown).Forget();

        _animator.SetBool("Jump", true);

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        coyoteTimer = 0f;
    }

    void JumpEnd()
    {
        isJumpPushed = false;
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f, rb.linearVelocity.z);
    }

    async UniTaskVoid JumpCooldown(float jumpCoolDown)
    {
        isJumpCooldown = true;
        await UniTask.WaitForSeconds(jumpCoolDown);
        isJumpCooldown = false;
        await UniTask.Yield(PlayerLoopTiming.Update);
    }

    void ApplyBetterGravity()
    {
        if (rb.linearVelocity.y < 0f)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1f) * Time.deltaTime;
        }
        else if (rb.linearVelocity.y > 0f && !isJumpPushed)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1f) * Time.deltaTime;
        }
    }

    void Interact()
    {
        Interaction().Forget();
    }

    async UniTaskVoid Interaction()
    {
        Collider[] interactionCols = Physics.OverlapSphere(gameObject.transform.position, interactionRadius, interactionLayer);

        float closestDist = Mathf.Infinity;
        Collider closestCol = null;

        foreach(Collider col in interactionCols)
        {
            if(closestCol == null) closestCol = col;
            if(Vector3.Distance(gameObject.transform.position, col.transform.position) < closestDist)
            {
                closestDist = Vector3.Distance(gameObject.transform.position, col.transform.position);
                closestCol = col;
            }
        }

        if(closestCol != null) 
        {
            NetworkObject targetNetObj = closestCol.GetComponent<NetworkObject>();
            if (targetNetObj != null)
            {
                RequestInteractServerRpc(targetNetObj.NetworkObjectId);
            }
        }
    }

    [ServerRpc]
    void RequestInteractServerRpc(ulong targetNetworkObjectId, ServerRpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetObject))
        {
            float distance = Vector3.Distance(transform.position, targetObject.transform.position);
            if(distance <= interactionRadius + 1f) 
            {
                IInteraction interactable = targetObject.GetComponent<IInteraction>();
                if (interactable != null)
                {
                    interactable.InteractServer(gameObject);
                }
            }
        }
    }

    public async UniTaskVoid SetPlayerSpeedDebuff(float slow, float time)
    {
        moveSpeed = moveSpeed * slow;
        await UniTask.WaitForSeconds(time);
        moveSpeed = moveSpeed / slow;
    } 

    void OnDisable()
    {
        if(CTS != null) CTS.Cancel();
        
        if (IsOwner && PlayerInputRouter.Instance != null)
        {
            PlayerInputRouter.Instance.OnJump -= Jump;
            PlayerInputRouter.Instance.OnJumpEnd -= JumpEnd;
            PlayerInputRouter.Instance.OnInteraction -= Interact;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(gameObject.transform.position, interactionRadius);
    }
}