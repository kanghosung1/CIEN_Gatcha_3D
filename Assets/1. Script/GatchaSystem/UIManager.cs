using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour {
    [Header("Refs")]
    public GachaManager gacha;                // 씬의 GachaManager Drag&Drop
    public Button drawButton;                 // “가챠 돌리기” (메인 UI)
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
    [Tooltip("플라이스루/대기 생략 후 '뽑기 버튼 활성화 상태'까지 스킵하는 버튼")]
    public Button skipToRevealButton;         // 이제 이 버튼만 사용

    [Header("In-stage Draw")]
    [Tooltip("카메라 연출이 끝난 후 3D 스테이지 위에 나타나는 '뽑기' 버튼")]
    public Button stageDrawButton;            // 새로 추가되는 버튼

    [Header("Optional")]
    public LottoMachine lotto;                // 로또 기계 참조(있다면 SpawnResultBall 사용)
    [Tooltip("resultIngame에 붙은 컨트롤러(등수별 Parent 활성화 담당)")]
    public ResultIngameEffectController ingameEffectCtrl;

    public GameObject TurnOffAtSkip;
    
    // 내부 상태
    readonly List<PrizeRowUI> rows = new List<PrizeRowUI>();
    private string lastPrizeNameOrLabel;
    private Prize _currentPrize;

    // 코루틴 핸들
    private Coroutine _sequenceCo;
    private Coroutine _flyCo;

    // 진행 플래그
    private bool _spawnedBall;   // SpawnResultBall 수행 여부
    private bool _revealed;      // 결과 표시 완료 여부

    void Start() {
        if (drawButton != null) drawButton.onClick.AddListener(OnClickDraw);
        if (exportCsvButton != null) exportCsvButton.onClick.AddListener(OnClickExportCsv);

        // 새 2차 뽑기 버튼
        if (stageDrawButton != null) {
            stageDrawButton.onClick.AddListener(OnClickStageDraw);
            stageDrawButton.gameObject.SetActive(false);
            stageDrawButton.interactable = false;
        }

        // SkipToReveal 버튼: 플라이스루 → "뽑기 버튼 활성화 상태"까지 스킵
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

    // 메인 UI에서 "가챠 돌리기" 버튼
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
        _sequenceCo = StartCoroutine(PlaySequenceUntilStageDraw(prize));
    }

    void OnClickExportCsv() {
        string path = Exporter.ExportCsv(gacha.State);
        if (resultText) resultText.text = $"CSV 저장: {path}";
    }

    /// <summary>
    /// 카메라 플라이스루까지 진행하고, 끝나면 2차 뽑기 버튼(stageDrawButton)을 활성화하는 시퀀스
    /// </summary>
    IEnumerator PlaySequenceUntilStageDraw(Prize prize) {
        _currentPrize = prize;
        _spawnedBall = false;
        _revealed = false;

        if (drawButton) drawButton.interactable = false;
        if (TotalUIPanel) TotalUIPanel.SetActive(false);
        if (resultIngame) resultIngame.gameObject.SetActive(false); // 초기엔 꺼둠

        if (stageRoot) stageRoot.SetActive(true);

        // 2차 뽑기 버튼은 카메라 연출이 끝날 때까지 숨김
        if (stageDrawButton != null) {
            stageDrawButton.gameObject.SetActive(false);
            stageDrawButton.interactable = false;
        }

        // 스킵 버튼 활성화 (플라이스루 → "뽑기 버튼 활성화 상태"까지 스킵)
        SetSkipToReveal(active: true, interactable: true);

        // 1) 카메라 플라이스루
        if (fly != null) {
            _flyCo = StartCoroutine(fly.Play());
            yield return _flyCo;   // 여기서 대기
            _flyCo = null;
        }

        // 2) 플라이스루 끝 → 2차 뽑기 버튼 활성화
        if (stageDrawButton != null) {
            stageDrawButton.gameObject.SetActive(true);
            stageDrawButton.interactable = true;
        }

        // 이 시점에서는 스킵 버튼 필요 없음
        SetSkipToReveal(active: false, interactable: false);

        _sequenceCo = null;
    }

    /// <summary>
    /// 카메라 연출이 끝난 뒤, 2차 뽑기 버튼을 눌렀을 때: 공 Spawn + delayAfterSpawn 후 결과 표시
    /// </summary>
    void OnClickStageDraw() {
        if (_currentPrize == null) return;

        if (stageDrawButton != null) {
            stageDrawButton.interactable = false;
            stageDrawButton.gameObject.SetActive(false);
        }

        // 혹시 이전 서브 시퀀스가 있다면 정리
        if (_sequenceCo != null) {
            StopCoroutine(_sequenceCo);
            _sequenceCo = null;
        }

        _sequenceCo = StartCoroutine(SpawnBallThenShowResult());
    }

    IEnumerator SpawnBallThenShowResult() {
        _spawnedBall = false;
        _revealed = false;

        // 1) 결과 구슬 Spawn
        EnsureSpawnBall();

        // 2) 대기
        if (delayAfterSpawn > 0f) {
            yield return new WaitForSeconds(delayAfterSpawn);
        }

        // 3) 결과 표시
        RevealCurrentPrize();

        _sequenceCo = null;
    }

    // === 스킵: 플라이스루/대기 생략 → "뽑기 버튼 활성화된 상태"까지 즉시 이동 ===
    void OnClickSkipToReveal() {
        // 진행 중 코루틴 중단
        if (_flyCo != null) { StopCoroutine(_flyCo); _flyCo = null; }
        if (_sequenceCo != null) { StopCoroutine(_sequenceCo); _sequenceCo = null; }

        TurnOffAtSkip.gameObject.SetActive(false);
        
        // 스테이지 유지
        if (stageRoot) stageRoot.SetActive(true);

// 카메라를 마지막 웨이포인트 상태로 즉시 스냅
        if (fly != null) {
            // 스킵할 때는 waypoint 토글 상태는 건들지 않음
            fly.JumpToEnd(applyArrivalToggles: false);
        }

        // 공은 아직 Spawn하지 않음
        _spawnedBall = false;
        _revealed = false;

        // 2차 뽑기 버튼 바로 활성화
        if (stageDrawButton != null) {
            stageDrawButton.gameObject.SetActive(true);
            stageDrawButton.interactable = true;
        }

        // 이 시점 이후로는 스킵 버튼 숨김
        SetSkipToReveal(active: false, interactable: false);
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

        if (resultFX != null) {
            var color = prize.id == "rank1" ? new Color(1f, 0.9f, 0.2f)
                : prize.id == "rank2" ? new Color(0.8f, 0.9f, 1f)
                : prize.id == "rank3" ? new Color(0.9f, 0.95f, 1f)
                : prize.id == "rank4" ? new Color(0.9f, 1f, 0.9f)
                : prize.id == "rank5" ? new Color(1f, 0.95f, 0.85f)
                : new Color(0.85f, 0.85f, 0.85f);

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

        // 스킵 버튼은 기본적으로 꺼두기
        SetSkipToReveal(active: false, interactable: false);

        // 2차 뽑기 버튼도 끔
        if (stageDrawButton != null) {
            stageDrawButton.gameObject.SetActive(false);
            stageDrawButton.interactable = false;
        }

        _currentPrize = null;
        _sequenceCo = null;
        _flyCo = null;
        _spawnedBall = false;
        _revealed = false;
        // 카메라 초기 위치로 리셋
        if (fly != null)
        {
            fly.transform.position = new Vector3(0.2f, 1f, -10f);
            fly.transform.rotation = Quaternion.identity; // 필요하면 그대로 유지
        }
    }

    void SetSkipToReveal(bool active, bool interactable) {
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