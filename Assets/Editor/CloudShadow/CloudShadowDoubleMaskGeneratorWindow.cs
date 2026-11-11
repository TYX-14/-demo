using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 使用主体云影和独立破碎遮罩相乘，生成双层遮罩 Cookie。
/// 不修改原图，也不修改现有生成器。
/// </summary>
public sealed class CloudShadowDoubleMaskGeneratorWindow : EditorWindow
{
    private const string SourcePath = "Assets/MYShaders/CloudShadow/CloudShadowCookie.png";
    private const string OutputPath = "Assets/MYShaders/CloudShadow/CloudShadowCookie_DoubleMask.png";

    [SerializeField] private Texture2D sourceTexture;
    [SerializeField] private string outputPath = OutputPath;
    [SerializeField] private int resolution = 1024;
    [SerializeField] private int seed = 41;

    [Header("第一层：云影主体")]
    [SerializeField, Range(1, 12)] private int bodyScale = 4;
    [SerializeField, Range(0f, 0.3f)] private float bodyWarp = 0.10f;
    [SerializeField, Range(0f, 1f)] private float sourceInfluence = 0.65f;

    [Header("第二层：破碎遮罩")]
    [SerializeField, Range(1, 20)] private int maskScale = 7;
    [SerializeField, Range(0f, 0.5f)] private float maskWarp = 0.18f;
    [SerializeField, Range(0f, 1f)] private float breakupStrength = 0.75f;
    [SerializeField, Range(0f, 1f)] private float maskThreshold = 0.42f;
    [SerializeField, Range(0.02f, 0.5f)] private float maskSoftness = 0.18f;

    [Header("整体调整")]
    [SerializeField, Range(0.5f, 2f)] private float contrast = 1.0f;

    private Texture2D previewTexture;

    [MenuItem("Tools/Cloud Shadow/双层遮罩生成器")]
    private static void Open()
    {
        var window = GetWindow<CloudShadowDoubleMaskGeneratorWindow>("双层遮罩生成器");
        window.minSize = new Vector2(360f, 560f);
        window.Show();
    }

    private void OnEnable()
    {
        if (sourceTexture == null)
            sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(SourcePath);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "第一层决定云影主体；第二层使用独立噪声遮罩，打断云影内部连续区域。两层使用不同的坐标扭曲，避免规则的行列排列。原图和旧生成器均不会被修改。",
            MessageType.Info);

        sourceTexture = (Texture2D)EditorGUILayout.ObjectField(
            "基础云图", sourceTexture, typeof(Texture2D), false);
        outputPath = EditorGUILayout.TextField("输出路径", outputPath);
        resolution = EditorGUILayout.IntPopup("分辨率", resolution,
            new[] { "512", "1024", "2048" }, new[] { 512, 1024, 2048 });
        seed = EditorGUILayout.IntField("随机种子", seed);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("第一层：云影主体", EditorStyles.boldLabel);
        bodyScale = EditorGUILayout.IntSlider("主体云团尺度", bodyScale, 1, 12);
        bodyWarp = EditorGUILayout.Slider("主体轮廓扭曲", bodyWarp, 0f, 0.3f);
        sourceInfluence = EditorGUILayout.Slider("原图影响比例", sourceInfluence, 0f, 1f);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("第二层：破碎遮罩", EditorStyles.boldLabel);
        maskScale = EditorGUILayout.IntSlider("遮罩尺度", maskScale, 1, 20);
        maskWarp = EditorGUILayout.Slider("遮罩形变", maskWarp, 0f, 0.5f);
        breakupStrength = EditorGUILayout.Slider("打碎强度", breakupStrength, 0f, 1f);
        maskThreshold = EditorGUILayout.Slider("遮罩覆盖阈值", maskThreshold, 0f, 1f);
        maskSoftness = EditorGUILayout.Slider("遮罩边缘柔和度", maskSoftness, 0.02f, 0.5f);

        EditorGUILayout.Space(8);
        contrast = EditorGUILayout.Slider("整体对比度", contrast, 0.5f, 2f);

        EditorGUILayout.Space(10);
        using (new EditorGUI.DisabledScope(sourceTexture == null))
        {
            if (GUILayout.Button("生成双层遮罩 PNG", GUILayout.Height(36f)))
                Generate();
        }

