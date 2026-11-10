using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 保留现有 Cookie 作为基础图层，叠加可平铺的程序噪声，生成新的云影纹理。
/// 原始纹理不会被修改。
/// </summary>
public sealed class CloudShadowTextureGeneratorWindow : EditorWindow
{
    private const string DefaultSourcePath = "Assets/MYShaders/CloudShadow/CloudShadowCookie.png";
    private const string DefaultOutputPath = "Assets/MYShaders/CloudShadow/CloudShadowCookie_Layered.png";

    [SerializeField] private Texture2D sourceTexture;
    [SerializeField] private string outputPath = DefaultOutputPath;
    [SerializeField] private int outputSize = 1024;
    [SerializeField] private int seed = 17;

    [Header("云团形状")]
    [SerializeField, Range(2, 12)] private int cloudScale = 4;
    [SerializeField, Range(0f, 0.3f)] private float warpStrength = 0.09f;
    [SerializeField, Range(1, 8)] private int warpScale = 3;

    [Header("多层叠加")]
    [SerializeField, Range(0f, 1f)] private float baseLayerInfluence = 0.68f;
    [SerializeField, Range(0f, 1f)] private float broadNoiseInfluence = 0.32f;
    [SerializeField, Range(0f, 0.4f)] private float detailStrength = 0.10f;
    [SerializeField, Range(4, 32)] private int detailScale = 12;
    [SerializeField, Range(0.5f, 2f)] private float contrast = 1.1f;

    private Texture2D generatedTexture;

    [MenuItem("Tools/Cloud Shadow/云影纹理生成器")]
    private static void Open()
    {
        CloudShadowTextureGeneratorWindow window =
            GetWindow<CloudShadowTextureGeneratorWindow>("云影纹理生成器");
        window.minSize = new Vector2(360f, 520f);
        window.Show();
    }

