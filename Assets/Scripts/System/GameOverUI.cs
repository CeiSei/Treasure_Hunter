using UnityEngine;
using TMPro;
using Unity.Netcode;
using UnityEngine.UI;

public class GameOverUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject resultPanel; 
    [SerializeField] private TextMeshProUGUI resultText; 

    [Header("Navigation Buttons")]
    [SerializeField] private Button returnToWaitingRoomBtn; // 대기방으로 돌아가기 (방장 전용)
    [SerializeField] private Button returnToMainMenuBtn; // 메인 화면으로 돌아가기 (전체 공통)
    
    [Header("Scene Names")]
    [SerializeField] private string waitingRoomSceneName = "WaitingRoomScene"; 
    [SerializeField] private string mainMenuSceneName = "MainMenuScene"; // 처음 접속했던 씬 이름

    void Start()
    {
        if (resultPanel != null) resultPanel.SetActive(false);

        InGameManager.OnGameOver += HandleGameOver;

        // 대기방으로 돌아가기 버튼 설정 (방장만 보이게 처리)
        if (returnToWaitingRoomBtn != null)
        {
            returnToWaitingRoomBtn.gameObject.SetActive(NetworkManager.Singleton.IsHost);
            returnToWaitingRoomBtn.onClick.AddListener(ReturnToWaitingRoom);
        }

        // 메인 화면으로 돌아가기 버튼 설정
        if (returnToMainMenuBtn != null)
        {
            returnToMainMenuBtn.onClick.AddListener(ReturnToMainMenu);
        }
    }

    void OnDestroy()
    {
        InGameManager.OnGameOver -= HandleGameOver;
    }

    private void HandleGameOver(int winningTeam)
    {
        resultPanel.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        int myTeam = 0;
        if (NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            NetworkPlayerState myState = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<NetworkPlayerState>();
            if (myState != null) myTeam = myState.TeamIndex.Value;
        }

        if (winningTeam == 0)
        {
            resultText.text = "무승부!";
            resultText.color = Color.white;
        }
        else if (winningTeam == myTeam)
        {
            resultText.text = "승리!";
            resultText.color = Color.green;
        }
        else
        {
            resultText.text = "패배...";
            resultText.color = Color.red; 
        }
    }

    // --- [내비게이션 로직] ---

    private void ReturnToWaitingRoom()
    {
        // 방장이 버튼을 누르면 모두를 데리고 대기방으로 이동
        if (NetworkManager.Singleton.IsHost)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(waitingRoomSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    private void ReturnToMainMenu()
    {
        // 1. 네트워크 연결을 완전히 끊음
        NetworkManager.Singleton.Shutdown();

        // 2. NetworkManager가 다음 접속 때 충돌하지 않도록 씬에 남아있는 객체 파괴
        if (NetworkManager.Singleton != null)
        {
            Destroy(NetworkManager.Singleton.gameObject);
        }

        // 3. 로컬 씬 매니저를 이용해 메인 화면으로 이동
        UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
    }
}