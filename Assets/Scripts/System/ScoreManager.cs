using TMPro;
using UnityEngine;
using Unity.Netcode;

public class ScoreManager : NetworkBehaviour
{
    // 접근을 쉽게 하기 위한 싱글톤 패턴
    public static ScoreManager Instance { get; private set; }
    
    public int TotalTeamSize;

    // 네트워크를 통해 자동으로 동기화되는 점수 리스트
    private NetworkList<int> TeamScore;

    [Header("Temp UI")]
    public TextMeshProUGUI[] TeamScoreText;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // NetworkList는 Awake에서 메모리를 할당해야 합니다.
        TeamScore = new NetworkList<int>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // 서버에서만 초기 팀 점수를 0으로 세팅합니다.
            for(int i = 0; i <= TotalTeamSize; i++)
            {
                TeamScore.Add(0);
            }
        }

        // 값이 변할 때마다 호출될 이벤트를 구독하여 UI를 갱신합니다.
        TeamScore.OnListChanged += OnScoreChanged;
        RefreshScoreUI();
    }

    public override void OnNetworkDespawn()
    {
        TeamScore.OnListChanged -= OnScoreChanged;
    }

    // 서버 사이드(BoxScript)에서 직접 호출하여 점수를 올리는 함수
    public void AddTeamScoreServer(int team, int score)
    {
        if (!IsServer) return;

        if (team >= 0 && team < TeamScore.Count)
        {
            TeamScore[team] += score;
        }
    }

    void OnScoreChanged(NetworkListEvent<int> changeEvent)
    {
        RefreshScoreUI();
    }

    void RefreshScoreUI()
    {
        for(int i = 0; i < TeamScore.Count; i++)
        {
            if (i < TeamScoreText.Length)
            {
                TeamScoreText[i].text = TeamScore[i].ToString();
            }
        }
    }
}