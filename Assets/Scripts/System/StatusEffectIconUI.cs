using UnityEngine;
using UnityEngine.UI;

public class StatusEffectIconUI : MonoBehaviour
{
    [Tooltip("상태 이상을 나타낼 메인 아이콘")]
    [SerializeField] private Image iconImage;
    
    [Tooltip("남은 시간을 표시할 반투명한 검은색 게이지 (Image Type: Filled)")]
    [SerializeField] private Image cooldownOverlay;

    private StatusEffect targetEffect;

    // 초기 설정
    public void Setup(StatusEffect effect, Sprite iconSprite)
    {
        targetEffect = effect;
        if (iconImage != null && iconSprite != null)
        {
            iconImage.sprite = iconSprite;
        }
    }

    void Update()
    {
        if (targetEffect != null && cooldownOverlay != null)
        {
            // 경과 시간 및 남은 시간 계산
            float elapsed = Time.time - targetEffect.StartTime;
            float remaining = targetEffect.Duration - elapsed;

            // 남은 비율에 따라 Fill Amount 조절 (시계 방향으로 줄어드는 연출)
            cooldownOverlay.fillAmount = remaining / targetEffect.Duration;
        }
    }
}