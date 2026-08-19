using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using System.Linq;
using System.IO;
using UnityEditor.AddressableAssets;

public static class Builder
{
    // ---------------------------------------------------------
    // 1. Windows 빌드 메서드
    // ---------------------------------------------------------
    public static void BuildWindowsAddressables()
    {
        SwitchPlatform(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
        AddressableAssetSettings.BuildPlayerContent();
    }

    public static void UpdateWindowsAddressables()
    {
        SwitchPlatform(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
        
        // Jenkinsfile에 작성했던 bin 파일의 유니티 내부 경로와 정확히 일치해야 합니다.
        string binPath = "Assets/AddressableAssetsData/Windows64/addressables_content_state.bin";
        UpdateAddressables(binPath);
    }

    public static void BuildWindowsClient()
    {
        SwitchPlatform(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
        
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = GetEnabledScenes(),
            locationPathName = "Builds/Windows/Game.exe", // 클라이언트 빌드 결과물이 저장될 경로
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        
        BuildPipeline.BuildPlayer(options);
    }

    // ---------------------------------------------------------
    // 2. Android 빌드 메서드
    // ---------------------------------------------------------
    public static void BuildAndroidAddressables()
    {
        SwitchPlatform(BuildTargetGroup.Android, BuildTarget.Android);
        AddressableAssetSettings.BuildPlayerContent();
    }

    public static void UpdateAndroidAddressables()
    {
        SwitchPlatform(BuildTargetGroup.Android, BuildTarget.Android);
        
        string binPath = "Assets/AddressableAssetsData/Android/addressables_content_state.bin";
        UpdateAddressables(binPath);
    }

    public static void BuildAndroidClient()
    {
        SwitchPlatform(BuildTargetGroup.Android, BuildTarget.Android);
        
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = GetEnabledScenes(),
            locationPathName = "Builds/Android/Game.apk", // AAB로 빌드하려면 Game.aab로 변경 및 설정 필요
            target = BuildTarget.Android,
            options = BuildOptions.None
        };
        
        BuildPipeline.BuildPlayer(options);
    }

    // ---------------------------------------------------------
    // 3. 공통 헬퍼 메서드
    // ---------------------------------------------------------
    
    // 플랫폼 전환 (배치 모드에서 타겟 플랫폼이 꼬이는 것을 방지)
    private static void SwitchPlatform(BuildTargetGroup group, BuildTarget target)
    {
        if (EditorUserBuildSettings.activeBuildTarget != target)
        {
            Debug.Log($"플랫폼을 {target}으로 변경합니다...");
            EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
        }
    }

    // Addressables Update Previous Build 실행
    private static void UpdateAddressables(string stateFilePath)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("AddressableAssetSettings를 찾을 수 없습니다.");
            EditorApplication.Exit(1);
        }

        if (!File.Exists(stateFilePath))
        {
            Debug.LogError($"Content state 파일을 찾을 수 없습니다: {stateFilePath}");
            EditorApplication.Exit(1);
        }

        Debug.Log($"다음 경로의 State 파일을 사용하여 업데이트 빌드를 시작합니다: {stateFilePath}");
        ContentUpdateScript.BuildContentUpdate(settings, stateFilePath);
    }

    // Build Settings에 등록되어 있고 활성화된 씬들의 경로만 가져오기
    private static string[] GetEnabledScenes()
    {
        return EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();
    }
}