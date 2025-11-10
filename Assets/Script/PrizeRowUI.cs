using TMPro;
using UnityEngine;

public class PrizeRowUI : MonoBehaviour {
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI remainingText;
    public TextMeshProUGUI wonText;

    public void Set(string name, int remaining, int won) {
        nameText.text = name;
        remainingText.text = $"남은수량: {remaining}";
        wonText.text = $"당첨수: {won}";
    }
}