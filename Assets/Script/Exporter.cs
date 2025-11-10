using System.Text;
using System.IO;
using UnityEngine;

public static class Exporter {
    public static string ExportCsv(AppState s) {
        var sb = new StringBuilder();
        sb.AppendLine("PrizeID,PrizeName,Remaining,WonCount");
        foreach (var p in s.prizes) {
            int won = s.stats.winCounts.TryGetValue(p.id, out var cnt) ? cnt : 0;
            sb.AppendLine($"{p.id},{p.name},{p.remaining},{won}");
        }
        string path = Path.Combine(Application.persistentDataPath, "summary.csv");
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        return path; // 관리자 화면에 경로 표시
    }
}