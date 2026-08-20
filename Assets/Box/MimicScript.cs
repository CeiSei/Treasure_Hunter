using UnityEngine;
using Unity.Netcode;
using Cysharp.Threading.Tasks;

public class MimicScript : NetworkBehaviour, IInteraction
{
    [Header("Debuff Settings")]
    [SerializeField] private float slowRatio = 0.5f;
    [SerializeField] private float debuffTime = 5f;

    Animator animator;
    void Awake()
    {
        animator = GetComponent<Animator>();
    }
    
    public override void OnNetworkSpawn()
    {
        if (InGameManager.Instance != null)
        {
            InGameManager.Instance.AddNewInteraction(this);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (InGameManager.Instance != null)
        {
            InGameManager.Instance.DeleteInteraction(this);
        }
    }

    public void InteractServer(GameObject interactor)
    {
        if (!IsServer) return;

        PlayerControl player = interactor.GetComponent<PlayerControl>();
        
        if(animator != null) animator.SetTrigger("Open");

        if (player != null)
        {
            RotateToPlayer(Quaternion.LookRotation(player.transform.position - transform.position)).Forget();
            
            ClientRpcParams rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { player.OwnerClientId } }
            };
            
            // 미믹은 이동 속도(MoveSpeed) 디버프를 유발하도록 StatType을 명시하여 전송
            player.ApplyStatusEffectClientRpc(StatType.MoveSpeed, slowRatio, debuffTime, rpcParams);
        }
    }

    async UniTaskVoid RotateToPlayer(Quaternion dest)
    {
        while (true)
        {
            if(transform.rotation != dest)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, dest, 400f * Time.deltaTime);
            else
                break;
            await UniTask.Yield(PlayerLoopTiming.Update);
        }
    }
}