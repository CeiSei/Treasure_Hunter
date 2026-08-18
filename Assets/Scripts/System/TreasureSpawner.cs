using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class TreasureSpawner : NetworkBehaviour
{
    [Header("Treasure Settings")]
    [Tooltip("생성할 일반 보물 상자 프리팹 (NetworkManager에 등록 필수)")]
    [SerializeField] private GameObject treasurePrefab; 
    
    [Tooltip("생성할 꽝(미믹) 프리팹 (NetworkManager에 등록 필수)")]
    [SerializeField] private GameObject mimicPrefab; 

    [Tooltip("미믹이 등장할 확률 (0% ~ 100%)")]
    [Range(0f, 100f)]
    [SerializeField] private float mimicSpawnChance = 20f;

    [Tooltip("타일 프리팹 안에 심어둔 박스 스폰 포인트의 태그")]
    [SerializeField] private string spawnPointTag = "BoxSpawnPoint";

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            ModularMapGenerator.OnMapGenerated += SpawnTreasures;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            ModularMapGenerator.OnMapGenerated -= SpawnTreasures;
        }
    }

    private void SpawnTreasures()
    {
        List<Transform> spawnList = new List<Transform>();

        // 1. 맵의 최상단 부모 객체(ModularMap) 탐색 (비활성화된 청크 내부 포함)
        GameObject mapHolder = GameObject.Find("ModularMap");

        if (mapHolder != null)
        {
            Transform[] allTransforms = mapHolder.GetComponentsInChildren<Transform>(true);
            
            foreach (Transform t in allTransforms)
            {
                if (t.CompareTag(spawnPointTag))
                {
                    spawnList.Add(t);
                }
            }
        }
        else
        {
            GameObject[] activeObjects = GameObject.FindGameObjectsWithTag(spawnPointTag);
            foreach (var obj in activeObjects)
            {
                spawnList.Add(obj.transform);
            }
        }

        if (spawnList.Count == 0)
        {
            Debug.LogWarning("보물 상자 스폰 포인트를 찾을 수 없습니다! 타일 프리팹의 BoxSpawnPoint 태그를 확인하세요.");
            return;
        }

        // 2. 스폰 포인트 위치 섞기 (Fisher-Yates Shuffle)
        for (int i = 0; i < spawnList.Count; i++)
        {
            int randomIndex = Random.Range(i, spawnList.Count);
            Transform temp = spawnList[i];
            spawnList[i] = spawnList[randomIndex];
            spawnList[randomIndex] = temp;
        }

        // 3. 설정된 보물 개수 가져오기
        int targetTreasureCount = WaitingRoomUI.SelectedTreasureCount;
        int spawnCount = Mathf.Min(targetTreasureCount, spawnList.Count);

        int normalCount = 0;
        int mimicCount = 0;

        for (int i = 0; i < spawnCount; i++)
        {
            GameObject prefabToSpawn = treasurePrefab;

            if (mimicPrefab != null && Random.Range(0f, 100f) <= mimicSpawnChance)
            {
                prefabToSpawn = mimicPrefab;
                mimicCount++;
            }
            else
            {
                normalCount++;
            }

            if (prefabToSpawn != null)
            {
                // Y축을 기준으로 0 ~ 360도 사이의 랜덤 회전값 생성
                Quaternion randomYRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                
                // 기존 스폰 포인트의 회전값에 랜덤 Y축 회전값을 더함 (자연스러운 배치)
                Quaternion finalRotation = spawnList[i].rotation * randomYRotation;

                // 회전이 적용된 상태로 인스턴스화
                GameObject box = Instantiate(prefabToSpawn, spawnList[i].position, finalRotation);
                
                NetworkObject netObj = box.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn();
                }
            }
        }

        Debug.Log($"보물 상자 스폰 완료! (일반: {normalCount}개, 미믹: {mimicCount}개 / 전체 스폰포인트: {spawnList.Count}개 중)");
    }
}