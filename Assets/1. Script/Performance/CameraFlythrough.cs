using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CameraFlythrough : MonoBehaviour
{
    [Header("Stops & Rotation")]
    [Tooltip("각 웨이포인트에 도착한 뒤 대기(초). 음수면 무한대기(TriggerContinue() 필요). 인덱스는 '도착지' 웨이포인트 기준.")]
    public List<float> arrivalPauses = new List<float>();

    [Tooltip("도착 후 다음 구간 진행 방향으로 회전하는 시간(초)")]
    public float rotateDuration = 0.25f;

    [Tooltip("회전 이징 곡선")]
    public AnimationCurve rotateEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("회전 마지막에 정확히 스냅할지(경계 프레임 잔여각 제거)")]
    public bool rotateSnapAtEnd = true;

    [Header("Path")]
    [Tooltip("0..N (Start -> ... -> End)")]
    public List<Transform> waypoints = new List<Transform>();

    [Tooltip("각 구간(waypoints[i] -> waypoints[i+1]) 이동 시간(초). 길이가 모자라면 마지막 유효값 사용.")]
    public List<float> segmentDurations = new List<float>() { 0.6f, 0.6f, 0.8f };

    [Header("Dive (특정 구간 가속/이징)")]
    [Tooltip("이 인덱스 구간부터(waypoints[diveStartIndex] -> waypoints[diveStartIndex+1])가 '다이브' 구간")]
    public int diveStartIndex = 2;

    [Tooltip("다이브 구간 시간(초). <=0이면 segmentDurations[diveStartIndex] 사용")]
    public float overrideDiveDuration = -1f;

    [Header("Easing")]
    [Tooltip("일반 구간 위치 이징")]
    public AnimationCurve posEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("다이브 구간 위치 이징(가속감 주려면 EaseIn 권장)")]
    public AnimationCurve diveEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("FOV Zoom (경로 전체 진행도 기반)")]
    public bool useFovZoom = true;
    public float fovStart = 60f;
    public float fovEnd = 42f;
    public AnimationCurve fovEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("End Shake")]
    public bool smallShakeOnEnd = true;
    public float shakeAmp = 0.08f;
    public float shakeTime = 0.12f;

    [Header("Teleport on Dive")]
    [Tooltip("diveStartIndex에 진입하면 다음(+1) 구간 시작점으로 즉시 텔레포트합니다.")]
    public bool teleportOnDive = true;

    [Tooltip("몇 구간 앞(기본 +1)으로 텔레포트할지")]
    public int teleportAdvance = 1;
    
    [System.Serializable]
    public class WaypointToggleSet
    {
        public List<GameObject> targets = new List<GameObject>();
        public List<bool> states = new List<bool>();
    }

    [Header("Per-Waypoint Toggle (Multiple)")]
    [Tooltip("각 웨이포인트별로 여러 오브젝트의 활성/비활성 상태를 설정")]
    public List<WaypointToggleSet> waypointToggles = new List<WaypointToggleSet>();

    [Tooltip("시작 시(waypoint[0]에 ‘이미 도착’한 상태)도 토글을 적용할지")]
    public bool applyToggleOnStart = true;

    // 이벤트
    public System.Action OnDiveBegin;

    // 무한대기 해제용
    bool _continueSignal;

    Camera _cam;

    void ApplyArrivalActions(int waypointIndex)
    {
        if (waypointIndex < 0 || waypointToggles == null || waypointIndex >= waypointToggles.Count)
            return;

        var toggleSet = waypointToggles[waypointIndex];
        if (toggleSet == null || toggleSet.targets == null) return;

        for (int i = 0; i < toggleSet.targets.Count; i++)
        {
            var obj = toggleSet.targets[i];
            if (obj == null) continue;

            bool desiredState = true;
            if (i < toggleSet.states.Count)
                desiredState = toggleSet.states[i];

            obj.SetActive(desiredState);
        }
    }
    
    void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    // 무한대기 해제(외부 호출)
    public void TriggerContinue() => _continueSignal = true;

    float GetArrivalPause(int waypointIndex)
    {
        if (arrivalPauses == null || arrivalPauses.Count == 0) return 0f;
        if (waypointIndex < 0) return 0f;
        if (waypointIndex < arrivalPauses.Count) return arrivalPauses[waypointIndex];
        return 0f;
    }

    float GetDurationForSegment(int segIndex)
    {
        if (segIndex == diveStartIndex && overrideDiveDuration > 0f)
            return overrideDiveDuration;

        if (segmentDurations == null || segmentDurations.Count == 0)
            return 0.6f;

        if (segIndex >= 0 && segIndex < segmentDurations.Count && segmentDurations[segIndex] > 0f)
            return segmentDurations[segIndex];

        for (int i = segmentDurations.Count - 1; i >= 0; i--)
            if (segmentDurations[i] > 0f) return segmentDurations[i];

        return 0.6f;
    }

    float ComputeTotalDuration()
    {
        if (waypoints == null) return 0f;
        int segs = Mathf.Max(0, waypoints.Count - 1);
        float total = 0f;
        for (int i = 0; i < segs; i++) total += GetDurationForSegment(i);
        return Mathf.Max(0.0001f, total);
    }

    /// <summary>
    /// 전체 시퀀스 실행: 시작 세팅 → (이동 → 대기 → 회전)* → 종료 셰이크
    /// </summary>
    public IEnumerator Play()
    {
        if (waypoints == null || waypoints.Count < 2) yield break;

        // 시작 상태: 위치 고정, 시선은 0->1
        transform.position = waypoints[0].position;
        Quaternion startRot = Quaternion.LookRotation(
            (waypoints[1].position - waypoints[0].position).normalized, Vector3.up);
        transform.rotation = startRot;

        if (useFovZoom && _cam) _cam.fieldOfView = fovStart;
        if (applyToggleOnStart) ApplyArrivalActions(0);

        int segs = waypoints.Count - 1;
        float totalDur = ComputeTotalDuration();
        float elapsedGlobal = 0f; // ★ 경로 전체 진행도(끊김/점프 방지 핵심)

        for (int i = 0; i < segs; i++)
        {
            if (teleportOnDive && i == diveStartIndex)
            {
                // 텔레포트 목표 웨이포인트(현재 세그먼트의 도착지에서 +teleportAdvance-1 만큼 더)
                // 기본은 +1 세그먼트 시작점(= waypoints[i+1])으로 점프 후, 그 구간(i+1 -> i+2)부터 다시 dive
                int targetWpIndex = Mathf.Min(i + teleportAdvance, waypoints.Count - 1);

                // FOV 진행도 끊김 방지: 스킵한 세그먼트 시간만큼 글로벌 시간 누적
                float skippedDur = GetDurationForSegment(i);
                elapsedGlobal += skippedDur;

                // 실제 위치 텔레포트
                transform.position = waypoints[targetWpIndex].position;

                // 다음 진행 방향으로 시선 정렬(있으면)
                if (targetWpIndex < waypoints.Count - 1)
                {
                    Vector3 lookDir = waypoints[targetWpIndex + 1].position - waypoints[targetWpIndex].position;
                    if (lookDir.sqrMagnitude > 1e-8f)
                        transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                }
                
                ApplyArrivalActions(targetWpIndex);

                // 도착 대기 적용(선택적으로 유지)
                float pause = GetArrivalPause(targetWpIndex);
                if (pause < 0f)
                {
                    _continueSignal = false;
                    while (!_continueSignal) yield return null;
                }
                else if (pause > 0f)
                {
                    yield return new WaitForSeconds(pause);
                }

                // 이제 "다음(+1) 세그먼트"를 다이브 구간으로 재지정
                // (현재 i는 곧 i++로 넘어가므로, 다음 루프에서 i+1 == diveStartIndex 가 되어 다이브가 다시 시작됨)
                diveStartIndex = targetWpIndex;

                // 이 세그먼트(i)의 이동/회전 처리는 모두 건너뛰고 다음으로 진행
                continue;
            }
            
            // (1) 이동: i -> i+1
            if (i == diveStartIndex) OnDiveBegin?.Invoke();

            Vector3 a = waypoints[i].position;
            Vector3 b = waypoints[i + 1].position;
            Vector3 seg = b - a;

            if (seg.sqrMagnitude < 1e-8f)
            {
                transform.position = b;
            }
            else
            {
                float dur = GetDurationForSegment(i);
                float t = 0f;

                // 이동 동안엔 회전 고정
                Quaternion fixedRot = transform.rotation;

                while (t < dur)
                {
                    float dt = Time.deltaTime;
                    t += dt;
                    elapsedGlobal += dt;

                    float nLocal = Mathf.Clamp01(t / dur);
                    float nEase = (i == diveStartIndex) ? diveEase.Evaluate(nLocal) : posEase.Evaluate(nLocal);

                    // 위치 보간
                    transform.position = Vector3.Lerp(a, b, nEase);

                    // 이동 중 회전 고정
                    transform.rotation = fixedRot;

                    // FOV: ★ 경로 전체 진행도 기반 보간(세그먼트 경계 점프 제거)
                    if (useFovZoom && _cam)
                    {
                        float nGlobal = Mathf.Clamp01(elapsedGlobal / totalDur);
                        _cam.fieldOfView = Mathf.Lerp(fovStart, fovEnd, fovEase.Evaluate(nGlobal));
                    }

                    yield return null;
                }

                // 안전 스냅
                transform.position = b;
            }
            ApplyArrivalActions(i + 1);

            // (2) 도착 후 대기
            float afterPause = GetArrivalPause(i + 1); // 도착지 기준
            if (afterPause < 0f)
            {
                _continueSignal = false;
                while (!_continueSignal) yield return null;
            }
            else if (afterPause > 0f)
            {
                yield return new WaitForSeconds(afterPause);
            }

            // (3) 다음 구간 방향으로 회전 (마지막 세그먼트는 불필요)
            if (i >= segs - 1) break;

            Vector3 nextDir = (waypoints[i + 2].position - waypoints[i + 1].position);
            if (nextDir.sqrMagnitude > 1e-8f)
            {
                Quaternion fromRot = transform.rotation;
                Quaternion toRot = Quaternion.LookRotation(nextDir.normalized, Vector3.up);

                float rt = 0f;
                float rDur = Mathf.Max(0.0001f, rotateDuration);
                while (rt < rDur)
                {
                    rt += Time.deltaTime;
                    float r01 = Mathf.Clamp01(rt / rDur);
                    float rE = rotateEase.Evaluate(r01);
                    transform.rotation = Quaternion.Slerp(fromRot, toRot, rE);
                    yield return null;
                }
                if (rotateSnapAtEnd) transform.rotation = toRot;
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