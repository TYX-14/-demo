using UnityEngine;
using UnityEngine.Rendering;

namespace Grass.Script
{
    /// <summary>
    /// Runtime prototype: builds a mesh patch from Unity Terrain height/normal data
    /// and renders the existing Toon/Grass material on that patch.
    /// Add this component to the Terrain GameObject.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Terrain))]
    public class TerrainGrassGenerator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Terrain terrain;
        [SerializeField] private Material grassMaterial;

        [Header("Patch (Terrain local units)")]
        [Tooltip("Center of the patch in normalized Terrain coordinates (0-1).")]
        [SerializeField] private Vector2 centerNormalized = new Vector2(0.5f, 0.5f);
        [Tooltip("Width (X) and depth (Z) of the patch in Terrain local units.")]
        [SerializeField] private Vector2 patchSize = new Vector2(20f, 20f);
        [Tooltip("Vertices per side. Higher values follow small terrain details more closely.")]
        [Range(2, 256)]
        [SerializeField] private int resolution = 32;

        [Header("Generation")]
        [SerializeField] private bool generateOnPlay = true;
        [SerializeField] private string generatedObjectName = "Generated Terrain Grass";

        private GameObject generatedObject;
        private Mesh generatedMesh;

        private void Reset()
        {
            terrain = GetComponent<Terrain>();
        }

        private void Awake()
        {
            if (terrain == null)
                terrain = GetComponent<Terrain>();
        }

        private void Start()
        {
            if (Application.isPlaying && generateOnPlay)
                GenerateGrass();
        }

        [ContextMenu("Generate Terrain Grass")]
        public void GenerateGrass()
        {
            if (terrain == null)
                terrain = GetComponent<Terrain>();

            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("[TerrainGrassGenerator] Terrain or TerrainData is missing.", this);
                return;
            }

            if (grassMaterial == null)
            {
                Debug.LogError("[TerrainGrassGenerator] Assign the existing Grass material first.", this);
                return;
            }

            TerrainData data = terrain.terrainData;
            Vector3 terrainSize = data.size;

            float width = Mathf.Clamp(patchSize.x, 0.1f, terrainSize.x);
            float depth = Mathf.Clamp(patchSize.y, 0.1f, terrainSize.z);
            float centerX = Mathf.Clamp01(centerNormalized.x) * terrainSize.x;
            float centerZ = Mathf.Clamp01(centerNormalized.y) * terrainSize.z;

            float minX = Mathf.Clamp(centerX - width * 0.5f, 0f, terrainSize.x);
            float maxX = Mathf.Clamp(centerX + width * 0.5f, 0f, terrainSize.x);
            float minZ = Mathf.Clamp(centerZ - depth * 0.5f, 0f, terrainSize.z);
            float maxZ = Mathf.Clamp(centerZ + depth * 0.5f, 0f, terrainSize.z);

            int side = Mathf.Max(2, resolution);
            int vertexCount = side * side;
            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector4[] tangents = new Vector4[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[(side - 1) * (side - 1) * 6];

            for (int z = 0; z < side; z++)
            {
                float localZ = Mathf.Lerp(minZ, maxZ, z / (float)(side - 1));
                for (int x = 0; x < side; x++)
                {
                    float localX = Mathf.Lerp(minX, maxX, x / (float)(side - 1));
                    float normalizedX = localX / terrainSize.x;
                    float normalizedZ = localZ / terrainSize.z;
                    int index = z * side + x;

                    float localY = data.GetInterpolatedHeight(normalizedX, normalizedZ);
                    Vector3 normal = data.GetInterpolatedNormal(normalizedX, normalizedZ).normalized;

                    // The grass shader builds its blade basis from NORMAL and TANGENT.
                    Vector3 tangent = Vector3.ProjectOnPlane(Vector3.right, normal);
                    if (tangent.sqrMagnitude < 0.0001f)
                        tangent = Vector3.ProjectOnPlane(Vector3.forward, normal);
                    tangent.Normalize();

                    vertices[index] = new Vector3(localX, localY, localZ);
                    normals[index] = normal;
                    tangents[index] = new Vector4(tangent.x, tangent.y, tangent.z, 1f);
                    uvs[index] = new Vector2(normalizedX, normalizedZ);
                }
            }

            int triangleIndex = 0;
            for (int z = 0; z < side - 1; z++)
            {
                for (int x = 0; x < side - 1; x++)
                {
                    int a = z * side + x;
                    int b = a + side;
                    int c = a + 1;
                    int d = b + 1;

                    // Winding order produces upward-facing triangles in Terrain local space.
                    triangles[triangleIndex++] = a;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = c;

                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = d;
                }
            }

            EnsureGeneratedObject();

            if (generatedMesh != null)
            {
                if (Application.isPlaying)
                    Destroy(generatedMesh);
                else
                    DestroyImmediate(generatedMesh);
            }

            generatedMesh = new Mesh
            {
                name = "Terrain Grass Patch Mesh",
                indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            generatedMesh.vertices = vertices;
            generatedMesh.normals = normals;
            generatedMesh.tangents = tangents;
            generatedMesh.uv = uvs;
            generatedMesh.triangles = triangles;
            generatedMesh.RecalculateBounds();

            // Geometry-shader blades extend beyond the source surface.
            float bladeHeight = Mathf.Max(0f, grassMaterial.GetFloat("_BladeHeight")) +
                                Mathf.Max(0f, grassMaterial.GetFloat("_BladeHeightRandom"));
            float bladeWidth = Mathf.Max(0f, grassMaterial.GetFloat("_BladeWidth")) +
                               Mathf.Max(0f, grassMaterial.GetFloat("_BladeWidthRandom"));
            generatedMesh.bounds.Expand(new Vector3(bladeHeight * 2f + bladeWidth * 2f,
                                                      bladeHeight * 2f + bladeWidth * 2f,
                                                      bladeHeight * 2f + bladeWidth * 2f));

            MeshFilter filter = generatedObject.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = generatedObject.GetComponent<MeshRenderer>();
            filter.sharedMesh = generatedMesh;
            meshRenderer.sharedMaterial = grassMaterial;

            Debug.Log($"[TerrainGrassGenerator] Generated {side}x{side} vertices ({triangles.Length / 3} source triangles) on Terrain '{terrain.name}'.", this);
        }

        [ContextMenu("Clear Generated Terrain Grass")]
        public void ClearGeneratedGrass()
        {
            if (generatedObject != null)
            {
                if (Application.isPlaying)
                    Destroy(generatedObject);
                else
                    DestroyImmediate(generatedObject);
            }

            generatedObject = null;
            generatedMesh = null;
        }

        private void EnsureGeneratedObject()
        {
            if (generatedObject == null)
            {
                Transform existing = transform.Find(generatedObjectName);
                if (existing != null)
                    generatedObject = existing.gameObject;
            }

            if (generatedObject == null)
            {
                generatedObject = new GameObject(generatedObjectName);
                generatedObject.transform.SetParent(transform, false);
            }

            generatedObject.transform.localPosition = Vector3.zero;
            generatedObject.transform.localRotation = Quaternion.identity;
            generatedObject.transform.localScale = Vector3.one;

            if (generatedObject.GetComponent<MeshFilter>() == null)
                generatedObject.AddComponent<MeshFilter>();
            if (generatedObject.GetComponent<MeshRenderer>() == null)
                generatedObject.AddComponent<MeshRenderer>();
        }

        private void OnDestroy()
        {
            if (generatedMesh != null)
            {
                if (Application.isPlaying)
                    Destroy(generatedMesh);
                else
                    DestroyImmediate(generatedMesh);
            }
        }
    }
}
