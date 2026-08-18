using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using TMPro;
using System.Collections.Generic;
using Unity.Netcode;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    CancellationTokenSource CTS;

    [SerializeField] private TextMeshProUGUI TimeText;
    
    // 서버만 값을 쓸 수 있고, 모두가 읽을 수 있는 네트워크 변수
    public NetworkVariable<int> TimeLeft = new NetworkVariable<int>(120, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [SerializeField] private List<IInteraction> interactionList = new List<IInteraction>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            CTS = new CancellationTokenSource();
            StartGameTimer();
        }

        // 변수 값이 변경될 때마다 UI 갱신 이벤트 연결
        TimeLeft.OnValueChanged += (oldValue, newValue) => RefreshTimeUI();
        RefreshTimeUI();
    }

    public override void OnNetworkDespawn()
    {
        if (CTS != null) CTS.Cancel();
    }

    public void AddNewInteraction(IInteraction iit)
    {
        interactionList.Add(iit);    
    }

    public void DeleteInteraction(IInteraction iit)
    {
        interactionList.Remove(iit);

        if(interactionList.Count <= 0)
        {
            Debug.Log("더 이상 획득 가능한 보물이 없습니다!");
        }
    }

    void StartGameTimer()
    {
        Timer(CTS.Token).Forget();
    }

    async UniTaskVoid Timer(CancellationToken token)
    {
        while (!token.IsCancellationRequested && TimeLeft.Value > 0)
        {
            await UniTask.WaitForSeconds(1, cancellationToken: token);
            
            // 시간 차감은 무조건 서버에서만 진행합니다.
            if (IsServer)
            {
                TimeLeft.Value -= 1;
            }
        }
    }

    void RefreshTimeUI()
    {
        int t = TimeLeft.Value;
        TimeText.text = (t / 60).ToString() + " : " + (t % 60 >= 10 ? (t % 60).ToString() : ("0" + (t % 60).ToString()));
    }
}