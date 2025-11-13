using UnityEngine;

public class RandomColorEnable : MonoBehaviour
{
    [Tooltip("랜덤으로 선택할 색 목록")]
    public Color[] colors;

    private Renderer rend;

    private void Awake()
    {
        rend = GetComponent<Renderer>();
    }

    private void OnEnable()
    {
        if (colors == null || colors.Length == 0)
        {
            Debug.LogWarning("색 배열이 비어 있습니다.");
            return;
        }

        // 랜덤 색 선택
        Color c = colors[Random.Range(0, colors.Length)];

        // 머터리얼 색 적용
        rend.material.color = c;
    }
}
