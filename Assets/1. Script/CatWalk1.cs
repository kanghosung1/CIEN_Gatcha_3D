using UnityEngine;

public class CatWalk1 : MonoBehaviour
{
    private float moveSpeed = 1f; // 이동 속도 (초당 단위)

    void Update()
    {
        transform.Translate(Vector3.forward * moveSpeed * Time.deltaTime);
    }
    void OnEnable()
    {
        transform.position = new Vector3(-27.56f, 0, 4.91f);
    }
}