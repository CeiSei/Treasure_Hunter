using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;

public class NetworkUIManager : MonoBehaviour
{
    [Header("Network UI Buttons")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button serverButton;

    void Start()
    {
        // Host: 서버와 클라이언트를 동시에 실행 (내가 방장이 되어 직접 플레이함)
        hostButton.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartHost();
            HideUI();
        });

        // Client: 이미 열려있는 서버(방)에 플레이어로서 접속
        clientButton.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartClient();
            HideUI();
        });

        // Server: 플레이어 캐릭터 없이 오직 게임 연산만 담당하는 전용(Dedicated) 서버 실행
        serverButton.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartServer();
            HideUI();
        });
    }

    void HideUI()
    {
        // 접속에 성공하면 버튼 UI를 가려줍니다.
        gameObject.SetActive(false);
    }
}