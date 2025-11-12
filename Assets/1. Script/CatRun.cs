using UnityEngine;

public class CatRun : MonoBehaviour
{
    private float moveSpeed = 0.5f; // 이동 속도 (초당 단위)

    void Update()
    {
        transform.Translate(Vector3.forward * moveSpeed * Time.deltaTime);
    }
    void OnEnable()
    {
        transform.position = new Vector3(-18.17f, 0, 1.67f);
    }
}