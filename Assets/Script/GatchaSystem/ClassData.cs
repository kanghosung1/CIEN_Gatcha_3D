using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Prize {
    public string id;      // "rank1","rank2","rank3","rank4","last"
    public string name;    // "1등", "2등", ...
    public int remaining;  // 남은 수량
    public int weight;     // 확률 가중치 (남은 수량>0일 때만 반영)
}

[Serializable]
public class DrawStats {
    public int totalDraws;
    public Dictionary<string,int> winCounts = new Dictionary<string,int>(); // id별 당첨 수
}

[Serializable]
public class AppState {
    public List<Prize> prizes = new List<Prize>();
    public DrawStats stats = new DrawStats();
    public string startedAtIso;  // 행사 시작 시각
    public string lastSavedIso;
}