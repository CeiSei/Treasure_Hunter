using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;

public class PlayerStatusUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Horizontal Layout Group이 부착된 빈 게임 오브젝트")]
    [SerializeField] private Transform iconContainer; 
    
    [Tooltip("StatusEffectIconUI가 부착된 아이콘 프리팹")]
    [SerializeField] private GameObject iconPrefab;

    private PlayerControl localPlayer;
    
    // UI 게임오브젝트와 Addressables 비동기 핸들을 추적하기 위한 딕셔너리
    private Dictionary<StatusEffect, GameObject> activeIcons = new Dictionary<StatusEffect, GameObject>();
    private Dictionary<StatusEffect, AsyncOperationHandle<Sprite>> activeHandles = new Dictionary<StatusEffect, AsyncOperationHandle<Sprite>>();

    void Update()
    {
        if (localPlayer == null && NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
        {
            if (NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerControl>();
                if (localPlayer != null)
                {
                    localPlayer.OnStatusEffectAdded += HandleEffectAdded;
                    localPlayer.OnStatusEffectRemoved += HandleEffectRemoved;
                }
            }
        }
    }

    private async void HandleEffectAdded(StatusEffect effect)
    {
        // 1. 아이콘 프리팹을 즉시 생성하여 위치를 선점합니다 (로딩 딜레이 방지)
        GameObject newIcon = Instantiate(iconPrefab, iconContainer);
        StatusEffectIconUI iconScript = newIcon.GetComponent<StatusEffectIconUI>();
        activeIcons.Add(effect, newIcon);

        // 2. StatType 기반으로 Addressables 키 동적 생성 (예: "Icon_MoveSpeed")
        string addressableKey = $"Icon_{effect.TargetStat}";

        try
        {
            // 3. S3 또는 로컬에서 스프라이트 비동기 로드
            var handle = Addressables.LoadAssetAsync<Sprite>(addressableKey);
            activeHandles.Add(effect, handle);

            Sprite targetSprite = await handle.ToUniTask();

            // 4. 로드 완료 후 UI 갱신 (로딩 도중 디버프가 끝나 이미 파괴되었을 수 있으므로 널 체크 필수)
            if (iconScript != null && newIcon != null)
            {
                iconScript.Setup(effect, targetSprite);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[{addressableKey}] Addressables 이미지를 로드할 수 없습니다: {e.Message}");
        }
    }

    private void HandleEffectRemoved(StatusEffect effect)
    {
        // 1. UI 게임오브젝트 파괴
        if (activeIcons.TryGetValue(effect, out GameObject iconObj))
        {
            if (iconObj != null) Destroy(iconObj);
            activeIcons.Remove(effect);
        }

        // 2. Addressables 메모리 해제 (Memory Leak 방지)
        if (activeHandles.TryGetValue(effect, out AsyncOperationHandle<Sprite> handle))
        {
            Addressables.Release(handle);
            activeHandles.Remove(effect);
        }
    }

    void OnDestroy()
    {
        if (localPlayer != null)
        {
            localPlayer.OnStatusEffectAdded -= HandleEffectAdded;
            localPlayer.OnStatusEffectRemoved -= HandleEffectRemoved;
        }

        // 씬 전환 시 남아있는 모든 Addressables 핸들 강제 해제
        foreach (var handle in activeHandles.Values)
        {
            Addressables.Release(handle);
        }
        activeHandles.Clear();
    }
}