using UnityEngine;
using UnityEngine.Rendering;
using MFrameork.Event;

public sealed class CloudShadowRuntime : MonoBehaviour, IEventReceiver<GameModeChangedEvent>
{
    private const int TextureSize = 1024;

    [Header("云影外观")]
    [SerializeField, Range(2f, 20f), Tooltip("数值越大，单位面积内的云块越多，云块越小。")]
    private float cloudTiling = 8f;

    [SerializeField, Range(5f, 100f), Tooltip("探索模式下，云层投射平面高于初始摄像机的距离。")]
    private float exploreCloudHeight = 45f;

    [SerializeField, Range(5f, 100f), Tooltip("战斗模式下，云层投射平面高于当前摄像机的距离。")]
    private float battleCloudHeight = 20f;

    private GameMode currentGameMode = GameMode.Explore;

    private float ActiveCloudHeight =>
        currentGameMode == GameMode.Battle ? battleCloudHeight : exploreCloudHeight;

    [SerializeField, Range(120f, 500f), Tooltip("云层投射平面的世界尺寸。")]
    private float planeSize = 320f;

    [SerializeField, Range(0.0001f, 0.02f), Tooltip("云纹理流动速度。")]
    private float cloudSpeed = 0.0025f;

    [SerializeField, Range(0.0001f, 0.02f), Tooltip("云纹理横向流动速度。")]
    private float cloudSideSpeed = 0.0012f;

