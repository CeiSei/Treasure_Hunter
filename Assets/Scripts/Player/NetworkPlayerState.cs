using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using System;

public class NetworkPlayerState : NetworkBehaviour
{
    // 닉네임 동기화 변수
    public NetworkVariable<FixedString32Bytes> Nickname = new NetworkVariable<FixedString32Bytes>(
        "",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // 팀 동기화 변수 (0: 무소속, 1: 레드팀, 2: 블루팀)
    public NetworkVariable<int> TeamIndex = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public static event Action OnPlayerStateChanged;

    [Header("UI References")]
    [SerializeField] private PlayerNameTag nameTag;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            SetNicknameServerRpc(RelayManager.LocalNickname);
        }

        // 닉네임이나 팀이 변경될 때마다 UI 갱신
        Nickname.OnValueChanged += (oldValue, newValue) => { UpdateState(); };
        TeamIndex.OnValueChanged += (oldValue, newValue) => { UpdateState(); };

        UpdateState();
    }

    public override void OnNetworkDespawn()
    {
        OnPlayerStateChanged?.Invoke();
    }

    private void UpdateState()
    {
        OnPlayerStateChanged?.Invoke();
        UpdateNameTag();
    }

    [ServerRpc]
    private void SetNicknameServerRpc(string name)
    {
        Nickname.Value = name;
    }

    // 외부 UI에서 호출할 팀 변경 요청 함수
    [ServerRpc]
    public void SetTeamServerRpc(int teamIndex)
    {
        TeamIndex.Value = teamIndex;
    }

    private void UpdateNameTag()
    {
        if (nameTag != null)
        {
            string pName = Nickname.Value.ToString();
            if (string.IsNullOrEmpty(pName)) pName = "접속 중...";
            
            // 네임태그 스크립트로 닉네임과 팀 번호를 함께 전달
            nameTag.SetName(pName, TeamIndex.Value);
        }
    }
}