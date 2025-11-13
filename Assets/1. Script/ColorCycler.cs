using UnityEngine;

public class ColorCycler : MonoBehaviour
{
    [Tooltip("순서대로 적용할 머티리얼 리스트")]
    public Material[] materials;

    [Tooltip("재질이 바뀌는 시간 간격 (초)")]
    public float changeInterval = 1f;

    private Renderer rend;
    private int currentIndex = 0;
    private float timer = 0f;

    private void Awake()
    {
        rend = GetComponent<Renderer>();
    }

    private void OnEnable()
    {
        timer = 0f;
        currentIndex = 0;

        if (materials != null && materials.Length > 0)
        {
            rend.material = new Material(materials[currentIndex]); // 인스턴스 생성
        }
    }

    private void Update()
    {
        if (materials == null || materials.Length == 0)
            return;

        timer += Time.deltaTime;

        if (timer >= changeInterval)
        {
            timer = 0f;

            currentIndex = (currentIndex + 1) % materials.Length;

            // 새로운 머티리얼로 전체 교체
            rend.material = new Material(materials[currentIndex]);
        }
    }
}