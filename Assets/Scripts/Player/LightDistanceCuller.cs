using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

public class LightDistanceCuller : MonoBehaviour
{
    [Tooltip("이 거리보다 멀어지면 빛을 끕니다.")]
    [SerializeField] private float cullDistance = 30f;
    
    private Light[] myLights;
    private Transform mainCamera;
    private CancellationTokenSource cts;

    void Start()
    {
        myLights = GetComponentsInChildren<Light>();
        if (Camera.main != null) 
        {
            mainCamera = Camera.main.transform;
        }
        
        cts = new CancellationTokenSource();
        CheckDistanceLoop(cts.Token).Forget();
    }

    private async UniTaskVoid CheckDistanceLoop(CancellationToken token)
    {
        // 매 프레임 거리를 재면 오히려 CPU 부하가 생기므로 0.5초(또는 1초) 단위로 체크합니다.
        while (!token.IsCancellationRequested)
        {
            if (mainCamera != null)
            {
                float sqrDistance = (transform.position - mainCamera.position).sqrMagnitude;
                
                // Vector3.Distance 대신 sqrMagnitude를 쓰면 내부적으로 제곱근 연산을 생략해 연산이 훨씬 빠릅니다.
                bool shouldBeEnabled = sqrDistance <= (cullDistance * cullDistance);

                for(int i = 0; i < myLights.Length; i++)
                {
                    if (myLights[i].enabled != shouldBeEnabled)
                    {
                        myLights[i].enabled = shouldBeEnabled;
                    }
                }
            }
            
            await UniTask.WaitForSeconds(0.5f, cancellationToken: token);
        }
    }

    void OnDestroy()
    {
        if (cts != null) 
        {
            cts.Cancel();
            cts.Dispose();
        }
    }
}