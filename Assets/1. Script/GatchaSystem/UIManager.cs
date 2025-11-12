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
    public Button skipButton;                 // 초기 화면으로
    [Tooltip("플라이스루/대기 생략 후 즉시 결과 표시로 이동하는 스킵 버튼")]
    public Button skipToRevealButton;         // 결과 직행

    [Header("Optional")]
    public LottoMachine lotto;                // 로또 기계 참조(있다면 SpawnResultBall 사용)
    [Tooltip("resultIngame에 붙은 컨트롤러(등수별 Parent 활성화 담당)")]
    public ResultIngameEffectController ingameEffectCtrl;

    // 내부 상태
    readonly List<PrizeRowUI> rows = new List<PrizeRowUI>();
    private string lastPrizeNameOrLabel;
    private Prize _currentPrize;

    // 코루틴 핸들(스킵용)
    private Coroutine _sequenceCo;
    private Coroutine _flyCo;

    // 진행 플래그
    private bool _spawnedBall;   // SpawnResultBall 수행 여부
    private bool _revealed;      // 결과 표시 완료 여부

    void Start() {
        if (drawButton != null) drawButton.onClick.AddListener(OnClickDraw);
        if (exportCsvButton != null) exportCsvButton.onClick.AddListener(OnClickExportCsv);
        if (skipButton != null) {
            skipButton.onClick.AddListener(OnClickSkipToHome);
            skipButton.gameObject.SetActive(false);
            skipButton.interactable = false;
        }
        if (skipToRevealButton != null) {
            skipToRevealButton.onClick.AddListener(OnClickSkipToReveal);
            skipToRevealButton.gameObject.SetActive(false);
            skipToRevealButton.interactable = false;
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
            ingameEffectCtrl = resultIngame.GetComponent<ResultIngameEffectController>();
            if (ingameEffectCtrl == null) {
                ingameEffectCtrl = resultIngame.GetComponentInChildren<ResultIngameEffectController>(true);
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
        _currentPrize = prize;
        _spawnedBall = false;
        _revealed = false;

        if (drawButton) drawButton.interactable = false;
        if (TotalUIPanel) TotalUIPanel.SetActive(false);
        if (resultIngame) resultIngame.gameObject.SetActive(false); // 초기엔 꺼둠

        if (stageRoot) stageRoot.SetActive(true);

        // 스킵 버튼 활성화
        SetSkipButtons(active: true, interactable: true);

        // 1) 카메라 플라이스루
        if (fly != null) {
            _flyCo = StartCoroutine(fly.Play());
            yield return _flyCo;   // 여기서 대기
            _flyCo = null;
        }

        // 2) 결과구슬 Spawn
        EnsureSpawnBall();

        // 3) 대기
        if (delayAfterSpawn > 0f) {
            yield return new WaitForSeconds(delayAfterSpawn);
        }

        // 4) 결과 표시
        RevealCurrentPrize();

        _sequenceCo = null;
    }

    // === 스킵 ①: 처음 화면으로 복귀 ===
    void OnClickSkipToHome() {
        if (_flyCo != null) { StopCoroutine(_flyCo); _flyCo = null; }
        if (_sequenceCo != null) { StopCoroutine(_sequenceCo); _sequenceCo = null; }

        if (resultIngame) resultIngame.gameObject.SetActive(false);
        if (stageRoot) stageRoot.SetActive(false);

        SetSkipButtons(active: false, interactable: false);

        RefreshAll();
        if (TotalUIPanel) TotalUIPanel.SetActive(true);
        if (drawButton) drawButton.interactable = true;

        if (resultText) resultText.text = $"이전 결과: {lastPrizeNameOrLabel}";
    }

    // === 스킵 ②: 결과 직전까지(카메라도 마지막으로 스냅) → 즉시 결과 표시 ===
    void OnClickSkipToReveal() {
        // 진행 중 코루틴 중단
        if (_flyCo != null) { StopCoroutine(_flyCo); _flyCo = null; }
        if (_sequenceCo != null) { StopCoroutine(_sequenceCo); _sequenceCo = null; }

        // 스테이지 유지
        if (stageRoot) stageRoot.SetActive(true);

        // ★ 카메라를 마지막 웨이포인트 상태로 즉시 스냅
        if (fly != null) {
            fly.JumpToEnd(applyArrivalToggles: true);
        }

        // 결과구슬이 아직이면 바로 Spawn
        EnsureSpawnBall();

        // 대기 생략 → 결과 바로 표시
        RevealCurrentPrize();
    }

    void EnsureSpawnBall() {
        if (_spawnedBall) return;
        if (lotto != null) {
            lotto.SpawnResultBall();
        }
        _spawnedBall = true;
    }

    void RevealCurrentPrize() {
        if (_revealed) return;
        _revealed = true;

        var prize = _currentPrize;
        if (prize == null) {
            CleanupAfterSequence();
            return;
        }

        if (resultIngame != null) {
            resultIngame.gameObject.SetActive(true);
            int rank = GetRankFromPrizeId(prize.id);
            if (ingameEffectCtrl != null) {
                ingameEffectCtrl.ActivateRankParent(rank); // 해당 Parent만 Enable → 폭죽 OnEnable 발동
            }
        }
        if (midText != null) {
            midText.text = $"결과: {lastPrizeNameOrLabel}";
        }

        // 결과 순간엔 스킵 비활성
        SetSkipButtons(active: false, interactable: false);

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
    }

    void CleanupAfterSequence() {
        if (stageRoot) stageRoot.SetActive(false);
        RefreshAll();
        if (TotalUIPanel) TotalUIPanel.SetActive(true);
        if (drawButton) drawButton.interactable = true;

        SetSkipButtons(active: false, interactable: false);

        _currentPrize = null;
        _sequenceCo = null;
        _flyCo = null;
        _spawnedBall = false;
        _revealed = false;
    }

    void SetSkipButtons(bool active, bool interactable) {
        if (skipButton != null) {
            skipButton.gameObject.SetActive(active);
            skipButton.interactable = interactable;
        }
        if (skipToRevealButton != null) {
            skipToRevealButton.gameObject.SetActive(active);
            skipToRevealButton.interactable = interactable;
        }
    }

    /// <summary>
    /// Prize.id를 등수(1~6)로 매핑
    /// rank1~rank5 → 1~5, last → 6
    /// </summary>
    static int GetRankFromPrizeId(string id) {
        if (string.IsNullOrEmpty(id)) return 6;
        switch (id) {
            case "rank1": return 1;
            case "rank2": return 2;
            case "rank3": return 3;
            case "rank4": return 4;
            case "rank5": return 5;
            case "rank6": return 6; // 혹시나 rank6도 쓸 수 있게 대비
            case "last":  return 6;
            default:      return 6;
        }
    }
}