    private void OnEnable()
    {
        if (sourceTexture == null)
            sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultSourcePath);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "以当前 CloudShadowCookie.png 为基础图层，叠加大尺度云团噪声、轮廓扭曲和细节噪声。原图不会被覆盖。",
            MessageType.Info);

        EditorGUILayout.LabelField("基础纹理", EditorStyles.boldLabel);
        sourceTexture = (Texture2D)EditorGUILayout.ObjectField(
            "保留的原始云图", sourceTexture, typeof(Texture2D), false);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("输出设置", EditorStyles.boldLabel);
        outputPath = EditorGUILayout.TextField("生成文件路径", outputPath);
        outputSize = EditorGUILayout.IntPopup(
            "输出分辨率", outputSize,
            new[] { "512", "1024", "2048" },
            new[] { 512, 1024, 2048 });
        seed = EditorGUILayout.IntField("随机种子", seed);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("云团形状", EditorStyles.boldLabel);
        cloudScale = EditorGUILayout.IntSlider("大云团尺度", cloudScale, 2, 12);
        warpScale = EditorGUILayout.IntSlider("轮廓扭曲尺度", warpScale, 1, 8);
        warpStrength = EditorGUILayout.Slider("轮廓不规则程度", warpStrength, 0f, 0.3f);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("多层叠加", EditorStyles.boldLabel);
        baseLayerInfluence = EditorGUILayout.Slider("原图保留比例", baseLayerInfluence, 0f, 1f);
        broadNoiseInfluence = EditorGUILayout.Slider("大尺度云团比例", broadNoiseInfluence, 0f, 1f);
        detailScale = EditorGUILayout.IntSlider("细节噪声尺度", detailScale, 4, 32);
        detailStrength = EditorGUILayout.Slider("内部细节强度", detailStrength, 0f, 0.4f);
        contrast = EditorGUILayout.Slider("整体明暗对比", contrast, 0.5f, 2f);

        EditorGUILayout.Space(10);
        using (new EditorGUI.DisabledScope(sourceTexture == null))
        {
            if (GUILayout.Button("生成云影 PNG", GUILayout.Height(34f)))
                Generate();
        }

        if (generatedTexture != null)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("最近生成的纹理", EditorStyles.boldLabel);
            Rect previewRect = GUILayoutUtility.GetAspectRect(1f, GUILayout.MaxHeight(240f));
            EditorGUI.DrawPreviewTexture(previewRect, generatedTexture, null, ScaleMode.ScaleToFit);
            EditorGUILayout.ObjectField("生成结果", generatedTexture, typeof(Texture2D), false);
        }
    }

    private void Generate()
    {
        if (sourceTexture == null)
        {
            Debug.LogError("[Cloud Cookie Generator] 请先指定基础云图。");
            return;
        }

        if (!sourceTexture.isReadable)
        {
            Debug.LogError("[Cloud Cookie Generator] 基础云图没有启用 Read/Write。请在纹理 Import Settings 中启用后再生成。");
            return;
        }

        if (string.IsNullOrWhiteSpace(outputPath) || !outputPath.Replace('\\\\', '/').StartsWith("Assets/"))
        {
            Debug.LogError("[Cloud Cookie Generator] 输出路径必须是项目 Assets 文件夹内的相对路径，例如 Assets/MYShaders/CloudShadow/CloudShadowCookie_Layered.png。");
            return;
        }

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string absoluteOutputPath = Path.GetFullPath(Path.Combine(projectRoot, outputPath));
        if (!absoluteOutputPath.StartsWith(projectRoot + Path.DirectorySeparatorChar))
        {
            Debug.LogError("[Cloud Cookie Generator] 输出路径不能离开当前 Unity 项目。");
            return;
        }

        string assetFolder = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(assetFolder))
            Directory.CreateDirectory(assetFolder);

        Color32[] pixels = new Color32[outputSize * outputSize];
        float weightSum = Mathf.Max(0.0001f, baseLayerInfluence + broadNoiseInfluence);

        for (int y = 0; y < outputSize; y++)
        {
            float v = y / (float)outputSize;
            for (int x = 0; x < outputSize; x++)
            {
                float u = x / (float)outputSize;

                // 两个独立的周期噪声方向共同扭曲采样坐标，且在纹理边缘保持连续。
                float warpX = PeriodicNoise(u, v, warpScale, seed + 101) - 0.5f;
                float warpY = PeriodicNoise(u, v, warpScale, seed + 307) - 0.5f;
                float warpedU = Mathf.Repeat(u + warpX * warpStrength, 1f);
                float warpedV = Mathf.Repeat(v + warpY * warpStrength, 1f);

                Color source = sourceTexture.GetPixelBilinear(warpedU, warpedV);
                float baseGray = (source.r + source.g + source.b) / 3f;

                float broad = FractalNoise(warpedU, warpedV, cloudScale, seed + 503, 4);
                float detail = FractalNoise(warpedU, warpedV, detailScale, seed + 907, 3);

                float cloud = (baseGray * baseLayerInfluence + broad * broadNoiseInfluence) / weightSum;
                cloud += (detail - 0.5f) * detailStrength;
                cloud = Mathf.Clamp01((cloud - 0.5f) * contrast + 0.5f);

                byte value = (byte)Mathf.RoundToInt(cloud * 255f);
                pixels[y * outputSize + x] = new Color32(value, value, value, 255);
            }
        }

        Texture2D output = new Texture2D(outputSize, outputSize, TextureFormat.RGBA32, false, true)
        {
            name = Path.GetFileNameWithoutExtension(outputPath),
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };
        output.SetPixels32(pixels);
        output.Apply(false, false);

        File.WriteAllBytes(absoluteOutputPath, output.EncodeToPNG());
        DestroyImmediate(output);

        AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(outputPath) as TextureImporter;
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

        generatedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
        Selection.activeObject = generatedTexture;
        EditorGUIUtility.PingObject(generatedTexture);
        Debug.Log("[Cloud Cookie Generator] 已生成云影纹理：" + outputPath +
                  "。原始 CloudShadowCookie.png 未被修改。");
    }

    private static float FractalNoise(float u, float v, int baseFrequency, int noiseSeed, int octaves)
    {
        float value = 0f;
        float amplitude = 1f;
        float amplitudeSum = 0f;
        int frequency = Mathf.Max(1, baseFrequency);

        for (int octave = 0; octave < octaves; octave++)
        {
            value += PeriodicNoise(u, v, frequency, noiseSeed + octave * 7919) * amplitude;
            amplitudeSum += amplitude;
            amplitude *= 0.5f;
            frequency *= 2;
        }

        return value / Mathf.Max(0.0001f, amplitudeSum);
    }

    // 使用周期性格点噪声，而不是普通 PerlinNoise，确保纹理左右、上下边缘可以平铺。
    private static float PeriodicNoise(float u, float v, int period, int noiseSeed)
    {
        period = Mathf.Max(1, period);
        u = Mathf.Repeat(u, 1f) * period;
        v = Mathf.Repeat(v, 1f) * period;

        int x0 = Mathf.FloorToInt(u);
        int y0 = Mathf.FloorToInt(v);
        int x1 = (x0 + 1) % period;
        int y1 = (y0 + 1) % period;

        float tx = Fade(u - x0);
        float ty = Fade(v - y0);

        float a = Hash(x0, y0, noiseSeed);
        float b = Hash(x1, y0, noiseSeed);
        float c = Hash(x0, y1, noiseSeed);
        float d = Hash(x1, y1, noiseSeed);

        float top = Mathf.Lerp(a, b, tx);
        float bottom = Mathf.Lerp(c, d, tx);
        return Mathf.Lerp(top, bottom, ty);
    }

    private static float Fade(float value)
    {
        return value * value * value * (value * (value * 6f - 15f) + 10f);
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
