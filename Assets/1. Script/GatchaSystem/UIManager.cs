using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour {
    [Header("Refs")]
    public GachaManager gacha;                // 씬의 GachaManager Drag&Drop
    public Button drawButton;                 // “가챠 돌리기”
    public TextMeshProUGUI resultText;        // 결과 표시
    public Transform prizeListParent;         // ScrollView Content 오브젝트
    public GameObject prizeRowPrefab;         // PrizeRowPrefab
    public Button exportCsvButton;            // (선택) CSV 내보내기

    public GameObject TotalUIPanel;
    
    // 3D 세트 루트(연출 때만 켬)
    public GameObject stageRoot;                 // 비활성로 시작
// 카메라 연출 컴포넌트
    public CameraFlythrough fly;
// 결과 연출
    public ResultFXManager resultFX;
    
    // 내부 상태
    readonly List<PrizeRowUI> rows = new List<PrizeRowUI>();

    void Start() {
        // 버튼 연결
        if (drawButton != null) drawButton.onClick.AddListener(OnClickDraw);
        if (exportCsvButton != null) exportCsvButton.onClick.AddListener(OnClickExportCsv);

        if (gacha == null) {
            gacha = FindFirstObjectByType<GachaManager>();
            if (gacha == null) {
                Debug.LogError("UIManager: GachaManager를 찾을 수 없습니다. 참조를 인스펙터에서 지정하세요.");
                return;
            }
        }
        if (resultText != null) resultText.text = "이전 결과: -";
        BuildRows();      // 먼저 행 생성
        RefreshAll();     // 그 다음 내용 채우기
        stageRoot.SetActive(false);
    }

    void BuildRows() {
        if (gacha == null || gacha.State == null) {
            Debug.LogError("UIManager: gacha 또는 gacha.State가 null입니다. GachaManager 참조를 확인하세요.");
            return;
        }
        if (prizeListParent == null || prizeRowPrefab == null) {
            Debug.LogError("UIManager: prizeListParent 또는 prizeRowPrefab이 비어 있습니다.");
            return;
        }

        // 기존 UI 비우기
        for (int i = prizeListParent.childCount - 1; i >= 0; i--)
            Destroy(prizeListParent.GetChild(i).gameObject);
        rows.Clear();

        // 현재 prize 개수만큼 행 생성
        var prizes = gacha.State.prizes;
        for (int i = 0; i < prizes.Count; i++) {
            var go = Instantiate(prizeRowPrefab, prizeListParent);
            var row = go.GetComponent<PrizeRowUI>();
            if (row == null) {
                Debug.LogError("UIManager: prizeRowPrefab에 PrizeRowUI 컴포넌트가 없습니다.");
                Destroy(go);
                continue;
            }
            rows.Add(row);
        }
    }

    public void RefreshAll() {
        if (gacha == null || gacha.State == null) return;

        var prizes = gacha.State.prizes;

        // rows 개수와 prizes 개수가 다르면 자동 재빌드
        if (rows.Count != prizes.Count) {
            Debug.LogWarning($"UIManager: rows({rows.Count}) != prizes({prizes.Count}) → Rebuild");
            BuildRows();
        }

        int n = Mathf.Min(rows.Count, prizes.Count);
        for (int i = 0; i < n; i++) {
            var p = prizes[i];
            int won = 0;
            if (gacha.State.stats.winCounts.TryGetValue(p.id, out var cnt))
                won = cnt;
            rows[i].Set($"{p.name}", p.remaining, won);
        }
    }
    
   
        
        
    void OnClickDraw() {
        var prize = gacha.DrawOnce();
        if (prize == null) {
            resultText.text = "모든 경품 소진";
            drawButton.interactable = false;
            return;
        }
        StartCoroutine(PlaySequenceThenShow(prize));
        resultText.text = $"이전 결과: {prize.name}";
        RefreshAll();
    }

    void OnClickExportCsv() {
        string path = Exporter.ExportCsv(gacha.State);
        resultText.text = $"CSV 저장: {path}";
    }
    
    System.Collections.IEnumerator PlaySequenceThenShow(Prize prize) {
        // 입력 잠금
        if (drawButton) drawButton.interactable = false;

        if (TotalUIPanel) TotalUIPanel.SetActive(false);
        
        // 3D 스테이지 켜고 카메라 이동
        if (stageRoot) stageRoot.SetActive(true);
        if (fly != null) yield return StartCoroutine(fly.Play());

        // 결과 연출(UI)
        if (resultFX != null) {
            var color = prize.id == "rank1" ? new Color(1f,0.9f,0.2f)
                : prize.id == "rank2" ? new Color(0f,0f,0f)
                : prize.id == "last"  ? new Color(0f,0f,0f)
                : Color.black;
            resultFX.Show($"결과: {prize.name}", color, onClosed: () =>
            {
                if (stageRoot) stageRoot.SetActive(false);
                // 결과 패널 닫힌 후에만 복귀 처리
                RefreshAll();                         // 숫자 갱신
                if (TotalUIPanel) TotalUIPanel.SetActive(true);
                if (drawButton) drawButton.interactable = true;
            });
        } 
        else {
            // 안전망
            if (resultText) resultText.text = $"결과: {prize.name}";
            RefreshAll();
            if (TotalUIPanel) TotalUIPanel.SetActive(true);
            if (drawButton) drawButton.interactable = true;
        }
    }
}