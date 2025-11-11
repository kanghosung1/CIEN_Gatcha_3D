using UnityEngine;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class DevPanel : MonoBehaviour {
    public GachaManager gacha;
    bool show;

    void Update() {
        if (Input.GetKeyDown(KeyCode.F1)) show = !show;
    }

    void OnGUI() {
        if (!show) return;
        GUI.Box(new Rect(10,10,320,240), "Dev Panel (F1로 토글)");
        GUILayout.BeginArea(new Rect(20,40,300,200));
        
        if (GUILayout.Button("저장 폴더 열기")) {
#if UNITY_EDITOR
            EditorUtility.RevealInFinder(Application.persistentDataPath);
#else
            Application.OpenURL(Application.persistentDataPath);
#endif
        }
        if (GUILayout.Button("state.json 삭제(초기화)")) {
            var dir = Application.persistentDataPath;
            var state = Path.Combine(dir, "state.json");
            var bak   = Path.Combine(dir, "state.bak.json");
            var log   = Path.Combine(dir, "drawlog.ndjson");
            if (File.Exists(state)) File.Delete(state);
            if (File.Exists(bak))   File.Delete(bak);
            if (File.Exists(log))   File.Delete(log);
            // 다음 실행 시 LoadOrInit가 초기화 상태 생성
        }
        if (GUILayout.Button("앱 강제 종료(복구 테스트)")) {
            // 저장 도중이 아닌 정상 시점에 호출된다고 가정. 뽑기 직후는 이미 저장 끝남.
#if UNITY_EDITOR
            EditorApplication.isPlaying = false; // 에디터 플레이모드 종료
#else
            Application.Quit();
#endif
        }

        GUILayout.EndArea();
    }
}