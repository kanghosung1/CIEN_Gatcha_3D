using UnityEngine;

public class CatGoBack : MonoBehaviour
{
    private new Vector3 pos;
    void Awake()
    {
        pos =  transform.position;
    }

    void OnEnable()
    {
        transform.position = pos;
    }
}
