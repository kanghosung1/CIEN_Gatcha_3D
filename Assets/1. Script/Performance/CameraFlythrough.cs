using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public class CameraFlythrough : MonoBehaviour
{
     [Header("Path")]
    public List<Transform> waypoints = new List<Transform>(); // 0..N (Start -> ... -> Entry -> TunnelEnd 등)

    [Tooltip("각 구간(waypoints[i] -> waypoints[i+1])의 이동 시간(초). 길이가 모자라면 마지막 값을 재사용합니다.")]
    public List<float> segmentDurations = new List<float>() { 0.6f, 0.6f, 0.8f };
    
    [Header("Stops (per waypoint)")]
    public List<float> waypointPauses = new List<float>(); // 예: [0, 0.5, -1, 0, 0]  (2번 웨이포인트에서 무한대기)

    [Header("Dive (모니터 빨려들기 제어)")]
    [Tooltip("이 인덱스 구간부터(waypoints[diveStartIndex] -> waypoints[diveStartIndex+1])가 '다이브' 구간입니다.")]
    public int diveStartIndex = 2;
    
    [Tooltip("다이브 구간의 시간(초). 비워두면 segmentDurations[diveStartIndex]를 사용합니다.")]
    public float overrideDiveDuration = -1f;

    [Header("Easing")]
    [Tooltip("접근/일반 구간 보간곡선")]
    public AnimationCurve posEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("다이브 구간 보간곡선(가속감을 주려면 EaseIn 형태 권장)")]
    public AnimationCurve diveEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("FOV Zoom")]
    public bool useFovZoom = true;
    public float fovStart = 60f;
    public float fovEnd = 42f;
    public AnimationCurve fovEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("End Shake")]
    public bool smallShakeOnEnd = true;
    public float shakeAmp = 0.08f;
    public float shakeTime = 0.12f;

    // 회전 스무딩 파라미터
    [Header("Turn Smoothing")]
    [Tooltip("웨이포인트 코너에서 시선 전환이 부드럽게 일어나도록 하는 시간(초)")]
    public float turnSmoothTime = 0.15f;

    // 다이브 시작 알림 이벤트 (UIManager가 구독)
    public System.Action OnDiveBegin;
    // 무한대기 해제용
    bool _continueSignal;
    float GetPauseForWaypoint(int waypointIndex)
    {
        if (waypointIndex < 0) return 0f;
        if (waypointPauses == null || waypointPauses.Count == 0) return 0f;
        if (waypointIndex < waypointPauses.Count) return waypointPauses[waypointIndex];
        return 0f;
    }

// 외부에서 무한대기 해제
    public void TriggerContinue()
    {
        _continueSignal = true;
    }
    
    Vector3 _fwdVel; // SmoothDamp용 내부 속도
    
    Camera cam;
    bool diveSignal; // TriggerDive()로 켜짐

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    /// <summary>
    /// 외부에서 호출: 다이브 시작 신호(대기 해제)
    /// </summary>
    public void TriggerDive()
    {
        diveSignal = true;
    }
    float GetDurationForSegment(int segIndex)
    {
        // 다이브 구간만 별도 시간으로 강제
        if (segIndex == diveStartIndex && overrideDiveDuration > 0f)
            return overrideDiveDuration;

        // 기본값 처리
        if (segmentDurations == null || segmentDurations.Count == 0)
            return 0.6f;

        // 정상 범위 값이 있고 양수면 그 값 사용
        if (segIndex >= 0 && segIndex < segmentDurations.Count && segmentDurations[segIndex] > 0f)
            return segmentDurations[segIndex];

        // 길이가 모자라거나 0/음수면 마지막 유효한 양수 값을 재사용
        for (int i = segmentDurations.Count - 1; i >= 0; i--)
            if (segmentDurations[i] > 0f) return segmentDurations[i];

        // 그마저도 없으면 안전 기본값
        return 0.6f;
    }

    /// <summary>
    /// 전체 시퀀스 플레이. 시작 위치 세팅 → 구간별 이동 → (선택) 대기 → 다이브 → 종료 셰이크
    /// </summary>
    public IEnumerator Play()
    {
  if (waypoints == null || waypoints.Count < 2) yield break;

    // 시작 세팅
    transform.position = waypoints[0].position;
    transform.rotation = Quaternion.LookRotation(
        (waypoints[1].position - waypoints[0].position).normalized, Vector3.up);
    if (useFovZoom && cam) cam.fieldOfView = fovStart;

    int segs = waypoints.Count - 1;

    for (int i = 0; i < segs; i++)
    {
        // (선택) 웨이포인트 대기 기능 넣으셨다면 여기서 처리
        float pause = GetPauseForWaypoint(i); // 없으면 0 반환하도록 구현 or 0으로 가정
        if (pause < 0f)
        {
            _continueSignal = false;
            while (!_continueSignal) yield return null; // TriggerContinue() 기다림
        }
        else if (pause > 0f)
        {
            yield return new WaitForSeconds(pause);
        }

        // 다이브 구간 시작 이벤트 (UI에서 ejectDelay 타이밍 잡을 때 사용)
        if (i == diveStartIndex)
        {
            OnDiveBegin?.Invoke();
        }

        // 이번 구간의 a->b 설정
        Vector3 a = waypoints[i].position;
        Vector3 b = waypoints[i + 1].position;

        // ✅ 제로 길이 구간은 스킵 (회전 타깃 0벡터 방지)
        Vector3 seg = b - a;
        if (seg.sqrMagnitude < 1e-8f)
            continue;

        float dur = GetDurationForSegment(i);



        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / dur);
            float e = (i == diveStartIndex) ? diveEase.Evaluate(n) : posEase.Evaluate(n);

            // 위치 보간
            transform.position = Vector3.Lerp(a, b, e);

            // ✅ 회전 스무딩 (끊김 제거 핵심)
            // 1) 현재 구간 진행방향
            Vector3 segDir = (b - a).normalized;

            // 2) 코너에서 다음 구간 방향을 살짝 미리 섞어 더 자연스럽게 (옵션)
            if (i < segs - 1)
            {
                Vector3 c = waypoints[i + 2].position;
                Vector3 next = (c - b);
                if (next.sqrMagnitude > 1e-8f)
                {
                    float cornerBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.65f, 0.98f, n));
                    segDir = Vector3.Slerp(segDir, next.normalized, cornerBlend * 0.35f);
                }
            }
            // 3) 목표 회전
            Quaternion targetRot = Quaternion.LookRotation(segDir, Vector3.up);
            
            float rotAlpha = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, turnSmoothTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotAlpha);

            if (n > 0.995f)
            {
                transform.rotation = targetRot;
            }
            
            // FOV 보간
            if (useFovZoom && cam)
            {
                float f = (i == diveStartIndex) ? e : fovEase.Evaluate(n);
                cam.fieldOfView = Mathf.Lerp(fovStart, fovEnd, f);
            }

            yield return null;
        }
    }

    if (smallShakeOnEnd) yield return StartCoroutine(EndShake());
    }

    IEnumerator EndShake()
    {
        Vector3 basePos = transform.position;
        float t = 0f;
        while (t < shakeTime)
        {
            t += Time.deltaTime;
            float n = 1f - t / shakeTime;
            float amp = shakeAmp * n;
            transform.position = basePos + new Vector3(
                Random.Range(-amp, amp),
                Random.Range(-amp, amp),
                Random.Range(-amp, amp) * 0.5f
            );
            yield return null;
        }
        transform.position = basePos;
    }
}
