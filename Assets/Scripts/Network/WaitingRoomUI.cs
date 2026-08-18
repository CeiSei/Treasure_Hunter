using UnityEngine;
using TMPro;
using Unity.Netcode;
using UnityEngine.UI;

public class WaitingRoomUI : NetworkBehaviour
{
    [Header("Room Info")]
    [SerializeField] private TextMeshProUGUI roomCodeText;

    [Header("Player List UI")]
    [SerializeField] private TextMeshProUGUI[] playerTextSlots; 

    [Header("Game Start UI")]
    [SerializeField] private Button startGameButton; 
    [SerializeField] private string gameSceneName = "GameScene"; 
    
    [Tooltip("팀 미선택 시 방장에게 띄워줄 경고 텍스트")]
    [SerializeField] private TextMeshProUGUI warningText; 

    [Header("Team Selection UI")]
    [SerializeField] private Button redTeamButton;   
    [SerializeField] private Button blueTeamButton;  

    [Header("Time Setting UI")]
    [SerializeField] private TextMeshProUGUI timeDisplayText; 
    [SerializeField] private Button timeIncreaseButton; 
    [SerializeField] private Button timeDecreaseButton; 

    [Header("Map Size Setting UI")]
    [SerializeField] private TextMeshProUGUI mapSizeDisplayText; 
    [SerializeField] private Button mapSizeIncreaseButton; 
    [SerializeField] private Button mapSizeDecreaseButton; 

    [Header("Treasure Setting UI")]
    [SerializeField] private TextMeshProUGUI treasureDisplayText; 
    [SerializeField] private Button treasureIncreaseButton; 
    [SerializeField] private Button treasureDecreaseButton; 

