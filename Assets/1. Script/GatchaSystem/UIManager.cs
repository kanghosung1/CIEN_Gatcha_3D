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

    [Header("3D Stage / Camera")]
    public GameObject stageRoot;              // 비활성로 시작 권장
    public CameraFlythrough fly;

    [Header("Result FX / Ingame Label")]
    public ResultFXManager resultFX;
    [Tooltip("등수별 Parent들을 자식으로 가진 컨테이너(예: ResultIngame Root)")]
    public Transform resultIngame;            // 결과 간이 텍스트/이펙트 컨테이너
    public TextMeshPro midText;

    [Header("Timing")]
    [Tooltip("SpawnResultBall 이후 결과 UI를 띄우기까지 대기 시간(초)")]
    [SerializeField] private float delayAfterSpawn = 1.5f;

    [Header("Skip")]
    [Tooltip("플라이스루 도중 즉시 초기 UI로 돌아가는 스킵 버튼")]
    public Button skipButton;

    [Header("Optional")]
    public LottoMachine lotto;                // 로또 기계 참조(있다면 SpawnResultBall 사용)
    [Tooltip("resultIngame에 붙은 컨트롤러(등수별 Parent 활성화 담당)")]
    public ResultInGameEffectController ingameEffectCtrl;

    // 내부 상태
    readonly List<PrizeRowUI> rows = new List<PrizeRowUI>();
    private string lastPrizeNameOrLabel;

    // 코루틴 핸들(스킵용)
    private Coroutine _sequenceCo;
    private Coroutine _flyCo;

    void Start() {
        if (drawButton != null) drawButton.onClick.AddListener(OnClickDraw);
        if (exportCsvButton != null) exportCsvButton.onClick.AddListener(OnClickExportCsv);
        if (skipButton != null) {
            skipButton.onClick.AddListener(OnClickSkip);
            skipButton.gameObject.SetActive(false);
            skipButton.interactable = false;
        }

        if (gacha == null) {
            gacha = FindFirstObjectByType<GachaManager>();
            if (gacha == null) {
                Debug.LogError("UIManager: GachaManager를 찾을 수 없습니다. 인스펙터에서 지정하세요.");
                return;
            }
        }

        if (resultText != null) resultText.text = "이전 결과: -";

        BuildRows();
        RefreshAll();

        if (stageRoot) stageRoot.SetActive(false);

        // resultIngame 컨테이너/컨트롤러 참조 보정
        if (resultIngame != null && ingameEffectCtrl == null) {
            ingameEffectCtrl = resultIngame.GetComponent<ResultInGameEffectController>();
            if (ingameEffectCtrl == null) {
                // 자식에 붙여놨다면 GetComponentInChildren로도 탐색
                ingameEffectCtrl = resultIngame.GetComponentInChildren<ResultInGameEffectController>(true);
            }
        }

        if (resultIngame) resultIngame.gameObject.SetActive(false); // 표시 시점까지 비활성
    }

    void BuildRows() {
        if (gacha == null || gacha.State == null) {
            Debug.LogError("UIManager: gacha 또는 gacha.State가 null");
            return;
        }
        if (prizeListParent == null || prizeRowPrefab == null) {
            Debug.LogError("UIManager: prizeListParent 또는 prizeRowPrefab이 비어 있음");
            return;
        }

        for (int i = prizeListParent.childCount - 1; i >= 0; i--)
            Destroy(prizeListParent.GetChild(i).gameObject);
        rows.Clear();

        var prizes = gacha.State.prizes;
        for (int i = 0; i < prizes.Count; i++) {
            var go = Instantiate(prizeRowPrefab, prizeListParent);
            var row = go.GetComponent<PrizeRowUI>();
            if (row == null) {
                Debug.LogError("UIManager: prizeRowPrefab에 PrizeRowUI 컴포넌트가 없음");
                Destroy(go);
                continue;
            }
            rows.Add(row);
        }
    }

    public void RefreshAll() {
        if (gacha == null || gacha.State == null) return;

        var prizes = gacha.State.prizes;

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
            if (resultText) resultText.text = "모든 경품 소진";
            if (drawButton) drawButton.interactable = false;
            return;
        }

        lastPrizeNameOrLabel = prize.name;
        if (resultText) resultText.text = $"이전 결과: {prize.name}";
        RefreshAll();

        // 기존 시퀀스가 돌고 있으면 중지
        if (_sequenceCo != null) {
            StopCoroutine(_sequenceCo);
            _sequenceCo = null;
        }
        _sequenceCo = StartCoroutine(PlaySequenceThenShow(prize));
    }

    void OnClickExportCsv() {
        string path = Exporter.ExportCsv(gacha.State);
        if (resultText) resultText.text = $"CSV 저장: {path}";
    }

    IEnumerator PlaySequenceThenShow(Prize prize) {
        if (drawButton) drawButton.interactable = false;
        if (TotalUIPanel) TotalUIPanel.SetActive(false);
        if (resultIngame) resultIngame.gameObject.SetActive(false); // 초기엔 꺼둠

        lastPrizeNameOrLabel = prize.name;

        if (stageRoot) stageRoot.SetActive(true);

        // 스킵 버튼 활성화
        if (skipButton != null) {
            skipButton.gameObject.SetActive(true);
            skipButton.interactable = true;
        }

        // 1) 카메라 플라이스루: waypoint[0] → 마지막까지
        if (fly != null) {
            _flyCo = StartCoroutine(fly.Play());
            yield return _flyCo;   // 여기서 대기
            _flyCo = null;
        }

        // 2) 결과구슬 Spawn
        if (lotto != null) {
            lotto.SpawnResultBall();
        }

        // 3) 지정한 시간만큼 대기
        if (delayAfterSpawn > 0f) {
            yield return new WaitForSeconds(delayAfterSpawn);
        }

        // 4) 결과 Ingame 라벨(등수별 Parent 활성화) + ResultFX 출력
        int rank = GetRankFromPrizeId(prize.id); // 1~6로 매핑
        // resultIngame 컨테이너 켠 뒤, 등수 Parent만 on (나머지는 off)
        if (resultIngame != null) {
            resultIngame.gameObject.SetActive(true);
            if (ingameEffectCtrl != null) {
                ingameEffectCtrl.ActivateRankParent(rank); // 해당 Parent만 Enable → 자식 폭죽 OnEnable로 발동
            } else {
                Debug.LogWarning("UIManager: ResultIngameEffectController가 설정되지 않았습니다.");
            }
        }

        if (midText != null) {
            midText.text = $"결과: {lastPrizeNameOrLabel}";
        }

        if (resultFX != null) {
            var color = prize.id == "rank1" ? new Color(1f, 0.9f, 0.2f)
                : prize.id == "rank2" ? new Color(0.8f, 0.9f, 1f)
                : prize.id == "rank3" ? new Color(0.9f, 0.95f, 1f)
                : prize.id == "rank4" ? new Color(0.9f, 1f, 0.9f)
                : prize.id == "rank5" ? new Color(1f, 0.95f, 0.85f)
                : /* last = 6등 */     new Color(0.85f, 0.85f, 0.85f);

            resultFX.Show($"결과: {prize.name}", color, onClosed: () => {
                CleanupAfterSequence();
            });
        } else {
            if (resultText) resultText.text = $"결과: {prize.name}";
            CleanupAfterSequence();
        }

        _sequenceCo = null;
    }

    // 스킵 버튼
    void OnClickSkip() {
        // 카메라 코루틴 중단
        if (_flyCo != null) {
            StopCoroutine(_flyCo);
            _flyCo = null;
        }
        // 시퀀스 코루틴 중단
        if (_sequenceCo != null) {
            StopCoroutine(_sequenceCo);
            _sequenceCo = null;
        }

        // 결과 라벨/FX는 표시하지 않고 바로 초기 화면 복귀
        if (resultIngame) resultIngame.gameObject.SetActive(false);
        if (stageRoot) stageRoot.SetActive(false);

        // UI 복구
        if (skipButton != null) {
            skipButton.interactable = false;
            skipButton.gameObject.SetActive(false);
        }
        RefreshAll();
        if (TotalUIPanel) TotalUIPanel.SetActive(true);
        if (drawButton) drawButton.interactable = true;

        if (resultText) resultText.text = $"이전 결과: {lastPrizeNameOrLabel}";
    }

    void CleanupAfterSequence() {
        if (stageRoot) stageRoot.SetActive(false);
        RefreshAll();
        if (TotalUIPanel) TotalUIPanel.SetActive(true);
        if (drawButton) drawButton.interactable = true;

        if (skipButton != null) {
            skipButton.interactable = false;
            skipButton.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Prize.id를 등수(1~6)로 매핑
    /// rank1~rank5 → 1~5, last → 6
    /// 예외(id 미일치)는 6등으로 처리(원하면 변경)
    /// </summary>
    static int GetRankFromPrizeId(string id) {
        if (string.IsNullOrEmpty(id)) return 6;
        switch (id) {
            case "rank1": return 1;
            case "rank2": return 2;
            case "rank3": return 3;
            case "rank4": return 4;
            case "rank5": return 5;
            case "rank6": return 6; // 혹시나 rank6을 쓰는 경우 대비
            case "last":  return 6;
            default:      return 6;
        }
    }
}