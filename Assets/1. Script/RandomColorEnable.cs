using UnityEngine;

public class RandomColorEnable : MonoBehaviour
{
    [Tooltip("랜덤으로 선택할 머티리얼 목록")]
    public Material[] materials;

    private Renderer rend;

    private void Awake()
    {
        rend = GetComponent<Renderer>();
    }

    private void OnEnable()
    {
        if (materials == null || materials.Length == 0)
        {
            Debug.LogWarning("머티리얼 배열이 비어 있습니다.");
            return;
        }

        // 랜덤 머티리얼 선택
        Material chosen = materials[Random.Range(0, materials.Length)];

        // 머티리얼 교체 (인스턴스 생성)
        rend.material = new Material(chosen);
    }
}
