using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using TMPro;
using System.Collections.Generic;
using Unity.Netcode;
using System;

public class InGameManager : NetworkBehaviour
{
    public static InGameManager Instance { get; private set; }

    CancellationTokenSource CTS;

    [Header("UI References")]
    [SerializeField] private CanvasGroup loadingCanvasGroup; 
    [SerializeField] private float fadeDuration = 1.0f;
    
    [SerializeField] private TextMeshProUGUI TimeText;
    [SerializeField] private TextMeshProUGUI RedTeamScoreText;
    [SerializeField] private TextMeshProUGUI BlueTeamScoreText;
    
    [Header("Network Variables")]
    public NetworkVariable<int> TimeLeft = new NetworkVariable<int>(120, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> RedTeamScore = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> BlueTeamScore = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Game State")]
    [SerializeField] private List<IInteraction> interactionList = new List<IInteraction>();
    private bool isGameOver = false;

    // --- 중복 실행 방지를 위한 안전장치 (Lock) ---
    private bool isTimerStarted = false;
    private bool isFadingOut = false;

    public static event Action<int> OnGameOver; 

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public override void OnNetworkSpawn()
    {
        // 1차 방어: 만약 씬에 InGameManager가 중복으로 생성되었다면 
        // 늦게 생성된 녀석은 아무 로직도 실행하지 않고 바로 종료시킵니다.
        if (Instance != this) return;

        CTS = new CancellationTokenSource();
        
        // 씬 시작 시 Lock 초기화
        isTimerStarted = false;
        isFadingOut = false;

        if (loadingCanvasGroup != null)
        {
            loadingCanvasGroup.gameObject.SetActive(true);
            loadingCanvasGroup.alpha = 1f;
            loadingCanvasGroup.blocksRaycasts = true; 
        }

        if (IsServer)
        {
            TimeLeft.Value = WaitingRoomUI.SelectedGameTime;
            isGameOver = false;

            if (ModularMapGenerator.IsMapReady) StartGameTimer();
            else ModularMapGenerator.OnMapGenerated += StartGameTimer;
        }

        if (ModularMapGenerator.IsMapReady) HideLoadingScreen();
        else ModularMapGenerator.OnMapGenerated += HideLoadingScreen;

        TimeLeft.OnValueChanged += (oldValue, newValue) => RefreshTimeUI();
        RedTeamScore.OnValueChanged += (oldValue, newValue) => RefreshScoreUI();
        BlueTeamScore.OnValueChanged += (oldValue, newValue) => RefreshScoreUI();
        
        RefreshTimeUI();
        RefreshScoreUI();
    }

    public override void OnNetworkDespawn()
    {
        if (Instance != this) return;

        ModularMapGenerator.OnMapGenerated -= StartGameTimer;
        ModularMapGenerator.OnMapGenerated -= HideLoadingScreen;

        if (CTS != null)
        {
            CTS.Cancel();
            CTS.Dispose();
        }

        CleanupTreasures();
    }

    private void HideLoadingScreen()
    {
        // 2차 방어: 이미 페이드 아웃이 시작되었다면 이후의 호출은 무시합니다.
        if (isFadingOut) return;
        isFadingOut = true;

        if (loadingCanvasGroup != null)
        {
            FadeOutLoadingScreen(CTS.Token).Forget();
        }
    }

    private async UniTaskVoid FadeOutLoadingScreen(CancellationToken token)
    {
        float elapsedTime = 0f;
        await UniTask.WaitForSeconds(3f);
        
        while (elapsedTime < fadeDuration)
        {
            if (token.IsCancellationRequested) return;

            elapsedTime += Time.deltaTime;
            loadingCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
            
            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        if (!token.IsCancellationRequested)
        {
            loadingCanvasGroup.alpha = 0f;
            loadingCanvasGroup.blocksRaycasts = false;
            loadingCanvasGroup.gameObject.SetActive(false);
        }
    }

    private void StartGameTimer()
    {
        // 2차 방어: 이미 타이머가 돌아가고 있거나 게임이 끝났다면 실행하지 않습니다.
        if (isGameOver || isTimerStarted) return;
        isTimerStarted = true;
        
        Timer(CTS.Token).Forget();
    }

    public void AddNewInteraction(IInteraction iit)
    {
        interactionList.Add(iit);    
    }

    public void DeleteInteraction(IInteraction iit)
    {
        if(isGameOver) return;

        interactionList.Remove(iit);

        if(interactionList.Count <= 0)
        {
            if(IsServer) EndGame();
        }
    }

    async UniTaskVoid Timer(CancellationToken token)
    {
        while (!token.IsCancellationRequested && TimeLeft.Value > 0)
        {
            await UniTask.WaitForSeconds(1, cancellationToken: token);
            
            if (IsServer && !isGameOver)
            {
                TimeLeft.Value -= 1;
                
                if(TimeLeft.Value <= 0)
                {
                    EndGame();
                }
            }
        }
    }

    public void AddScore(int teamIndex, int scoreAmount)
    {
        if (!IsServer || isGameOver) return;

        if (teamIndex == 1) RedTeamScore.Value += scoreAmount;
        else if (teamIndex == 2) BlueTeamScore.Value += scoreAmount;
    }

    void RefreshTimeUI()
    {
        int t = TimeLeft.Value;
        if(TimeText != null)
            TimeText.text = (t / 60).ToString() + " : " + (t % 60 >= 10 ? (t % 60).ToString() : ("0" + (t % 60).ToString()));
    }

    void RefreshScoreUI()
    {
        if(RedTeamScoreText != null)
            RedTeamScoreText.text = RedTeamScore.Value.ToString();
        
        if(BlueTeamScoreText != null)
            BlueTeamScoreText.text = BlueTeamScore.Value.ToString();
    }

    private void EndGame()
    {
        if(isGameOver) return;
        isGameOver = true;
        
        int winningTeam = 0; 

        if (RedTeamScore.Value > BlueTeamScore.Value) winningTeam = 1; 
        else if (BlueTeamScore.Value > RedTeamScore.Value) winningTeam = 2; 

        ShowResultClientRpc(winningTeam);
    }

    [ClientRpc]
    private void ShowResultClientRpc(int winningTeam)
    {
        OnGameOver?.Invoke(winningTeam);
    }

    private void CleanupTreasures()
    {
        if (!IsServer) return;

        for (int i = interactionList.Count - 1; i >= 0; i--)
        {
            IInteraction interactable = interactionList[i];
            
            if (interactable is NetworkBehaviour netBehaviour)
            {
                NetworkObject netObj = netBehaviour.GetComponent<NetworkObject>();
                
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn(true); 
                }
            }
        }
        
        interactionList.Clear();
    }
}