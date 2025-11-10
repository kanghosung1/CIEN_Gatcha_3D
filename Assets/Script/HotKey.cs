using UnityEngine;

public class HotKey : MonoBehaviour
{
    [Header("Toggle target (Admin Panel root)")]
    public GameObject panel;           // AdminPanel 최상위 GameObject
    public KeyCode key = KeyCode.F1;   // 원하는 키

    void Start()
    {
        if (panel != null) panel.SetActive(false); // 시작 시 닫힘
    }

    void Update()
    {
        if (panel == null) return;
        if (Input.GetKeyDown(key))
        {
            panel.SetActive(!panel.activeSelf);
        }
    }
}
