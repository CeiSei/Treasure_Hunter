using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

public class LightDistanceCuller : MonoBehaviour
{
    [Tooltip("이 거리보다 멀어지면 빛을 끕니다.")]
    [SerializeField] private float cullDistance = 30f;
    
    [Tooltip("빛이 켜지거나 꺼질 때 걸리는 시간 (초)")]
    [SerializeField] private float fadeDuration = 1f;
    
    private Light[] myLights;
    private float[] originalIntensities; 
    private Transform mainCamera;
    
    private CancellationTokenSource cts;
    private CancellationTokenSource fadeCts; 
    
    private bool isCurrentlyEnabled = true;

    void Start()
    {
        myLights = GetComponentsInChildren<Light>();
        originalIntensities = new float[myLights.Length];
        
        for (int i = 0; i < myLights.Length; i++)
        {
            originalIntensities[i] = myLights[i].intensity;
        }

        if (Camera.main != null) 
        {
            mainCamera = Camera.main.transform;
        }
        
        // 시작 시 초기 거리 판별 및 밝기 설정
        if (mainCamera != null)
        {
             float sqrDistance = (transform.position - mainCamera.position).sqrMagnitude;
             isCurrentlyEnabled = sqrDistance <= (cullDistance * cullDistance);
             for (int i = 0; i < myLights.Length; i++)
             {
                 myLights[i].enabled = isCurrentlyEnabled;
                 myLights[i].intensity = isCurrentlyEnabled ? originalIntensities[i] : 0f;
             }
        }

        cts = new CancellationTokenSource();
        CheckDistanceLoop(cts.Token).Forget();
    }

    private async UniTaskVoid CheckDistanceLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (mainCamera != null)
            {
                float sqrDistance = (transform.position - mainCamera.position).sqrMagnitude;
                bool shouldBeEnabled = sqrDistance <= (cullDistance * cullDistance);

                // 상태가 변했을 때만 페이드 효과 실행
                if (isCurrentlyEnabled != shouldBeEnabled)
                {
                    isCurrentlyEnabled = shouldBeEnabled;
                    
                    if (fadeCts != null)
                    {
                        fadeCts.Cancel();
                        fadeCts.Dispose();
                    }
                    fadeCts = new CancellationTokenSource();
                    FadeLights(shouldBeEnabled, fadeCts.Token).Forget();
                }
            }
            
            await UniTask.WaitForSeconds(0.5f, cancellationToken: token);
        }
    }

    private async UniTaskVoid FadeLights(bool turnOn, CancellationToken token)
    {
        if (turnOn)
        {
            for (int i = 0; i < myLights.Length; i++)
            {
                myLights[i].enabled = true;
            }
        }

        float elapsedTime = 0f;
        
        // 현재 밝기를 시작점으로 잡음 (경계선을 빠르게 왔다 갔다 할 때를 대비)
        float[] startIntensities = new float[myLights.Length];
        for (int i = 0; i < myLights.Length; i++)
        {
            startIntensities[i] = myLights[i].intensity;
        }

        while (elapsedTime < fadeDuration)
        {
            if (token.IsCancellationRequested) return;

            elapsedTime += Time.deltaTime;
            float t = elapsedTime / fadeDuration;

            for (int i = 0; i < myLights.Length; i++)
            {
                float targetIntensity = turnOn ? originalIntensities[i] : 0f;
                myLights[i].intensity = Mathf.Lerp(startIntensities[i], targetIntensity, t);
            }

            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        if (!token.IsCancellationRequested)
        {
            for (int i = 0; i < myLights.Length; i++)
            {
                myLights[i].intensity = turnOn ? originalIntensities[i] : 0f;
                
                // 완전히 꺼진 이후에 렌더링 최적화를 위해 비활성화 처리
                if (!turnOn)
                {
                    myLights[i].enabled = false;
                }
            }
        }
    }

    void OnDestroy()
    {
        if (cts != null) 
        {
            cts.Cancel();
            cts.Dispose();
        }
        if (fadeCts != null)
        {
            fadeCts.Cancel();
            fadeCts.Dispose();
        }
    }
}