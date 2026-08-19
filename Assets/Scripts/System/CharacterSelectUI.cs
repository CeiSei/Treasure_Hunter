using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using Unity.Netcode;

public class CharacterSelectUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("캐릭터 버튼들이 생성될 부모 Transform (ScrollRect의 Content 등)")]
    [SerializeField] private Transform characterListContent;
    
    [Tooltip("생성할 캐릭터 버튼 UI 프리팹")]
    [SerializeField] private GameObject characterButtonPrefab;
    
    [Tooltip("현재 선택된 캐릭터 이름을 표시할 텍스트")]
    [SerializeField] private TextMeshProUGUI selectedCharacterText;

    [Header("Addressables")]
    [Tooltip("캐릭터 프리팹들에 부여된 Addressables 라벨")]
    [SerializeField] private string characterLabel = "Character";

    public static string SelectedCharacterKey { get; private set; } = "";

    // 서버에서 프리팹을 즉시 꺼내어 스폰할 수 있도록 딕셔너리에 보관
    public static Dictionary<string, GameObject> LoadedCharacterPrefabs = new Dictionary<string, GameObject>();

    void Start()
    {
        if (selectedCharacterText != null) 
            selectedCharacterText.text = "캐릭터를 선택하세요";

        LoadCharacterList().Forget();
    }

    private async UniTaskVoid LoadCharacterList()
    {
        // 1. 'Character' 라벨이 붙은 모든 에셋 위치 정보를 가져옵니다.
        var locationsHandle = Addressables.LoadResourceLocationsAsync(characterLabel);
        var locations = await locationsHandle.ToUniTask();

        if (locations.Count == 0)
        {
            Debug.LogWarning("Addressables에서 캐릭터 리소스를 찾을 수 없습니다.");
            return;
        }

        foreach (var location in locations)
        {
            string characterKey = location.PrimaryKey;

            // 2. 위치 정보뿐만 아니라 실제 프리팹 에셋을 로드합니다.
            var assetHandle = Addressables.LoadAssetAsync<GameObject>(location);
            GameObject prefab = await assetHandle.ToUniTask();

            // 3. NGO NetworkManager에 런타임 동적 등록 (이름 및 참조 기반 중복 검사)
            if (NetworkManager.Singleton != null)
            {
                NetworkObject netObj = prefab.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    bool isAlreadyRegistered = false;
                    
                    // 현재 NetworkManager에 등록된 프리팹들과 이름/참조를 비교하여 중복을 차단합니다.
                    foreach (var networkPrefab in NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs)
                    {
                        if (networkPrefab.Prefab != null)
                        {
                            // 인스턴스가 완전히 동일하거나, 프리팹의 이름이 같으면 중복으로 간주합니다.
                            if (networkPrefab.Prefab == prefab || networkPrefab.Prefab.name == prefab.name)
                            {
                                isAlreadyRegistered = true;
                                break;
                            }
                        }
                    }

                    if (!isAlreadyRegistered)
                    {
                        NetworkManager.Singleton.AddNetworkPrefab(prefab);
                    }
                }
            }

            // 4. 스폰을 위해 딕셔너리에 캐싱
            LoadedCharacterPrefabs[characterKey] = prefab;

            // 5. UI 버튼 생성
            GameObject btnObj = Instantiate(characterButtonPrefab, characterListContent);
            Button btn = btnObj.GetComponent<Button>();
            TextMeshProUGUI btnText = btnObj.GetComponentInChildren<TextMeshProUGUI>();

            if (btnText != null)
            {
                string displayName = characterKey;
                int slashIndex = displayName.LastIndexOf('/');
                if (slashIndex >= 0) displayName = displayName.Substring(slashIndex + 1);
                displayName = displayName.Replace(".prefab", "");
                
                btnText.text = displayName;
            }

            // 버튼 클릭 이벤트 리스너 등록
            btn.onClick.AddListener(() => OnCharacterSelected(characterKey, btnText.text));
        }

        Addressables.Release(locationsHandle);
    }

    private void OnCharacterSelected(string addressableKey, string displayName)
    {
        SelectedCharacterKey = addressableKey;

        if (selectedCharacterText != null)
        {
            selectedCharacterText.text = $"현재 조작 중: <color=green>{displayName}</color>";
        }

        // 6. 내 로컬 플레이어 객체를 찾아서 캐릭터 교체(서버 RPC) 요청
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            PlayerCharacterSwapper swapper = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerCharacterSwapper>();
            if (swapper != null)
            {
                swapper.RequestCharacterSwapServerRpc(addressableKey);
            }
        }
    }
}