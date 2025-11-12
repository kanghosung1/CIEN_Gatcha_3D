using System.Collections;
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

    public LottoMachine lotto;                 // 로또 기계 참조
    public Transform resultIngame;            // 말미에 잠깐 띄울 임시 텍스트(새 텍스트)
    public TextMeshPro midText;
    public float ejectDelayAfterDive = 0.45f;  // 다이브 신호 후 로또 토출까지 지연(초)
    private string lastPrizeNameOrLabel;

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
        
        if (fly != null)
        {
            // 중복 구독 방지 위해 먼저 해제
            fly.OnDiveBegin -= HandleDiveBegin;
            fly.OnDiveBegin += HandleDiveBegin;
        }
        
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
        lastPrizeNameOrLabel = prize.name;
        StartCoroutine(PlaySequenceThenShow(prize));
        resultText.text = $"이전 결과: {prize.name}";
        RefreshAll();
    }

    void OnClickExportCsv() {
        string path = Exporter.ExportCsv(gacha.State);
        resultText.text = $"CSV 저장: {path}";
    }
    
    IEnumerator PlaySequenceThenShow(Prize prize) {
        if (drawButton) drawButton.interactable = false;
        if (TotalUIPanel) TotalUIPanel.SetActive(false);

        lastPrizeNameOrLabel = prize.name;

        if (stageRoot) stageRoot.SetActive(true);

        bool flyDone = false;
        StartCoroutine(PlayFlyThenMark(() => flyDone = true)); // fly.Play() 안에서 정지/진행을 모두 처리

        // 여기서는 다이브 타이밍을 건드리지 않음 (OnDiveBegin에서 eject를 처리)

        // 카메라 연출 끝날 때까지 대기
        while (!flyDone) yield return null;

        // 결과 패널
        if (resultFX != null) {
            var color = prize.id == "rank1" ? new Color(1f,0.9f,0.2f)
                : prize.id == "rank2" ? new Color(0.8f,0.9f,1f)
                : prize.id == "last"  ? new Color(0.85f,0.85f,0.85f)
                : Color.white;

            resultFX.Show($"결과: {prize.name}", color, onClosed: () =>
            {
                if (stageRoot) stageRoot.SetActive(false);
                RefreshAll();
                if (TotalUIPanel) TotalUIPanel.SetActive(true);
                if (drawButton) drawButton.interactable = true;
            });
        } else {
            if (resultText) resultText.text = $"결과: {prize.name}";
            RefreshAll();
            if (stageRoot) stageRoot.SetActive(false);
            if (TotalUIPanel) TotalUIPanel.SetActive(true);
            if (drawButton) drawButton.interactable = true;
        }
    }
    
    // fly.Play()를 병렬로 돌리고, 끝나면 콜백으로 마킹
    IEnumerator PlayFlyThenMark(System.Action onDone) {
        if (fly != null) yield return StartCoroutine(fly.Play());
        onDone?.Invoke();
    }
    void HandleDiveBegin()
    {
        // 다이브가 시작된 "그 순간"을 기준으로 ejectDelay를 적용
        StartCoroutine(CoEjectAfterDelay());
    }
    
    IEnumerator CoEjectAfterDelay()
    {
        yield return new WaitForSeconds(ejectDelayAfterDive);

        // 로또 토출
        if (lotto != null) lotto.SpawnResultBall();

        if (midText != null || resultIngame != null)
        {
            resultIngame.gameObject.SetActive(true);
            midText.text = $"결과: {lastPrizeNameOrLabel}"; // prize.name을 저장해두고 쓰면 됩니다.
        }
    }
    
}