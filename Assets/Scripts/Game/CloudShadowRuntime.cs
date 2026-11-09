using UnityEngine;
using UnityEngine.Rendering;

public sealed class CloudShadowRuntime : MonoBehaviour
{
    private const int TextureSize = 512;
    private const float PlaneSize = 260f;
    private const float HeightAboveCamera = 42f;

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
    private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");

    private Camera targetCamera;
    private Light sun;
    private Transform cloudPlane;
    private Material cloudMaterial;
    private Texture2D cloudTexture;
    private Vector2 textureOffset;

    private readonly Vector2 cloudVelocity = new Vector2(0.0035f, 0.0018f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateAtRuntime()
    {
        if (FindObjectOfType<CloudShadowRuntime>() != null)
            return;

        GameObject host = new GameObject("Runtime Cloud Shadows");
        DontDestroyOnLoad(host);
        host.AddComponent<CloudShadowRuntime>();
    }

    private void Update()
    {
        if (targetCamera == null)
            targetCamera = FindTargetCamera();

        if (sun == null)
            sun = FindSun();

        if (cloudPlane == null && targetCamera != null && sun != null)
            CreateCloudShadowPlane();

        if (cloudPlane == null || targetCamera == null)
            return;

        Vector3 cameraPosition = targetCamera.transform.position;
        cloudPlane.position = new Vector3(
            cameraPosition.x,
            cameraPosition.y + HeightAboveCamera,
            cameraPosition.z);

        textureOffset += cloudVelocity * Time.deltaTime;
        cloudMaterial.SetTextureOffset(BaseMapId, textureOffset);
    }

    private Camera FindTargetCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.isActiveAndEnabled)
            return mainCamera;

        Camera[] cameras = FindObjectsOfType<Camera>();
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].isActiveAndEnabled)
                return cameras[i];
        }

        return null;
    }

    private Light FindSun()
    {
        if (RenderSettings.sun != null &&
            RenderSettings.sun.isActiveAndEnabled &&
            RenderSettings.sun.type == LightType.Directional)
            return RenderSettings.sun;

        Light[] lights = FindObjectsOfType<Light>();
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null &&
                lights[i].isActiveAndEnabled &&
                lights[i].type == LightType.Directional)
                return lights[i];
        }

        return null;
    }

    private void CreateCloudShadowPlane()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null)
        {
            Debug.LogError("[Cloud Shadows] 找不到 URP/Lit Shader，云影未能创建。");
            enabled = false;
            return;
        }

        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.9f;

        cloudTexture = GenerateCloudTexture();
        cloudMaterial = new Material(litShader)
        {
            name = "Runtime Cloud Shadow Material",
            enableInstancing = true,
            renderQueue = (int)RenderQueue.AlphaTest
        };

        cloudMaterial.SetTexture(BaseMapId, cloudTexture);
        cloudMaterial.SetColor(BaseColorId, Color.white);
        cloudMaterial.SetFloat(AlphaClipId, 1f);
        cloudMaterial.SetFloat(CutoffId, 0.5f);
        cloudMaterial.SetFloat("_Surface", 0f);
        cloudMaterial.SetFloat("_Cull", 2f);
        cloudMaterial.EnableKeyword("_ALPHATEST_ON");
        cloudMaterial.SetOverrideTag("RenderType", "TransparentCutout");
        cloudMaterial.SetTextureScale(BaseMapId, new Vector2(3f, 3f));

        GameObject planeObject = new GameObject("Cloud Shadow Caster");
        planeObject.transform.SetParent(transform, false);

        MeshFilter meshFilter = planeObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = CreatePlaneMesh(PlaneSize);

        MeshRenderer meshRenderer = planeObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = cloudMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        cloudPlane = planeObject.transform;

        Debug.Log("[Cloud Shadows] 已创建云影投射平面，使用 URP 方向光实时阴影。");
    }

    private static Mesh CreatePlaneMesh(float size)
    {
        float half = size * 0.5f;
        Mesh mesh = new Mesh { name = "Runtime Cloud Shadow Plane" };

        mesh.vertices = new[]
        {
            new Vector3(-half, 0f, -half),
            new Vector3(-half, 0f,  half),
            new Vector3( half, 0f,  half),
            new Vector3( half, 0f, -half)
        };

        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f)
        };

        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Texture2D GenerateCloudTexture()
    {
        Texture2D texture = new Texture2D(
            TextureSize, TextureSize, TextureFormat.RGBA32, false, true)
        {
            name = "Runtime Procedural Cloud Mask",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 1
        };

        Color32[] pixels = new Color32[TextureSize * TextureSize];

        for (int y = 0; y < TextureSize; y++)
        {
            float v = (float)y / TextureSize;

            for (int x = 0; x < TextureSize; x++)
            {
                float u = (float)x / TextureSize;

                float warpX = PeriodicNoise(u, v, 3);
                float warpY = PeriodicNoise(u + 0.37f, v + 0.71f, 3);
                float sampleU = Mathf.Repeat(u + (warpX - 0.5f) * 0.18f, 1f);
                float sampleV = Mathf.Repeat(v + (warpY - 0.5f) * 0.18f, 1f);

                float broad = PeriodicNoise(sampleU, sampleV, 4);
                float medium = PeriodicNoise(sampleU, sampleV, 8);
                float detail = PeriodicNoise(sampleU, sampleV, 16);
                float cloudValue = broad * 0.70f + medium * 0.22f + detail * 0.08f;

                float alpha = Mathf.SmoothStep(0.47f, 0.53f, cloudValue);
                byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                pixels[y * TextureSize + x] = new Color32(255, 255, 255, a);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static float PeriodicNoise(float u, float v, int cells)
    {
        float px = u * cells;
        float py = v * cells;

        int x0 = Mathf.FloorToInt(px);
        int y0 = Mathf.FloorToInt(py);
        float tx = px - x0;
        float ty = py - y0;

        tx = tx * tx * (3f - 2f * tx);
        ty = ty * ty * (3f - 2f * ty);

        float a = Hash(Mathf.Repeat(x0, cells), Mathf.Repeat(y0, cells));
        float b = Hash(Mathf.Repeat(x0 + 1, cells), Mathf.Repeat(y0, cells));
        float c = Hash(Mathf.Repeat(x0, cells), Mathf.Repeat(y0 + 1, cells));
        float d = Hash(Mathf.Repeat(x0 + 1, cells), Mathf.Repeat(y0 + 1, cells));

        float top = Mathf.Lerp(a, b, tx);
        float bottom = Mathf.Lerp(c, d, tx);
        return Mathf.Lerp(top, bottom, ty);
    }

    private static float Hash(float x, float y)
    {
        float value = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
        return value - Mathf.Floor(value);
    }

    private void OnDestroy()
    {
        if (cloudMaterial != null)
            Destroy(cloudMaterial);

        if (cloudTexture != null)
            Destroy(cloudTexture);

        if (cloudPlane != null)
        {
            MeshFilter filter = cloudPlane.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
                Destroy(filter.sharedMesh);
        }
    }
}
