using UnityEngine;

public class ResultIngameEffectController : MonoBehaviour
{
    [Tooltip("1등~6등에 대응하는 Parent 오브젝트 (인덱스 0 = 1등, 5 = 6등)")]
    public GameObject[] rankParents;

    /// <summary>
    /// 특정 등수(1~6)에 해당하는 Parent만 활성화.
    /// 등수 범위를 벗어나면 전부 비활성화.
    /// </summary>
    public void ActivateRankParent(int rank)
    {
        // rank는 1~6 입력 가정
        for (int i = 0; i < rankParents.Length; i++)
        {
            bool active = (i == rank - 1);
            if (rankParents[i] != null)
                rankParents[i].SetActive(active);
        }
    }
    // 테스트용 (에디터에서 R키로 임시 확인 가능)
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            int testRank = Random.Range(1, 7);
            Debug.Log($"[ResultIngameEffectController] Test Rank {testRank}");
            ActivateRankParent(testRank);
        }
    }
}
