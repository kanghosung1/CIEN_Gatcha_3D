using System.Collections.Generic;
using UnityEngine;

public class LottoMachine : MonoBehaviour
{
    [Header("Refs")]
    public Transform agitator;             // 돔 중앙에서 힘을 줄 기준점
    public List<Rigidbody> insideBalls;    // 돔 안에 미리 들어있는 공들
    public Transform ejectSpawn;           // 결과 공 생성 위치(돔 외부/토출구)
    public Transform ejectOutDir;          // 토출 방향 참조(전방 벡터)

    [Header("Agitate (항시 튀기기)")]
    public float burstInterval = 0.25f;   // 펄스 주기
    public float burstRadius = 1.2f;      // 영향 반경
    public float burstForce = 7.5f;       // 펄스 힘
    public float randomTorque = 0.8f;     // 회전 랜덤 토크
    public float windForce = 0.5f;        // 상시 ‘바람’ 세기

    [Header("Eject (결과 공 토출)")]
    public Rigidbody resultBallPrefab;     // 결과 공 프리팹(머티리얼 1장짜리)
    public float ejectForce = 8f;          // 앞으로 밀어낼 힘
    public float ejectUpward = 1.5f;       // 살짝 위로
    public Material[] colorPalette;        // 랜덤 색 팔레트(비어도 됨)

    [Header("Run")]
    public float runTime = 1.5f;           // 돔 흔들리는 총 시간 (토출 전 연출)
    public bool autoTestKey = true;        // 스페이스로 테스트 실행
    
    public Rigidbody lastSpawnedBall;   // 추가

    float timer;
    void Start()
    {
        // 시작 시 살짝 킥(정지 방지용)
        foreach (var rb in insideBalls)
        {
            if (!rb) continue;
            rb.AddForce(Random.onUnitSphere * 0.8f, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 0.5f, ForceMode.Impulse);
        }
    }

    void Update()
    {
        // 항시 약한 힘(바람) 가하기
        LightAgitate(Time.deltaTime);

        // 주기적 펄스
        timer += Time.deltaTime;
        if (timer >= burstInterval)
        {
            timer = 0f;
            AgitateOnce();
        }

        // 테스트/운영: 스페이스 → 결과 공 토출
        if (Input.GetKeyDown(KeyCode.Space))
            SpawnResultBall();
    }

    void AgitateOnce()
    {
        if (agitator == null) return;
        Vector3 center = agitator.position;

        foreach (var rb in insideBalls)
        {
            if (!rb) continue;
            Vector3 dir = (rb.worldCenterOfMass - center);
            float dist = dir.magnitude + 0.0001f;
            dir /= dist;

            float atten = Mathf.Clamp01(1f - (dist / Mathf.Max(0.0001f, burstRadius)));
            Vector3 force = dir * (burstForce * atten) + Random.onUnitSphere * 0.5f;
            rb.AddForce(force, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * randomTorque, ForceMode.Impulse);
        }
    }

    void LightAgitate(float dt)
    {
        foreach (var rb in insideBalls)
        {
            if (!rb) continue;
            rb.AddForce(Random.onUnitSphere * windForce, ForceMode.Acceleration);
        }
    }

    public void SpawnResultBall()
    {
        if (!resultBallPrefab || !ejectSpawn) return;

        var rb = Instantiate(resultBallPrefab, ejectSpawn.position, Quaternion.identity);
        lastSpawnedBall = rb;  // ★ 최근 생성된 공 저장

        // 랜덤 색(선택)
        if (colorPalette != null && colorPalette.Length > 0)
        {
            var mr = rb.GetComponentInChildren<MeshRenderer>();
            if (mr != null)
            {
                var mat = new Material(colorPalette[Random.Range(0, colorPalette.Length)]);
                mr.material = mat;
            }
        }

        Vector3 dir = (ejectOutDir ? ejectOutDir.forward : transform.forward);
        Vector3 vel = dir.normalized * ejectForce + Vector3.up * ejectUpward;
        rb.AddForce(vel, ForceMode.Impulse);
    }
}
