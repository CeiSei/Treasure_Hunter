using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Cysharp.Threading.Tasks;
using System;
using TMPro;

public class RelayManager : MonoBehaviour
{
    public static RelayManager Instance { get; private set; }

    public static string CurrentJoinCode { get; private set; } = "";
    // 로컬 플레이어의 닉네임을 저장해둘 전역 변수
    public static string LocalNickname { get; private set; } = "";

    [Header("Scene Management")]
    [SerializeField] private string waitingRoomSceneName = "WaitingRoomScene"; 

    [Header("UI References")]
    [SerializeField] private TMP_InputField joinCodeInputField;
    [SerializeField] private TMP_InputField nicknameInputField; // 닉네임 입력 필드 추가
    
    [Header("Error UI")]
    [SerializeField] private GameObject errorUIPanel;
    [SerializeField] private TextMeshProUGUI errorText;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    async void Start()
    {
        if (errorUIPanel != null) errorUIPanel.SetActive(false);
        await AuthenticateUserAsync();
    }

    private async UniTask AuthenticateUserAsync()
    {
        try
        {
            await UnityServices.InitializeAsync();
            
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"유니티 서비스 로그인 성공! 플레이어 ID: {AuthenticationService.Instance.PlayerId}");
            }
        }
        catch (Exception e)
        {
            ShowErrorUI($"유니티 서비스 연결 실패\n{e.Message}");
        }
    }

    // ==========================================
    // [호스트(방장) 로직]
    // ==========================================
    public void StartRelayHost()
    {
        if (NetworkManager.Singleton == null)
        {
            ShowErrorUI("NetworkManager를 찾을 수 없습니다.");
            return;
        }

        if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsConnectedClient)
        {
            ShowErrorUI("이미 네트워크에 연결되어 있습니다.");
            return;
        }

        // 닉네임 입력 확인 로직
        if (string.IsNullOrEmpty(nicknameInputField.text))
        {
            ShowErrorUI("닉네임을 입력해주세요!");
            return;
        }
        
        LocalNickname = nicknameInputField.text.Trim();
        CreateRelayHostAsync().Forget();
    }

    private async UniTaskVoid CreateRelayHostAsync()
    {
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);
            CurrentJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            
            Debug.Log($"릴레이 방 생성 성공! 참가 코드: {CurrentJoinCode}");

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(
                allocation.RelayServer.IpV4, 
                (ushort)allocation.RelayServer.Port, 
                allocation.AllocationIdBytes, 
                allocation.Key, 
                allocation.ConnectionData
            );

            NetworkManager.Singleton.StartHost();
            NetworkManager.Singleton.SceneManager.LoadScene(waitingRoomSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        catch (RelayServiceException e)
        {
            ShowErrorUI($"방 생성 실패\n오류: {e.Reason}");
        }
    }

    // ==========================================
    // [클라이언트(참가자) 로직]
    // ==========================================
    public void JoinRelayClient()
    {
        if (NetworkManager.Singleton == null)
        {
            ShowErrorUI("NetworkManager를 찾을 수 없습니다.");
            return;
        }

        if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsConnectedClient)
        {
            ShowErrorUI("이미 네트워크에 접속 중입니다.");
            return;
        }

        // 닉네임 입력 확인 로직
        if (string.IsNullOrEmpty(nicknameInputField.text))
        {
            ShowErrorUI("닉네임을 입력해주세요!");
            return;
        }

        if (string.IsNullOrEmpty(joinCodeInputField.text))
        {
            ShowErrorUI("참가 코드를 입력해주세요!");
            return;
        }

        LocalNickname = nicknameInputField.text.Trim();
        string cleanJoinCode = joinCodeInputField.text.Trim().ToUpper();
        JoinRelayClientAsync(cleanJoinCode).Forget();
    }

    private async UniTaskVoid JoinRelayClientAsync(string joinCode)
    {
        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(
                joinAllocation.RelayServer.IpV4, 
                (ushort)joinAllocation.RelayServer.Port, 
                joinAllocation.AllocationIdBytes, 
                joinAllocation.Key, 
                joinAllocation.ConnectionData, 
                joinAllocation.HostConnectionData
            );

            CurrentJoinCode = joinCode;
            NetworkManager.Singleton.StartClient();
        }
        catch (RelayServiceException e)
        {
            ShowErrorUI($"접속 실패\n코드를 확인해주세요. (원인: {e.Reason})");
        }
    }

    private void ShowErrorUI(string message)
    {
        Debug.LogWarning(message);
        if (errorUIPanel != null && errorText != null)
        {
            errorText.text = message;
            errorUIPanel.SetActive(true);
        }
    }

    public void CloseErrorUI()
    {
        if (errorUIPanel != null)
        {
            errorUIPanel.SetActive(false);
        }
    }
}