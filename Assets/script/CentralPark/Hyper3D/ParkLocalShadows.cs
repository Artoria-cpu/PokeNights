using UnityEngine;
using UnityEngine.Rendering;

// Limit point-light shadow maps to the lamps nearest the camera being rendered.
[ExecuteAlways]
public sealed class ParkLocalShadows : MonoBehaviour
{
    public Light[] lamps = new Light[0];
    [Range(1, 8)] public int shadowBudget = 6;
    float[] distances;
    void OnEnable() => RenderPipelineManager.beginCameraRendering += BeforeCamera;
    void BeforeCamera(ScriptableRenderContext context, Camera camera)
    {
        if (camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection) return;
        Refresh(camera);
    }
    public void Refresh(Camera camera)
    {
        if (!camera) return;
        if (distances == null || distances.Length != lamps.Length) distances = new float[lamps.Length];
        for (int i = 0; i < lamps.Length; i++)
            distances[i] = lamps[i] && lamps[i].isActiveAndEnabled
                ? (lamps[i].transform.position - camera.transform.position).sqrMagnitude : float.PositiveInfinity;
        for (int i = 0; i < lamps.Length; i++)
        {
            if (!lamps[i]) continue;
            int rank = 0;
            for (int j = 0; j < lamps.Length; j++)
                if (distances[j] < distances[i] || (distances[j] == distances[i] && j < i)) rank++;
            var mode = rank < shadowBudget && !float.IsInfinity(distances[i]) ? LightShadows.Hard : LightShadows.None;
            if (lamps[i].shadows != mode) lamps[i].shadows = mode;
        }
    }
    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeforeCamera;
        foreach (var lamp in lamps) if (lamp) lamp.shadows = LightShadows.None;
    }
}
