using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System;
using Cysharp.Threading.Tasks;
using System.Threading;

public class ModularMapGenerator : NetworkBehaviour
{
    [Header("Tile Sizes")]
    [Tooltip("방(타일) 프리팹 하나의 실제 크기")]
    [SerializeField] private float tileSize = 10f;
    [Tooltip("다리(복도) 프리팹의 실제 길이")]
    [SerializeField] private float bridgeLength = 5f;

    [Header("Tile Prefabs")]
    [SerializeField] private GameObject borderTilePrefab;
    [SerializeField] private List<GameObject> innerTilePrefabs;
    [SerializeField] private GameObject bridgePrefab;

    [Header("Optimization")]
    [Tooltip("플레이어 주변으로 활성화할 청크 반경 (1이면 3x3, 2면 5x5)")]
    [SerializeField] private int viewDistance = 1;

    private NetworkVariable<int> mapSeed = new NetworkVariable<int>(0);
    private NetworkVariable<int> mapSize = new NetworkVariable<int>(0); 

    private bool isMapGenerated = false;
    
    // 청크들을 좌표별로 저장할 2차원 배열
    private GameObject[,] chunkGrid;
    private int currentMapSize;
    private float gridOffset;

    // 플레이어의 이전 청크 좌표 (불필요한 연산을 막기 위해 위치가 변했을 때만 갱신)
    private int lastPlayerChunkX = -1;
    private int lastPlayerChunkZ = -1;
    private CancellationTokenSource cts;

    public static event Action OnMapGenerated;
    public static bool IsMapReady { get; private set; } = false;
    public static int CurrentMapSeed { get; private set; } = 0;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            mapSize.Value = WaitingRoomUI.SelectedMapSize;
            mapSeed.Value = UnityEngine.Random.Range(1, 999999);
            
