using UnityEngine;

public class ColorCycler : MonoBehaviour
{
    [Tooltip("순서대로 적용할 색 리스트")]
    public Color[] colors;

    [Tooltip("색이 바뀌는 시간 간격 (초)")]
    public float changeInterval = 1f;

    [Tooltip("Emission 강도 (밝기). 1~5 정도 추천")]
    public float emissionIntensity = 2f;

    private Renderer rend;
    private int currentIndex = 0;
    private float timer = 0f;

    private void Awake()
    {
        rend = GetComponent<Renderer>();

        // Emission 활성화 (Standard / URP 둘 다 가능)
        rend.material.EnableKeyword("_EMISSION");
    }

    private void OnEnable()
    {
        timer = 0f;
        currentIndex = 0;

        ApplyColor(colors[currentIndex]);
    }

    private void Update()
    {
        if (colors == null || colors.Length == 0)
            return;

        timer += Time.deltaTime;

        if (timer >= changeInterval)
        {
            timer = 0f;

            currentIndex = (currentIndex + 1) % colors.Length;

            ApplyColor(colors[currentIndex]);
        }
    }

    private void ApplyColor(Color c)
    {
        // 기본 색 변경
        rend.material.color = c;

        // Emission 색 변경 + 강도 적용
        Color emissionColor = c * Mathf.LinearToGammaSpace(emissionIntensity);
        rend.material.SetColor("_EmissionColor", emissionColor);
    }
}