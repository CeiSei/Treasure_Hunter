using UnityEngine;
using Unity.Netcode;
using Cysharp.Threading.Tasks;

// IInteraction 인터페이스를 상속받았다고 가정합니다.
public class BoxScript : NetworkBehaviour, IInteraction
{
    [Header("Score Settings")]
    [SerializeField] private int scoreAmount = 1;
    bool isOpened = false;
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
        if (isOpened) return;
        // 1. 상호작용한 플레이어의 PlayerControl 컴포넌트를 가져옴
        isOpened = true;
        PlayerControl player = interactor.GetComponent<PlayerControl>();

        if(animator != null)
            animator.SetTrigger("Open");

        if (player != null)
        {
            int teamIndex = player.GetTeam();

            // 2. 소속 팀이 있다면 점수 추가
            if (teamIndex != 0)
            {
                InGameManager.Instance.AddScore(teamIndex, scoreAmount);
            }
        }

        // 3. 점수 획득 후 동적 스폰된 상자를 완전히 파괴하여 모든 클라이언트 화면에서 제거
        //GetComponent<NetworkObject>().Despawn(true);
    }
}