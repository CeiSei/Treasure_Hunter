using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class IntroResourceManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI sizeText;
    
    [Header("Scene Transition")]
    [Tooltip("패치가 끝난 후 이동할 씬의 이름")]
    [SerializeField] private string nextSceneName = "WaitingRoom";

    [Header("Addressables Settings")]
    [Tooltip("다운로드할 에셋들의 라벨 (예: Preload, Character 등)")]
    [SerializeField] private List<string> preloadLabels = new List<string> { "Preload", "Character" };

    void Awake()
    {
    #if UNITY_ANDROID
        // 모바일 환경에서 목표 프레임을 강제하기 위해 수직 동기화(VSync)를 먼저 끕니다.
        QualitySettings.vSyncCount = 0; 
        
        // 프레임을 60으로 고정합니다.
        Application.targetFrameRate = 60; 
    #endif
    }

    void Start()
    {
        if (progressBar != null) progressBar.value = 0f;
        UpdateStatus("초기화 중...");
        
        CheckAndDownloadUpdates().Forget();
    }

    private async UniTaskVoid CheckAndDownloadUpdates()
    {
        // 1. Addressables 초기화
        await Addressables.InitializeAsync().ToUniTask();

        // 2. 카탈로그(에셋 목록) 업데이트 확인
        UpdateStatus("업데이트 확인 중...");
        var checkHandle = Addressables.CheckForCatalogUpdates(false);
        var catalogsToUpdate = await checkHandle.ToUniTask();

        if (catalogsToUpdate.Count > 0)
        {
            UpdateStatus("카탈로그 업데이트 중...");
            await Addressables.UpdateCatalogs(catalogsToUpdate, false).ToUniTask();
        }
        Addressables.Release(checkHandle);

        // 3. 다운로드 용량 계산
        UpdateStatus("다운로드 용량 계산 중...");
        long totalDownloadSize = 0;

        foreach (var label in preloadLabels)
        {
            var sizeHandle = Addressables.GetDownloadSizeAsync(label);
            totalDownloadSize += await sizeHandle.ToUniTask();
            Addressables.Release(sizeHandle);
        }

        if (totalDownloadSize > 0)
        {
            if (sizeText != null)
                sizeText.text = $"다운로드 크기: {totalDownloadSize / (1024 * 1024)} MB";

            UpdateStatus("리소스 다운로드 중...");

            // 4. 리소스 다운로드 및 프로그레스 바 갱신
            foreach (var label in preloadLabels)
            {
                var downloadHandle = Addressables.DownloadDependenciesAsync(label);

                while (!downloadHandle.IsDone)
                {
                    if (progressBar != null)
                        progressBar.value = downloadHandle.PercentComplete;
                    
                    if (statusText != null)
                        statusText.text = $"리소스 다운로드 중... ({(int)(downloadHandle.PercentComplete * 100)}%)";

                    await UniTask.Yield();
                }
                Addressables.Release(downloadHandle);
            }
        }

        // 5. 다운로드 완료 및 씬 이동
        UpdateStatus("패치 완료! 게임에 접속합니다.");
        if (progressBar != null) progressBar.value = 1f;

        await UniTask.WaitForSeconds(1f); // 유저가 완료 상태를 읽을 수 있도록 1초 대기
        
        SceneManager.LoadScene(nextSceneName);
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        Debug.Log(message);
    }
}