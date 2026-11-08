using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 在地形表面铺设程序化流动云影。地面最好具有 TerrainCollider 或其他 Collider。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CloudShadowProjector : MonoBehaviour
{
    [Header("云影范围")]
    [Min(1f)] public float 覆盖宽度 = 120f;
    [Range(8, 128)] public int 网格分辨率 = 48;
    [Min(0f)] public float 离地高度 = 0.08f;

    [Header("地面采样")]
    public LayerMask 地面图层 = ~0;
    [Min(1f)] public float 射线起点高度 = 300f;
    [Min(1f)] public float 射线长度 = 600f;
    [Min(0.1f)] public float 地形刷新间隔 = 2f;

    [Header("云影材质")]
    public Material 云影材质;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh shadowMesh;
    private Vector3[] vertices;
    private float refreshTimer;
    private bool runtimeCreatedMaterial;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        if (云影材质 == null)
        {
            Shader shader = Shader.Find("Custom/CloudShadowOverlay");
            if (shader != null)
            {
                云影材质 = new Material(shader);
                runtimeCreatedMaterial = true;
            }
            else
            {
                Debug.LogError("[云影投射器] 找不到 Shader：Custom/CloudShadowOverlay。请检查 Shader 是否成功导入。", this);
            }
        }

        if (云影材质 == null)
        {
            Debug.LogError("[云影投射器] 没有可用材质，云影无法渲染。请把云影材质拖入组件。", this);
            enabled = false;
            return;
        }

        meshRenderer.sharedMaterial = 云影材质;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        创建网格();
        更新地面高度(true);
    }

    private void Update()
    {
        refreshTimer += Time.deltaTime;
        if (refreshTimer >= 地形刷新间隔)
        {
            refreshTimer = 0f;
            更新地面高度(false);
        }
    }

    private void 创建网格()
    {
        int cells = Mathf.Max(2, 网格分辨率);
        int side = cells + 1;
        vertices = new Vector3[side * side];
        int[] triangles = new int[cells * cells * 6];
        float half = 覆盖宽度 * 0.5f;

        for (int z = 0; z < side; z++)
        for (int x = 0; x < side; x++)
        {
            int index = z * side + x;
            vertices[index] = new Vector3(
                Mathf.Lerp(-half, half, x / (float)cells), 离地高度,
                Mathf.Lerp(-half, half, z / (float)cells));
        }

        int t = 0;
        for (int z = 0; z < cells; z++)
        for (int x = 0; x < cells; x++)
        {
            int a = z * side + x;
            int b = a + 1;
            int c = a + side;
            int d = c + 1;
            triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
            triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
        }

        shadowMesh = new Mesh { name = "云影投射网格" };
        shadowMesh.vertices = vertices;
        shadowMesh.triangles = triangles;
        shadowMesh.RecalculateNormals();
        shadowMesh.RecalculateBounds();
        meshFilter.sharedMesh = shadowMesh;
    }

    private void 更新地面高度(bool 输出日志)
    {
        if (vertices == null || shadowMesh == null) return;

        int side = Mathf.Max(2, 网格分辨率) + 1;
        int hitCount = 0;
        int missCount = 0;

        for (int z = 0; z < side; z++)
        for (int x = 0; x < side; x++)
        {
            int index = z * side + x;
            Vector3 local = vertices[index];
            Vector3 worldXZ = transform.TransformPoint(new Vector3(local.x, 0f, local.z));
            Vector3 origin = new Vector3(worldXZ.x, transform.position.y + 射线起点高度, worldXZ.z);

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                射线长度, 地面图层, QueryTriggerInteraction.Ignore))
            {
                Vector3 localHit = transform.InverseTransformPoint(hit.point);
                vertices[index].y = localHit.y + 离地高度;
                hitCount++;
            }
            else
            {
                // 射线漏检时保留网格在组件所在高度，不再把顶点沉入地下数百米。
                vertices[index].y = 离地高度;
                missCount++;
            }
        }

        shadowMesh.vertices = vertices;
        shadowMesh.RecalculateNormals();
        shadowMesh.RecalculateBounds();

        if (输出日志)
        {
            if (hitCount > 0)
                Debug.Log($"[云影投射器] 网格已生成。地面命中 {hitCount} 个采样点，未命中 {missCount} 个。", this);
            else
                Debug.LogWarning($"[云影投射器] 网格已生成，但 {missCount} 个采样点全部未命中地面。云影会暂时保持在组件高度。请检查地面 Collider、地面图层、射线起点高度和射线长度。", this);
        }
    }

    private void OnDestroy()
    {
        if (shadowMesh != null)
        {
            if (Application.isPlaying) Destroy(shadowMesh);
            else DestroyImmediate(shadowMesh);
        }

        if (runtimeCreatedMaterial && 云影材质 != null)
        {
            if (Application.isPlaying) Destroy(云影材质);
            else DestroyImmediate(云影材质);
        }
    }
}