    [Header("阴影")]
    [SerializeField, Range(0.1f, 1f)]
    private float shadowStrength = 0.9f;

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
    private float fixedCloudAltitude;
    private bool hasCameraAnchor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateAtRuntime()
    {
        if (FindObjectOfType<CloudShadowRuntime>() != null)
            return;

        GameObject host = new GameObject("Runtime Cloud Shadows");
        DontDestroyOnLoad(host);
        host.AddComponent<CloudShadowRuntime>();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<GameModeChangedEvent>(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<GameModeChangedEvent>(this);
    }

    public void OnEvent(GameModeChangedEvent evt)
    {
        ApplyGameMode(evt.newMode);
    }

    private void Start()
    {
        if (GameModeManager.Instance != null)
            ApplyGameMode(GameModeManager.Instance.CurrentGameMode);
    }

    private void ApplyGameMode(GameMode newMode)
    {
        currentGameMode = newMode;
        if (targetCamera == null)
            return;

        fixedCloudAltitude = targetCamera.transform.position.y + ActiveCloudHeight;
        if (cloudPlane != null)
        {
            Vector3 position = cloudPlane.position;
            position.y = fixedCloudAltitude;
            cloudPlane.position = position;
        }
    }

    private void Update()
    {
        if (GameModeManager.Instance != null &&
            currentGameMode != GameModeManager.Instance.CurrentGameMode)
            ApplyGameMode(GameModeManager.Instance.CurrentGameMode);

        // Re-check MainCamera so scene transitions and battle cameras are picked up.
        Camera currentMainCamera = Camera.main;
        if (currentMainCamera != null && currentMainCamera.isActiveAndEnabled)
        {
            if (targetCamera != currentMainCamera)
                SetTargetCamera(currentMainCamera);
        }
        else if (targetCamera == null || !targetCamera.isActiveAndEnabled)
        {
            SetTargetCamera(FindFallbackCamera());
        }

        if (sun == null || !sun.isActiveAndEnabled)
            sun = FindSun();

        if (cloudPlane == null && targetCamera != null && sun != null)
            CreateCloudShadowPlane();

        if (cloudPlane == null || targetCamera == null)
            return;

        Vector3 cameraPosition = targetCamera.transform.position;

        // Follow only horizontally for coverage. Keep altitude fixed so changing
        // the camera pitch/height does not drag the projected shadow up and down.
        Vector3 nextPlanePosition = new Vector3(
            cameraPosition.x,
            fixedCloudAltitude,
            cameraPosition.z);

        if (hasCameraAnchor)
        {
            Vector3 cameraDelta = nextPlanePosition - cloudPlane.position;

            // Compensate UVs for plane movement. This keeps the cloud pattern
            // anchored in world space instead of making shadows follow the player.
            textureOffset.x += cameraDelta.x / planeSize * cloudTiling;
            textureOffset.y += cameraDelta.z / planeSize * cloudTiling;
        }

        cloudPlane.position = nextPlanePosition;
        hasCameraAnchor = true;

        // Only this deliberate texture motion moves the cloud shadows.
        textureOffset += new Vector2(cloudSideSpeed, cloudSpeed) * Time.deltaTime;
        cloudMaterial.SetTextureOffset(BaseMapId, textureOffset);
    }

    private void SetTargetCamera(Camera cameraToUse)
    {
        if (cameraToUse == null)
            return;

        targetCamera = cameraToUse;

        if (cloudPlane == null)
        {
            fixedCloudAltitude = targetCamera.transform.position.y + ActiveCloudHeight;
        }
        else
        {
            // A newly active camera may be in a different scene or battle setup.
            // Shift coverage while compensating UVs so the pattern stays world-anchored.
            Vector3 oldPosition = cloudPlane.position;
            Vector3 newPosition = new Vector3(
                targetCamera.transform.position.x,
                fixedCloudAltitude,
                targetCamera.transform.position.z);
            Vector3 cameraDelta = newPosition - oldPosition;
            textureOffset.x += cameraDelta.x / planeSize * cloudTiling;
            textureOffset.y += cameraDelta.z / planeSize * cloudTiling;
            cloudPlane.position = newPosition;
            hasCameraAnchor = true;
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        if (cloudMaterial != null)
            cloudMaterial.SetTextureScale(BaseMapId, Vector2.one * cloudTiling);

        if (cloudPlane != null)
        {
            MeshFilter filter = cloudPlane.GetComponent<MeshFilter>();
            if (filter != null)
            {
                Mesh oldMesh = filter.sharedMesh;
                filter.sharedMesh = CreatePlaneMesh(planeSize);
                if (oldMesh != null)
                    Destroy(oldMesh);
            }

            Vector3 position = cloudPlane.position;
            position.y = fixedCloudAltitude = (targetCamera != null
                ? targetCamera.transform.position.y
                : position.y - ActiveCloudHeight) + ActiveCloudHeight;
            cloudPlane.position = position;
        }

        if (sun != null)
            sun.shadowStrength = shadowStrength;
    }

    private Camera FindFallbackCamera()
    {
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
        sun.shadowStrength = shadowStrength;

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
        cloudMaterial.SetTextureScale(BaseMapId, Vector2.one * cloudTiling);

        GameObject planeObject = new GameObject("Cloud Shadow Caster");
        planeObject.transform.SetParent(transform, false);

        MeshFilter meshFilter = planeObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = CreatePlaneMesh(planeSize);

        MeshRenderer meshRenderer = planeObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = cloudMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        cloudPlane = planeObject.transform;
        fixedCloudAltitude = targetCamera.transform.position.y + ActiveCloudHeight;
        cloudPlane.position = new Vector3(
            targetCamera.transform.position.x,
            fixedCloudAltitude,
            targetCamera.transform.position.z);
        hasCameraAnchor = true;

        Debug.Log("[Cloud Shadows] 云影已启动。云块大小、速度和高度可在 Runtime Cloud Shadows 组件中调整。");
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
            name = "Runtime Soft Billow Cloud Mask",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 4
        };

        Color32[] pixels = new Color32[TextureSize * TextureSize];

        for (int y = 0; y < TextureSize; y++)
        {
            float v = (float)y / TextureSize;
            for (int x = 0; x < TextureSize; x++)
            {
                float u = (float)x / TextureSize;
                float warpX = Fbm(u, v, 4, 0.52f);
                float warpY = Fbm(u + 0.317f, v + 0.683f, 4, 0.52f);
                float sampleU = Mathf.Repeat(u + (warpX - 0.5f) * 0.19f, 1f);
                float sampleV = Mathf.Repeat(v + (warpY - 0.5f) * 0.19f, 1f);

                float broad = Fbm(sampleU, sampleV, 4, 0.55f);
                float medium = Fbm(sampleU + 0.11f, sampleV - 0.07f, 5, 0.5f);
                float detail = PeriodicNoise(sampleU, sampleV, 48);
                float cloudValue = broad * 0.70f + medium * 0.25f + detail * 0.05f;

                float edgeNoise = PeriodicNoise(sampleU + 0.23f, sampleV + 0.41f, 96);
                float threshold = 0.485f + (edgeNoise - 0.5f) * 0.045f;
                float alpha = Mathf.SmoothStep(threshold - 0.105f, threshold + 0.105f, cloudValue);
                byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                pixels[y * TextureSize + x] = new Color32(255, 255, 255, a);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(true, true);
        return texture;
    }

    private static float Fbm(float u, float v, int octaves, float persistence)
    {
        float sum = 0f;
        float amplitude = 1f;
        float totalAmplitude = 0f;
        int frequency = 3;
        for (int i = 0; i < octaves; i++)
        {
            sum += PeriodicNoise(u, v, frequency) * amplitude;
            totalAmplitude += amplitude;
            amplitude *= persistence;
            frequency *= 2;
        }
        return sum / totalAmplitude;
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
