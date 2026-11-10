using System;
using System.Linq;
using UnityEngine;
using System.Collections;
using Random = UnityEngine.Random;

public class GachaManager : MonoBehaviour {
    public AppState State;

    [Header("Settings")]
    public bool proportionalToRemaining = true; // true면 남은 개수 비례 확률 모드

    void Awake()
    {
        State = SaveSystem.LoadOrInit(CreateInitialState);
    }
    
    void Start() {
        StartCoroutine(AutoSaveLoop()); // 보조
    }

    AppState CreateInitialState() {
        var s = new AppState {
            startedAtIso = DateTime.UtcNow.ToString("o")
        };
        s.prizes = new System.Collections.Generic.List<Prize> {
            new Prize{ id="rank1", name="1등", remaining=1,  weight=1 },
            new Prize{ id="rank2", name="2등", remaining=3,  weight=1 },
            new Prize{ id="rank3", name="3등", remaining=8, weight=1 },
            new Prize{ id="rank4", name="4등", remaining=17, weight=1 }
        };

        // ✅ winCounts 딕셔너리 미리 전부 생성
        foreach (var p in s.prizes)
            s.stats.winCounts[p.id] = 0;

        return s;
    }

    public Prize DrawOnce() {
        var candidates = State.prizes.Where(p => p.remaining > 0 && p.weight > 0).ToList();
        if (candidates.Count == 0) {
            // 모든 경품 소진
            return null;
        }
        int totalW = proportionalToRemaining
            ? candidates.Sum(p => p.weight * p.remaining)
            : candidates.Sum(p => p.weight);
        
        int r = Random.Range(0, totalW);
        Prize chosen = null;
        
        foreach (var p in candidates) {
            int effectiveWeight = proportionalToRemaining ? p.weight * p.remaining : p.weight;
            if (r < effectiveWeight) { chosen = p; break; }
            r -= effectiveWeight;
        }
        if (chosen == null) chosen = candidates.Last(); // 안전장치

        // 수량 차감 & 통계 증가
        chosen.remaining--;
        State.stats.totalDraws++;
        if (!State.stats.winCounts.ContainsKey(chosen.id))
            State.stats.winCounts[chosen.id] = 0;
        State.stats.winCounts[chosen.id]++;

        // 로그/저장(중요: 추첨 직후 즉시)
        SaveSystem.AppendDrawLog(chosen.id, chosen.name, chosen.remaining);
        SaveSystem.Save(State);

        return chosen;
    }

    IEnumerator AutoSaveLoop() {
        var wait = new WaitForSeconds(60f);
        while (true) {
            yield return wait;
            SaveSystem.Save(State);
        }
    }
}