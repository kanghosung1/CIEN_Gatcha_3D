using System.IO;
using UnityEngine;
using System;

public static class SaveSystem {
    static string Dir => Application.persistentDataPath; //게임을 꺼도 저장되는 파일
    static string StatePath => Path.Combine(Dir, "state.json");
    static string TempPath  => Path.Combine(Dir, "state.tmp");
    static string BakPath   => Path.Combine(Dir, "state.bak.json");
    static string LogPath   => Path.Combine(Dir, "drawlog.ndjson"); // newline-delimited JSON

    public static void EnsureDir() //Dir 이 있는지 확인, 없으면 생성
    { 
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir); 
    }

    public static void Save(AppState state)
    {
        EnsureDir();
        state.lastSavedIso = DateTime.UtcNow.ToString("o");
        string json = JsonUtility.ToJson(state, prettyPrint: true);

        // temp 파일에 먼저 쓰기
        using (var fs = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None,
                   bufferSize: 4096, options: FileOptions.WriteThrough))
        using (var sw = new StreamWriter(fs))
        {
            sw.Write(json);
            sw.Flush();
            fs.Flush(true);
        }

        try
        {
            if (File.Exists(StatePath))
            {
                // 기존 파일이 있을 때만 Replace 사용
                File.Replace(TempPath, StatePath, BakPath, true);
            }
            else
            {
                // 최초 저장: 대상 파일이 없으면 Move로 새로 생성
                File.Move(TempPath, StatePath);
                // 백업 파일도 하나 만들어둠
                File.Copy(StatePath, BakPath, overwrite: true);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Save failed: {e}");
            // 임시 파일이 남아 있으면 삭제
            if (File.Exists(TempPath))
                File.Delete(TempPath);
        }
    }
    
    
    public static AppState LoadOrInit(Func<AppState> factory) // AppState를 새로 만들어주는 함수를 인자로 받음
    {
        EnsureDir();
        try {
            if (File.Exists(StatePath)) {
                string json = File.ReadAllText(StatePath);
                var state = JsonUtility.FromJson<AppState>(json);
                if (state != null) return state;
            }
            // state.json이 없거나 파싱 실패 → 백업 시도
            if (File.Exists(BakPath)) {
                string bak = File.ReadAllText(BakPath);
                var state = JsonUtility.FromJson<AppState>(bak);
                if (state != null) return state;
            }
        } catch { /* 아래에서 factory 사용 */ }

        var created = factory();
        Save(created);
        return created;
    }

    [Serializable] class DrawLogLine {
        public string iso;
        public string prizeId;
        public string prizeName;
        public int afterRemaining;
    }

    public static void AppendDrawLog(string prizeId, string prizeName, int afterRemaining) {
        EnsureDir();
        var line = new DrawLogLine {
            iso = DateTime.UtcNow.ToString("o"),
            prizeId = prizeId,
            prizeName = prizeName,
            afterRemaining = afterRemaining
        };
        string json = JsonUtility.ToJson(line);
        using (var fs = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.Read,
                                       bufferSize: 4096, options: FileOptions.WriteThrough)) {
            using (var sw = new StreamWriter(fs)) {
                sw.WriteLine(json);
                sw.Flush();
                fs.Flush(true);
            }
        }
    }
    public static bool TryUndoLastDraw(AppState state, out string info)
    {
        info = "";
        EnsureDir();

        // 로그 읽기
        if (!File.Exists(LogPath))
        {
            info = "로그 파일이 없습니다.";
            return false;
        }

        var lines = File.ReadAllLines(LogPath);
        if (lines.Length == 0)
        {
            info = "되돌릴 기록이 없습니다.";
            return false;
        }

        // 마지막 줄 파싱
        string last = lines[lines.Length - 1];
        DrawLogLine lastRec = null;
        try
        {
            lastRec = JsonUtility.FromJson<DrawLogLine>(last);
        }
        catch
        {
            info = "마지막 로그 레코드를 파싱할 수 없습니다.";
            return false;
        }
        if (lastRec == null || string.IsNullOrEmpty(lastRec.prizeId))
        {
            info = "마지막 로그 레코드가 비어 있습니다.";
            return false;
        }

        // 해당 상품 찾기
        var prize = state.prizes.Find(p => p.id == lastRec.prizeId);
        if (prize == null)
        {
            info = $"로그의 prizeId({lastRec.prizeId})가 현재 상태에 없습니다.";
            return false;
        }

        // 상태 되돌리기
        prize.remaining++;
        if (state.stats.totalDraws > 0) state.stats.totalDraws--;
        if (state.stats.winCounts.ContainsKey(prize.id) && state.stats.winCounts[prize.id] > 0)
            state.stats.winCounts[prize.id]--;

        info = $"되돌림: {lastRec.prizeName} / 남은수량 {prize.remaining}";

        // 로그에서 마지막 줄 제거(원자적 교체)
        var tmpPath = Path.Combine(Dir, "drawlog.tmp");
        using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None,
                   bufferSize: 4096, options: FileOptions.WriteThrough))
        using (var sw = new StreamWriter(fs))
        {
            for (int i = 0; i < lines.Length - 1; i++)
                sw.WriteLine(lines[i]);
            sw.Flush();
            fs.Flush(true);
        }
#if UNITY_STANDALONE_WIN
        File.Replace(tmpPath, LogPath, null, true);
#else
    File.Copy(tmpPath, LogPath, overwrite:true);
    File.Delete(tmpPath);
#endif

        // 상태 저장
        Save(state);
        return true;
    }
}
