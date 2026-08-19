using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class PlayerCharacterSwapper : NetworkBehaviour
{
    [ServerRpc]
    public void RequestCharacterSwapServerRpc(string characterKey, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        // 1. UI에서 로드해둔 딕셔너리에서 요청받은 캐릭터 프리팹 꺼내기
        if (CharacterSelectUI.LoadedCharacterPrefabs.TryGetValue(characterKey, out GameObject newPrefab))
        {
            NetworkObject oldNetObj = GetComponent<NetworkObject>();
            
            // 2. 현재 상태(위치, 회전, 대기방에서 고른 팀 인덱스 등) 저장
            Vector3 currentPos = transform.position;
            Quaternion currentRot = transform.rotation;
            
            int currentTeam = 0;
            NetworkPlayerState oldState = GetComponent<NetworkPlayerState>();
            if (oldState != null)
            {
                currentTeam = oldState.TeamIndex.Value;
            }

            // 3. 새 캐릭터 프리팹 인스턴스화
            GameObject newCharacterInstance = Instantiate(newPrefab, currentPos, currentRot);
            NetworkObject newNetObj = newCharacterInstance.GetComponent<NetworkObject>();

            // 4. 이전 상태 복구 (팀 유지)
            NetworkPlayerState newState = newCharacterInstance.GetComponent<NetworkPlayerState>();
            if (newState != null)
            {
                newState.TeamIndex.Value = currentTeam;
            }

            // 5. 새 캐릭터를 해당 클라이언트의 플레이어 객체로 스폰 및 권한 이양
            // [핵심 수정] 두 번째 파라미터(destroyWithScene)를 false로 설정하여 씬 전환 시 파괴되지 않도록 합니다.
            newNetObj.SpawnAsPlayerObject(clientId, false);

            // 6. 기존 플레이어 객체 파괴
            oldNetObj.Despawn(true);
            
            Debug.Log($"클라이언트 {clientId}의 캐릭터가 {characterKey}로 성공적으로 교체되었습니다.");
        }
        else
        {
            Debug.LogError($"서버 딕셔너리에서 {characterKey} 프리팹을 찾을 수 없습니다.");
        }
    }
}