using Unity.VisualScripting;
using UnityEngine;

public class Disappearaftertime : MonoBehaviour
{
    [Tooltip("Enable된 후, 줄어들기 시작할 때까지 기다리는 시간(초)")]
    public float startShrinkTime = 2f;

    [Tooltip("줄어들어서 사라지는 데 걸리는 시간(초)")]
    public float shrinkDuration = 0.5f;
    
    private float timer;
    private float shrinkTimer;
    private bool isShrinking = false;
    
    private Vector3 initialScale;

    private void Awake()
    {
        // 게임 시작 시점의 크기를 기억해 둠
        initialScale = transform.localScale;
    }
    private void OnEnable()
    {
        // 다시 켜질 때마다 초기화
        timer = 0f;
        shrinkTimer = 0f;
        isShrinking = false;

        transform.localScale = initialScale;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (!isShrinking)
        {
            if (timer >= startShrinkTime)
            {
                isShrinking = true;
                shrinkTimer = 0f;
            }
            else
            {
                return; // 아직 할 거 없음
            }
        }

        shrinkTimer += Time.deltaTime;
        float t = Mathf.Clamp01(shrinkTimer / shrinkDuration);

        transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, t);

        if (t >= 1f)
        {
            
                this.GameObject().SetActive(false);
                // 나중에 다시 쓸 거면, 다음 Enable 때 originalScale로 자동 복구됨
        }
    }
}