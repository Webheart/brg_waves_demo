using System;
using Unity.Mathematics;
using UnityEngine;

public class RaycastWaveFromInput : MonoBehaviour
{
    [SerializeField] Waves waves;
    [SerializeField] float raycastDistance = 1000f;
    [SerializeField] float maxMouseDelta = 10;
    [SerializeField] float2 strengthRange = new(0.5f, 2f);

    Camera mainCamera;

    void Awake()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (!Input.GetMouseButton(0)) return;
        var touchPosition = Input.mousePosition;
        var ray = mainCamera.ScreenPointToRay(touchPosition);
        var deltaMagnitude = math.clamp(Input.mousePositionDelta.magnitude, 0, maxMouseDelta);
        var t = math.unlerp(0, maxMouseDelta, deltaMagnitude);
        var strength = math.lerp(strengthRange.x, strengthRange.y, t);
        waves.RaycastNewWave(ray.origin, ray.GetPoint(raycastDistance), strength);
    }
}