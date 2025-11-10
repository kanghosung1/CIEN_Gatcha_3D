using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AdminPanel : MonoBehaviour
{
    [Header("Refs")]
    public GachaManager gacha;          // 씬의 GachaManager
    public UIManager ui;                // 씬의 UIManager (Refresh용)
    public TMP_Dropdown prizeDropdown;  // 상품 선택
    public TMP_InputField setInput;     // 남은 수량 지정 입력
    public Button plusBtn, minusBtn, setBtn, undoBtn;
    public TextMeshProUGUI statusText;  // 상태/안내 표시

    void Start()
    {
        if (gacha == null) gacha = FindFirstObjectByType<GachaManager>();
        if (ui == null) ui = FindFirstObjectByType<UIManager>();
        BuildPrizeDropdown();

        plusBtn.onClick.AddListener(() => Adjust(+1));
        minusBtn.onClick.AddListener(() => Adjust(-1));
        setBtn.onClick.AddListener(SetRemaining);
        undoBtn.onClick.AddListener(UndoLast);
    }
    
    
    void BuildPrizeDropdown()
    {
        prizeDropdown.ClearOptions();
        var options = gacha.State.prizes
            .Select(p => new TMP_Dropdown.OptionData($"{p.name} ({p.id})"))
            .ToList();
        prizeDropdown.AddOptions(options);
        prizeDropdown.value = 0;
        prizeDropdown.RefreshShownValue();
    }

    Prize CurrentPrize()
    {
        int idx = prizeDropdown.value;
        if (idx < 0 || idx >= gacha.State.prizes.Count) return null;
        return gacha.State.prizes[idx];
    }

    void Adjust(int delta)
    {
        var p = CurrentPrize();
        if (p == null) return;
        int before = p.remaining;
        p.remaining = Mathf.Max(0, p.remaining + delta);
        SaveSystem.Save(gacha.State);                  // 저장
        ui?.RefreshAll();                              // UI 갱신
        statusText.text = $"{p.name}: {before} → {p.remaining}";
    }

    void SetRemaining()
    {
        var p = CurrentPrize();
        if (p == null) return;
        if (!int.TryParse(setInput.text, out int v) || v < 0)
        {
            statusText.text = "유효하지 않은 값입니다.(0 이상의 정수)";
            return;
        }
        int before = p.remaining;
        p.remaining = v;
        SaveSystem.Save(gacha.State);
        ui?.RefreshAll();
        statusText.text = $"{p.name}: {before} → {p.remaining} (지정)";
    }

    void UndoLast()
    {
        string info;
        bool ok = SaveSystem.TryUndoLastDraw(gacha.State, out info);
        if (ok)
        {
            ui?.RefreshAll();
            statusText.text = $"Undo 성공: {info}";
        }
        else
        {
            statusText.text = $"Undo 실패: {info}";
        }
    }
}