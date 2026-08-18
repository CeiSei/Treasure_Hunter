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
        // 상자가 스폰되면 호스트와 클라이언트 모두 InGameManager의 리스트에 이 상자를 추가함
        if (InGameManager.Instance != null)
        {
            InGameManager.Instance.AddNewInteraction(this);
        }
    }

    public override void OnNetworkDespawn()
    {
        // 상자가 파괴되거나 씬이 끝날 때 리스트에서 제거
        if (InGameManager.Instance != null)
        {
            InGameManager.Instance.DeleteInteraction(this);
        }
    }

    // 플레이어가 상호작용 키를 눌렀을 때 서버에서만 실행되는 함수
    public void InteractServer(GameObject interactor)
    {
        if (!IsServer) return;

        // 1. 상호작용한 플레이어의 PlayerControl 컴포넌트를 가져옴
        PlayerControl player = interactor.GetComponent<PlayerControl>();
        
        if(animator != null) animator.SetTrigger("Open");

        if (player != null)
        {
            RotateToPlayer(Quaternion.LookRotation(player.transform.position - transform.position)).Forget();
            player.GetComponent<PlayerControl>().SetPlayerSpeedDebuff(slowRatio, debuffTime).Forget();
        }

        // 3. 점수 획득 후 동적 스폰된 상자를 완전히 파괴하여 모든 클라이언트 화면에서 제거
        //GetComponent<NetworkObject>().Despawn(true);
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