    public NetworkVariable<int> netGameTime = new NetworkVariable<int>(120, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> netMapSize = new NetworkVariable<int>(8, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> netTreasureCount = new NetworkVariable<int>(10, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public static int SelectedGameTime = 120; 
    public static int SelectedMapSize = 8; 
    public static int SelectedTreasureCount = 10; 

    public override void OnNetworkSpawn()
    {
        netGameTime.OnValueChanged += (oldVal, newVal) => { SelectedGameTime = newVal; UpdateTimeUI(); };
        netMapSize.OnValueChanged += (oldVal, newVal) => { SelectedMapSize = newVal; UpdateMapSizeUI(); };
        netTreasureCount.OnValueChanged += (oldVal, newVal) => { SelectedTreasureCount = newVal; UpdateTreasureUI(); };

        SelectedGameTime = netGameTime.Value;
        SelectedMapSize = netMapSize.Value;
        SelectedTreasureCount = netTreasureCount.Value;
        
        UpdateTimeUI();
        UpdateMapSizeUI();
        UpdateTreasureUI();
    }

    void Start()
    {
        if (roomCodeText != null) roomCodeText.text = $"방 코드 : {RelayManager.CurrentJoinCode}";
        if (warningText != null) warningText.text = ""; // 시작 시 경고문구 초기화

        NetworkPlayerState.OnPlayerStateChanged += RefreshPlayerList;
        RefreshPlayerList();

        if (redTeamButton != null) redTeamButton.onClick.AddListener(() => RequestTeamChange(1));
        if (blueTeamButton != null) blueTeamButton.onClick.AddListener(() => RequestTeamChange(2));

        bool isHost = NetworkManager.Singleton.IsHost;

        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(isHost);
            startGameButton.onClick.AddListener(StartGame);
        }

        if (timeIncreaseButton != null) timeIncreaseButton.gameObject.SetActive(isHost);
        if (timeDecreaseButton != null) timeDecreaseButton.gameObject.SetActive(isHost);
        if (mapSizeIncreaseButton != null) mapSizeIncreaseButton.gameObject.SetActive(isHost);
        if (mapSizeDecreaseButton != null) mapSizeDecreaseButton.gameObject.SetActive(isHost);
        if (treasureIncreaseButton != null) treasureIncreaseButton.gameObject.SetActive(isHost);
        if (treasureDecreaseButton != null) treasureDecreaseButton.gameObject.SetActive(isHost);

        if (isHost)
        {
            if (timeIncreaseButton != null) timeIncreaseButton.onClick.AddListener(() => ChangeTime(30));
            if (timeDecreaseButton != null) timeDecreaseButton.onClick.AddListener(() => ChangeTime(-30));
            
            if (mapSizeIncreaseButton != null) mapSizeIncreaseButton.onClick.AddListener(() => ChangeMapSize(2));
            if (mapSizeDecreaseButton != null) mapSizeDecreaseButton.onClick.AddListener(() => ChangeMapSize(-2));

            if (treasureIncreaseButton != null) treasureIncreaseButton.onClick.AddListener(() => ChangeTreasureCount(2));
            if (treasureDecreaseButton != null) treasureDecreaseButton.onClick.AddListener(() => ChangeTreasureCount(-2));
        }
    }

    public override void OnDestroy()
    {
        NetworkPlayerState.OnPlayerStateChanged -= RefreshPlayerList;
    }

    private void ChangeTime(int amount)
    {
        if (IsServer) netGameTime.Value = Mathf.Clamp(netGameTime.Value + amount, 30, 600);
    }

    private void UpdateTimeUI()
    {
        if (timeDisplayText != null) timeDisplayText.text = $"{SelectedGameTime / 60}분 {SelectedGameTime % 60}초";
    }

    private void ChangeMapSize(int amount)
    {
        if (IsServer) netMapSize.Value = Mathf.Clamp(netMapSize.Value + amount, 4, 20);
    }

    private void UpdateMapSizeUI()
    {
        if (mapSizeDisplayText != null) mapSizeDisplayText.text = $"{SelectedMapSize} x {SelectedMapSize}";
    }

    private void ChangeTreasureCount(int amount)
    {
        if (IsServer) netTreasureCount.Value = Mathf.Clamp(netTreasureCount.Value + amount, 1, 50); 
    }

    private void UpdateTreasureUI()
    {
        if (treasureDisplayText != null) treasureDisplayText.text = $"보물 {SelectedTreasureCount}개";
    }

    void RequestTeamChange(int teamIndex)
    {
        if (NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            NetworkPlayerState myPlayerState = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<NetworkPlayerState>();
            if (myPlayerState != null) myPlayerState.SetTeamServerRpc(teamIndex);
        }
    }

    void RefreshPlayerList()
    {
        foreach (var slot in playerTextSlots)
        {
            if (slot != null) { slot.text = "빈 자리"; slot.color = Color.gray; }
        }

        NetworkPlayerState[] connectedPlayers = FindObjectsByType<NetworkPlayerState>(FindObjectsSortMode.None);
        
        // 모두가 팀을 골랐는지 체크하기 위한 지역 변수
        bool allPlayersSelectedTeam = true; 

        for (int i = 0; i < connectedPlayers.Length; i++)
        {
            if (i < playerTextSlots.Length && playerTextSlots[i] != null)
            {
                NetworkPlayerState player = connectedPlayers[i];
                string pName = player.Nickname.Value.ToString();
                if (string.IsNullOrEmpty(pName)) pName = "접속 중...";

                string teamPrefix = "";
                if (player.TeamIndex.Value == 1)
                {
                    teamPrefix = "<color=red>[레드]</color> ";
                }
                else if (player.TeamIndex.Value == 2)
                {
                    teamPrefix = "<color=#3399FF>[블루]</color> ";
                }
                else
                {
                    // 팀 인덱스가 0이라면 무소속 상태
                    allPlayersSelectedTeam = false; 
                }

                string finalText = teamPrefix + pName;

                if (player.OwnerClientId == NetworkManager.Singleton.LocalClientId)
                {
                    playerTextSlots[i].text = finalText + " (나)";
                    playerTextSlots[i].color = Color.green;
                }
                else if (player.OwnerClientId == NetworkManager.ServerClientId)
                {
                    playerTextSlots[i].text = finalText + " (방장)";
                    playerTextSlots[i].color = Color.yellow;
                }
                else
                {
                    playerTextSlots[i].text = finalText;
                    playerTextSlots[i].color = Color.white;
                }
            }
        }
        
        // 누군가 뒤늦게 팀을 선택해서 전원이 소속을 갖게 되었다면 경고 메시지 지우기
        if (allPlayersSelectedTeam && warningText != null)
        {
            warningText.text = "";
        }
    }

    void StartGame()
    {
        if (NetworkManager.Singleton.IsHost)
        {
            NetworkPlayerState[] connectedPlayers = FindObjectsByType<NetworkPlayerState>(FindObjectsSortMode.None);
            
            // --- 1. 팀 미선택 인원 검증 로직 ---
            foreach (var player in connectedPlayers)
            {
                if (player.TeamIndex.Value == 0) // 0은 무소속을 의미함
                {
                    Debug.LogWarning("팀을 선택하지 않은 플레이어가 있습니다! 게임 시작 취소.");
                    
                    if (warningText != null)
                    {
                        warningText.text = "모든 인원이 팀을 선택해야 시작할 수 있습니다!";
                        warningText.color = Color.red;
                    }
                    
                    return; // 여기서 함수를 탈출하여 씬 로드를 막습니다.
                }
            }

            // --- 2. 검증 통과 시 씬 로드 ---
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
}