using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public class CameraFlythrough : MonoBehaviour
{
     [Header("Path")]
       public List<Transform> waypoints = new List<Transform>(); // 경로 포인트들 (입구→복도→도착)
       public float totalDuration = 1.6f;                         // 전체 이동 시간
       public AnimationCurve posEase = AnimationCurve.EaseInOut(0,0, 1,1);
   
       [Header("FOV Zoom")]
       public bool useFovZoom = true;
       public float fovStart = 60f;
       public float fovEnd = 42f;
       public AnimationCurve fovEase = AnimationCurve.EaseInOut(0,0, 1,1);
   
       [Header("End Shake")]
       public bool smallShakeOnEnd = true;
       public float shakeAmp = 0.08f;
       public float shakeTime = 0.12f;
   
       Camera cam;
   
       void Awake() { cam = GetComponent<Camera>(); }
   
       public IEnumerator Play()
       {
           if (waypoints == null || waypoints.Count < 2) yield break;
   
           // 시작 세팅
           transform.position = waypoints[0].position;
           transform.rotation = Quaternion.LookRotation(
               (waypoints[1].position - waypoints[0].position).normalized, Vector3.up);
           if (useFovZoom && cam) cam.fieldOfView = fovStart;
   
           // 구간별 균등 이동
           int segs = waypoints.Count - 1;
           float segDur = totalDuration / segs;
   
           for (int i = 0; i < segs; i++)
           {
               var a = waypoints[i].position;
               var b = waypoints[i+1].position;
               var qa = transform.rotation;
               var qb = Quaternion.LookRotation((b - a).normalized, Vector3.up);
   
               float t = 0f;
               while (t < segDur)
               {
                   t += Time.deltaTime;
                   float n = Mathf.Clamp01(t / segDur);
                   float e = posEase.Evaluate(n);
                   transform.position = Vector3.Lerp(a, b, e);
                   transform.rotation = Quaternion.Slerp(qa, qb, e);
   
                   if (useFovZoom && cam)
                   {
                       float whole = Mathf.Clamp01((i * segDur + t) / totalDuration);
                       cam.fieldOfView = Mathf.Lerp(fovStart, fovEnd, fovEase.Evaluate(whole));
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