            CheckAndGenerateMap(0, 0);
        }
        else
        {
            mapSeed.OnValueChanged += CheckAndGenerateMap;
            mapSize.OnValueChanged += CheckAndGenerateMap;
            
            CheckAndGenerateMap(0, 0); 
        }
    }

    public override void OnNetworkDespawn()
    {
        IsMapReady = false; 
        CurrentMapSeed = 0;
        
        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    private void CheckAndGenerateMap(int previousValue, int newValue)
    {
        if (!isMapGenerated && mapSeed.Value != 0 && mapSize.Value != 0)
        {
            GenerateMap();
        }
    }

    private void GenerateMap()
    {
        isMapGenerated = true;

        currentMapSize = mapSize.Value;
        int seed = mapSeed.Value;
        CurrentMapSeed = seed;

        UnityEngine.Random.InitState(seed);

        Transform mapHolder = new GameObject("ModularMap").transform;
        gridOffset = tileSize + bridgeLength;
        
        // 맵 크기에 맞춰 청크 배열 초기화
        chunkGrid = new GameObject[currentMapSize, currentMapSize];

        for (int x = 0; x < currentMapSize; x++)
        {
            for (int z = 0; z < currentMapSize; z++)
            {
                // 각 그리드 좌표마다 청크를 담을 빈 껍데기(부모) 객체 생성
                GameObject chunkObj = new GameObject($"Chunk_{x}_{z}");
                chunkObj.transform.SetParent(mapHolder);
                chunkObj.transform.position = new Vector3(x * gridOffset, 0, z * gridOffset);
                
                // 1. 방(타일) 생성
                Vector3 tilePos = new Vector3(x * gridOffset, 0, z * gridOffset);
                GameObject tileToInstantiate = null;

                if (x == 0 || x == currentMapSize - 1 || z == 0 || z == currentMapSize - 1)
                {
                    tileToInstantiate = borderTilePrefab;
                }
                else
                {
                    if (innerTilePrefabs.Count > 0)
                    {
                        int randomIndex = UnityEngine.Random.Range(0, innerTilePrefabs.Count);
                        tileToInstantiate = innerTilePrefabs[randomIndex];
                    }
                }

                if (tileToInstantiate != null)
                {
                    GameObject tile = Instantiate(tileToInstantiate, tilePos, Quaternion.identity);
                    tile.transform.SetParent(chunkObj.transform); // 청크의 자식으로 넣음
                }

                // 2. 다리(복도) 생성
                if (x < currentMapSize - 1)
                {
                    Vector3 hBridgePos1 = new Vector3(x * gridOffset + (gridOffset / 2f), 0, z * gridOffset + 30);
                    Vector3 hBridgePos2 = new Vector3(x * gridOffset + (gridOffset / 2f), 0, z * gridOffset - 30);
                    if (bridgePrefab != null)
                    {
                        GameObject hBridge1 = Instantiate(bridgePrefab, hBridgePos1, Quaternion.identity);
                        hBridge1.transform.SetParent(mapHolder);

                        GameObject hBridge2 = Instantiate(bridgePrefab, hBridgePos2, Quaternion.identity);
                        hBridge2.transform.SetParent(mapHolder);
                    }
                }

                if (z < currentMapSize - 1)
                {
                    Vector3 vBridgePos1 = new Vector3(x * gridOffset + 30, 0, z * gridOffset + (gridOffset / 2f));
                    Vector3 vBridgePos2 = new Vector3(x * gridOffset - 30, 0, z * gridOffset + (gridOffset / 2f));
                    
                    if (bridgePrefab != null)
                    {
                        GameObject vBridge1 = Instantiate(bridgePrefab, vBridgePos1, Quaternion.Euler(0, 90, 0));
                        vBridge1.transform.SetParent(mapHolder);

                        GameObject vBridge2 = Instantiate(bridgePrefab, vBridgePos2, Quaternion.Euler(0, 90, 0));
                        vBridge2.transform.SetParent(mapHolder);
                    }
                }

                // 완성된 청크 묶음을 배열에 저장하고, 일단 모든 청크를 비활성화 (꺼둠)
                chunkObj.SetActive(false);
                chunkGrid[x, z] = chunkObj;
            }
        }

        Debug.Log($"동기화 맵 조립 완료 (크기: {currentMapSize}x{currentMapSize}, 시드: {seed})");

        WaitUntilLoad().Forget();

        // 맵 생성이 완료되면 플레이어의 위치를 추적하여 청크를 켜고 끄는 비동기 루프 시작
        cts = new CancellationTokenSource();
        UpdateChunksLoop(cts.Token).Forget();
    }

    async UniTaskVoid WaitUntilLoad()
    {
        await UniTask.WaitForSeconds(2f);
        IsMapReady = true;
        OnMapGenerated?.Invoke();
        await UniTask.Yield(PlayerLoopTiming.Update);
    }

    private async UniTaskVoid UpdateChunksLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            // 아직 내 로컬 캐릭터가 스폰되지 않았다면 대기
            if (NetworkManager.Singleton.LocalClient == null || NetworkManager.Singleton.LocalClient.PlayerObject == null)
            {
                await UniTask.WaitForSeconds(0.5f, cancellationToken: token);
                continue;
            }

            Vector3 playerPos = NetworkManager.Singleton.LocalClient.PlayerObject.transform.position;

            // 플레이어의 실제 월드 좌표를 그리드 좌표(청크 인덱스)로 변환
            int currentChunkX = Mathf.RoundToInt(playerPos.x / gridOffset);
            int currentChunkZ = Mathf.RoundToInt(playerPos.z / gridOffset);

            // 맵을 벗어난 값을 방지하기 위한 예외 처리
            currentChunkX = Mathf.Clamp(currentChunkX, 0, currentMapSize - 1);
            currentChunkZ = Mathf.Clamp(currentChunkZ, 0, currentMapSize - 1);

            // 플레이어가 새로운 청크 구역으로 넘어갔을 때만 갱신 (성능 최적화)
            if (currentChunkX != lastPlayerChunkX || currentChunkZ != lastPlayerChunkZ)
            {
                lastPlayerChunkX = currentChunkX;
                lastPlayerChunkZ = currentChunkZ;

                RefreshChunksVisibility(currentChunkX, currentChunkZ);
            }

            // 매 프레임 검사할 필요 없이 0.2초마다 갱신하여 최적화
            await UniTask.WaitForSeconds(0.2f, cancellationToken: token);
        }
    }

    private void RefreshChunksVisibility(int centerChunkX, int centerChunkZ)
    {
        for (int x = 0; x < currentMapSize; x++)
        {
            for (int z = 0; z < currentMapSize; z++)
            {
                // 현재 검사 중인 청크가 플레이어 중심 반경(viewDistance) 안에 있는지 확인
                bool isWithinDistance = Mathf.Abs(x - centerChunkX) <= viewDistance && 
                                        Mathf.Abs(z - centerChunkZ) <= viewDistance;

                // 상태가 다를 때만 SetActive를 호출하여 오버헤드 방지
                if (chunkGrid[x, z].activeSelf != isWithinDistance)
                {
                    chunkGrid[x, z].SetActive(isWithinDistance);
                }
            }
        }
    }
}