        if (previewTexture != null)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("生成预览", EditorStyles.boldLabel);
            Rect rect = GUILayoutUtility.GetAspectRect(1f, GUILayout.MaxHeight(240f));
            EditorGUI.DrawPreviewTexture(rect, previewTexture, null, ScaleMode.ScaleToFit);
            EditorGUILayout.ObjectField("生成文件", previewTexture, typeof(Texture2D), false);
        }
    }

    private void Generate()
    {
        if (sourceTexture == null || !sourceTexture.isReadable)
        {
            Debug.LogError("[Cloud Cookie Double Mask] 请指定已开启 Read/Write 的基础云图。");
            return;
        }

        string normalizedPath = outputPath.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalizedPath) || !normalizedPath.StartsWith("Assets/") ||
            Path.GetExtension(normalizedPath).ToLowerInvariant() != ".png")
        {
            Debug.LogError("[Cloud Cookie Double Mask] 输出路径必须是 Assets 文件夹内的 PNG 路径。");
            return;
        }

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string absolutePath = Path.GetFullPath(Path.Combine(projectRoot, normalizedPath));
        string assetsRoot = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
        if (!absolutePath.StartsWith(assetsRoot))
        {
            Debug.LogError("[Cloud Cookie Double Mask] 输出路径不能离开 Assets 文件夹。");
            return;
        }

        string folder = Path.GetDirectoryName(normalizedPath);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        var pixels = new Color32[resolution * resolution];
        float softness = Mathf.Max(0.001f, maskSoftness);

        for (int y = 0; y < resolution; y++)
        {
            float v = y / (float)resolution;
            for (int x = 0; x < resolution; x++)
            {
                float u = x / (float)resolution;

                // 主体层有自己的低频坐标扭曲。
                float bodyU = Mathf.Repeat(u + (Noise(u, v, 3, seed + 11) - 0.5f) * bodyWarp, 1f);
                float bodyV = Mathf.Repeat(v + (Noise(u, v, 3, seed + 23) - 0.5f) * bodyWarp, 1f);
                float source = sourceTexture.GetPixelBilinear(bodyU, bodyV).grayscale;
                float bodyNoise = Fractal(bodyU, bodyV, bodyScale, seed + 101, 4);
                float body = Mathf.Lerp(bodyNoise, source, sourceInfluence);

                // 破碎层使用另一套种子、尺度和扭曲，不与主体层共用排列。
                float maskU = Mathf.Repeat(u + (Noise(u, v, 5, seed + 307) - 0.5f) * maskWarp, 1f);
                float maskV = Mathf.Repeat(v + (Noise(u, v, 5, seed + 401) - 0.5f) * maskWarp, 1f);
                float maskNoise = Fractal(maskU, maskV, maskScale, seed + 503, 3);
                float mask = SmoothStep(maskThreshold - softness, maskThreshold + softness, maskNoise);

                // Cookie 白色保留直射光，暗色削弱直射光。
                // 遮罩只作用于主体云影的暗度，因此低遮罩区域会被“打亮”，形成断裂。
                float shadowAmount = 1f - body;
                shadowAmount *= Mathf.Lerp(1f, mask, breakupStrength);
                float result = 1f - shadowAmount;
                result = Mathf.Clamp01((result - 0.5f) * contrast + 0.5f);

                byte value = (byte)Mathf.RoundToInt(result * 255f);
                pixels[y * resolution + x] = new Color32(value, value, value, 255);
            }
        }

        var output = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true)
        {
            name = Path.GetFileNameWithoutExtension(normalizedPath),
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };
        output.SetPixels32(pixels);
        output.Apply(false, false);
        File.WriteAllBytes(absolutePath, output.EncodeToPNG());
        DestroyImmediate(output);

        AssetDatabase.ImportAsset(normalizedPath, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(normalizedPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.isReadable = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        previewTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalizedPath);
        Selection.activeObject = previewTexture;
        EditorGUIUtility.PingObject(previewTexture);
        Debug.Log("[Cloud Cookie Double Mask] 已生成：" + normalizedPath +
                  "。原始云图及旧版生成器均未修改。");
    }

    private static float Fractal(float u, float v, int frequency, int noiseSeed, int octaves)
    {
        float sum = 0f, weight = 1f, total = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += Noise(u, v, frequency, noiseSeed + i * 7919) * weight;
            total += weight;
            weight *= 0.5f;
            frequency *= 2;
        }
        return sum / Mathf.Max(0.0001f, total);
    }

    // 周期性 value noise，保证生成纹理可 Repeat 平铺。
    private static float Noise(float u, float v, int period, int noiseSeed)
    {
        period = Mathf.Max(1, period);
        float px = Mathf.Repeat(u, 1f) * period;
        float py = Mathf.Repeat(v, 1f) * period;
        int x0 = Mathf.FloorToInt(px), y0 = Mathf.FloorToInt(py);
        int x1 = (x0 + 1) % period, y1 = (y0 + 1) % period;
        float tx = Fade(px - x0), ty = Fade(py - y0);
        float a = Hash(x0, y0, noiseSeed), b = Hash(x1, y0, noiseSeed);
        float c = Hash(x0, y1, noiseSeed), d = Hash(x1, y1, noiseSeed);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        float t = Mathf.Clamp01((value - edge0) / Mathf.Max(0.0001f, edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    private static float Hash(int x, int y, int noiseSeed)
    {
        unchecked
        {
            uint value = (uint)(x * 374761393 + y * 668265263 + noiseSeed * 1442695041);
            value = (value ^ (value >> 13)) * 1274126177;
            value ^= value >> 16;
            return value / (float)uint.MaxValue;
        }
    }
}
