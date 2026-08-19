using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CinemachineInputAxisController))]
public class CinemachineInputFix : MonoBehaviour
{
    void Start()
    {
        var axisController = GetComponent<CinemachineInputAxisController>();
        
        if (PlayerInputRouter.Instance != null && axisController != null)
        {
            // 1. PlayerInputRouter에 정의된 Look 액션을 가져와서 런타임 참조(Reference)로 변환합니다.
            InputActionReference lookRef = InputActionReference.Create(PlayerInputRouter.Instance.GetLookAction());

            // 2. 시네머신 컨트롤러의 모든 축(Pan, Tilt 등)에 Look 입력을 강제로 덮어씌웁니다.
            for (int i = 0; i < axisController.Controllers.Count; i++)
            {
                var reader = axisController.Controllers[i];
                
                // [핵심 수정 부분] Cinemachine 최신 릴리즈 버전에 맞추어 변수명을 Input으로 수정했습니다.
                reader.Input.InputAction = lookRef; 
                
                axisController.Controllers[i] = reader; 
            }

#if UNITY_ANDROID
            axisController.Controllers[0].Input.Gain = 200f;
            axisController.Controllers[1].Input.Gain = -100f;

#else
            axisController.Controllers[0].Input.Gain = 1f;
            axisController.Controllers[1].Input.Gain = -1f;
#endif

            Debug.Log("시네머신 카메라 입력이 PlayerInputRouter의 Look으로 덮어씌워졌습니다. (CM Default 방지)");
        }
    }
}