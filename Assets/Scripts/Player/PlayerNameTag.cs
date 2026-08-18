using UnityEngine;
using TMPro;

public class PlayerNameTag : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    private Transform mainCameraTransform;

    void Start()
    {
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
    }

    // 팀 인덱스(teamIndex)를 추가로 받습니다.
    public void SetName(string name, int teamIndex = 0)
    {
        if (nameText != null)
        {
            nameText.text = name;

            // 팀에 따른 글씨 색상 변경
            if (teamIndex == 1) nameText.color = Color.red;
            else if (teamIndex == 2) nameText.color = new Color(0.2f, 0.6f, 1f); // 블루팀 (하늘색 계열)
            else nameText.color = Color.white; // 무소속
        }
    }

    void LateUpdate()
    {
        if (mainCameraTransform == null)
        {
            if (Camera.main != null) mainCameraTransform = Camera.main.transform;
            return;
        }

        transform.LookAt(transform.position + mainCameraTransform.rotation * Vector3.forward,
                         mainCameraTransform.rotation * Vector3.up);
    }
}