using UnityEngine;

public class CatWalk1 : MonoBehaviour
{
    private float moveSpeed = 1f; // 이동 속도 (초당 단위)

    void Update()
    {
        transform.Translate(Vector3.forward * moveSpeed * Time.deltaTime);
    }
}