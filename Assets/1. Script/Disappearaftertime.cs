using Unity.VisualScripting;
using UnityEngine;

public class Disappearaftertime : MonoBehaviour
{
    [Tooltip("이 오브젝트가 Enable된 후 유지될 시간(초)")]
    public float lifeTime = 2f;

    private float timer;

    private void OnEnable()
    {
        timer = 0f; 
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= lifeTime)
        {
            this.GameObject().SetActive(false);
        }
    }
}
