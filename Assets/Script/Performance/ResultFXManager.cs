using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

public class ResultFXManager : MonoBehaviour
{
    public TextMeshProUGUI resultText;  // “결과: 1등” 같은 텍스트
    public RectTransform resultRoot;    // 텍스트 부모(스케일 팝)
    public Image flash;                 // 풀스크린 흰 이미지(알파 0 시작)
    
    [Header("Exit")]
    public Button exitButton;          // ← 인스펙터 연결
    Action onClosed;                   // ← 닫힐 때 실행할 콜백
    
    public float holdTime = 0.9f;

    void Awake()
    {
        if (exitButton != null) {
            exitButton.gameObject.SetActive(false);
            exitButton.onClick.AddListener(() => {
                exitButton.gameObject.SetActive(false);
                gameObject.SetActive(false);  // 결과 패널 닫기
                onClosed?.Invoke();           // 복귀 콜백 실행
                onClosed = null;
            });
        }
        gameObject.SetActive(false); // 시작 시 숨김
    }
    
    public void Show(string label, Color color, Action onClosed)
    {
        this.onClosed = onClosed;
        gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(CoPlay(label, color));
    }

    IEnumerator CoPlay(string label, Color color)
    {
        if (resultText) { resultText.text = label; resultText.color = color; }
        if (resultRoot) resultRoot.localScale = Vector3.one * 0.6f;

        // 짧은 플래시
        if (flash) yield return StartCoroutine(FadeImage(flash, 0.5f, 0f, 0.22f));

        // 팝 스케일
        yield return StartCoroutine(ScaleTo(resultRoot, 0.12f, 1.2f));
        yield return StartCoroutine(ScaleTo(resultRoot, 0.08f, 1.0f));

        yield return new WaitForSeconds(holdTime);
        if (exitButton) exitButton.gameObject.SetActive(true); // 유저가 ‘나가기’ 누를 수 있게

    }

    IEnumerator ScaleTo(Transform tr, float dur, float target)
    {
        if (!tr) yield break;
        Vector3 s0 = tr.localScale;
        Vector3 s1 = Vector3.one * target;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t/dur), 3f);
            tr.localScale = Vector3.Lerp(s0, s1, e);
            yield return null;
        }
        tr.localScale = s1;
    }

    IEnumerator FadeImage(Image img, float from, float to, float dur)
    {
        if (!img) yield break;
        var c = img.color;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float e = (t/dur); e = e*e*(3f-2f*e); // smoothstep
            c.a = Mathf.Lerp(from, to, e);
            img.color = c;
            yield return null;
        }
        c.a = to; img.color = c;
    }
}