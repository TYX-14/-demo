using UnityEngine;
using UnityEngine.Rendering;

namespace MyShared.VolumetricLight
{
    /// <summary>
    /// A stylized, locally placed volumetric light beam.
    /// The object's local Y axis is the beam axis; the object origin is the beam center.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class VolumetricLightBeam : MonoBehaviour
    {
        [Header("体积光柱")]
        [Min(0.1f)] public float length = 12f;
        [Min(0.05f)] public float radius = 1.5f;
        [ColorUsage(false, true)] public Color beamColor = new Color(1f, 0.86f, 0.58f, 1f);
        [Min(0f)] public float intensity = 1.4f;
        [Range(0.01f, 1f)] public float density = 0.35f;
        [Range(0.05f, 1f)] public float edgeSoftness = 0.65f;
        [Range(0f, 1f)] public float noiseStrength = 0.12f;
        [Min(0.01f)] public float noiseScale = 1.8f;
        [Range(4, 48)] public int raymarchSteps = 24;

        [Header("地面光斑")]
        public bool showGroundSpot = true;
        [Min(0.1f)] public float groundSpotSize = 4f;
        [Min(0f)] public float groundSpotIntensity = 0.8f;
        [Range(0.05f, 1f)] public float groundSpotSoftness = 0.75f;
        [Tooltip("从光束上端沿光束方向向下检测地面的最大距离。")]
        [Min(0.1f)] public float groundSpotRayDistance = 100f;
        [Tooltip("斜射时光斑椭圆长轴相对短轴的最大拉伸倍数。")]
        [Range(1f, 5f)] public float groundSpotMaxStretch = 3f;
        [Tooltip("参与地面检测的碰撞体层。建议只勾选 Terrain 和 Ground 等地面层。")]
        public LayerMask groundLayers = ~0;

        [Tooltip("是否在光柱中显示缓慢漂浮的光尘。")]
        public bool showLightParticles = true;
        [Min(0f)] public float particleEmissionRate = 16f;
        [Min(0.01f)] public float particleRiseSpeed = 0.45f;
        [Min(0.1f)] public float particleLifetime = 4f;
        [Min(0.001f)] public float particleSize = 0.055f;
        [Min(1)] public int particleMaxCount = 120;
        [ColorUsage(false, true)] public Color particleColor = new Color(1f, 0.82f, 0.48f, 0.9f);
        [Min(0f)] public float particleIntensity = 1.2f;

        [Header("Shader")]
        [SerializeField] private Shader beamShader;
        [SerializeField] private Shader groundSpotShader;
        [SerializeField] private Shader particleShader;

        private MeshRenderer beamRenderer;
        private MeshFilter beamFilter;
        private Material beamMaterial;
        private Material groundMaterial;
        private GameObject groundSpotObject;
        private MeshRenderer groundRenderer;
        private GameObject particleObject;
        private ParticleSystem lightParticleSystem;
        private ParticleSystemRenderer particleRenderer;
        private Material particleMaterial;
        private MaterialPropertyBlock beamProperties;
        private MaterialPropertyBlock groundProperties;

        private static readonly int BeamColorId = Shader.PropertyToID("_BeamColor");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int DensityId = Shader.PropertyToID("_Density");
        private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
        private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
        private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int StepCountId = Shader.PropertyToID("_StepCount");
        private static readonly int SpotColorId = Shader.PropertyToID("_SpotColor");
        private static readonly int SpotIntensityId = Shader.PropertyToID("_SpotIntensity");
        private static readonly int SpotSoftnessId = Shader.PropertyToID("_SpotSoftness");
        private static readonly int SpotNoiseScaleId = Shader.PropertyToID("_NoiseScale");

        private void Reset()
        {
            CacheComponents();
            AssignBuiltinCube();
            RemoveProxyCollider();
            ApplyTransformScale();
        }

        private void OnEnable()
        {
            CacheComponents();
            AssignBuiltinCube();
            RemoveProxyCollider();
            CreateMaterials();
            CreateGroundSpot();
            CreateLightParticles();
            ApplyTransformScale();
            UpdateProperties();
        }

        private void OnDisable()
        {
            ReleaseRuntimeObjects();
        }

        private void OnValidate()
        {
            length = Mathf.Max(0.1f, length);
            radius = Mathf.Max(0.05f, radius);
            groundSpotSize = Mathf.Max(0.1f, groundSpotSize);
            groundSpotRayDistance = Mathf.Max(0.1f, groundSpotRayDistance);
            groundSpotMaxStretch = Mathf.Clamp(groundSpotMaxStretch, 1f, 5f);
            raymarchSteps = Mathf.Clamp(raymarchSteps, 4, 48);

            if (beamFilter == null)
                CacheComponents();

            if (beamFilter != null)
                AssignBuiltinCube();

            ApplyTransformScale();

            if (isActiveAndEnabled && beamMaterial != null)
            {
                CreateGroundSpot();
                UpdateProperties();
            }
        }

        private void LateUpdate()
        {
            ApplyTransformScale();
            UpdateProperties();
            UpdateLightParticles();
        }

        private void CacheComponents()
        {
            if (beamFilter == null) beamFilter = GetComponent<MeshFilter>();
            if (beamRenderer == null) beamRenderer = GetComponent<MeshRenderer>();
        }

        private void AssignBuiltinCube()
        {
            if (beamFilter != null && beamFilter.sharedMesh == null)
                beamFilter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        }

        private void RemoveProxyCollider()
        {
            Collider proxyCollider = GetComponent<Collider>();
            if (proxyCollider == null) return;

            if (Application.isPlaying) Destroy(proxyCollider);
            else DestroyImmediate(proxyCollider);
        }

        private void CreateMaterials()
        {
            if (beamShader == null) beamShader = Shader.Find("MyShared/VolumetricLightBeam");
            if (groundSpotShader == null) groundSpotShader = Shader.Find("MyShared/VolumetricLightGroundSpot");
            if (particleShader == null) particleShader = Shader.Find("MyShared/VolumetricLightParticles");

            if (beamShader != null && (beamMaterial == null || beamMaterial.shader != beamShader))
            {
                DestroyMaterial(beamMaterial);
                beamMaterial = new Material(beamShader) { name = "Volumetric Light Beam (Runtime)" };
                beamMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            if (beamRenderer != null && beamMaterial != null)
                beamRenderer.sharedMaterial = beamMaterial;

            if (groundSpotShader != null && (groundMaterial == null || groundMaterial.shader != groundSpotShader))
            {
                DestroyMaterial(groundMaterial);
                groundMaterial = new Material(groundSpotShader) { name = "Volumetric Light Ground Spot (Runtime)" };
                groundMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private void CreateGroundSpot()
        {
            if (!showGroundSpot)
            {
                if (groundSpotObject != null) groundSpotObject.SetActive(false);
                return;
            }

            if (groundSpotObject == null)
            {
                groundSpotObject = new GameObject("Ground Light Spot");
                groundSpotObject.hideFlags = HideFlags.HideAndDontSave;

                MeshFilter filter = groundSpotObject.AddComponent<MeshFilter>();
                filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                groundRenderer = groundSpotObject.AddComponent<MeshRenderer>();
                groundRenderer.shadowCastingMode = ShadowCastingMode.Off;
                groundRenderer.receiveShadows = false;
            }

            if (groundRenderer != null && groundMaterial != null)
                groundRenderer.sharedMaterial = groundMaterial;

            UpdateGroundSpotTransform();
        }

        private void ApplyTransformScale()
        {
            if (transform == null) return;
            transform.localScale = new Vector3(radius * 2f, length, radius * 2f);
            UpdateGroundSpotTransform();
        }

        private void UpdateGroundSpotTransform()
        {
            if (groundSpotObject == null) return;

            if (!showGroundSpot || groundMaterial == null)
            {
                groundSpotObject.SetActive(false);
                return;
            }

            // 光束沿本地 -Y 方向射出，从光束上端开始检测实际碰撞表面。
            Vector3 beamDirection = -transform.up.normalized;
            Vector3 rayOrigin = transform.position + transform.up * (length * 0.5f);
            if (!Physics.Raycast(rayOrigin, beamDirection, out RaycastHit hit,
                    groundSpotRayDistance, groundLayers, QueryTriggerInteraction.Ignore))
            {
                // 没有真实地面碰撞体时隐藏光斑，避免它悬浮在错误高度。
                groundSpotObject.SetActive(false);
                return;
            }

            Vector3 projectedBeamDirection = Vector3.ProjectOnPlane(beamDirection, hit.normal);
            if (projectedBeamDirection.sqrMagnitude < 0.0001f)
                projectedBeamDirection = Vector3.ProjectOnPlane(transform.forward, hit.normal);

            if (projectedBeamDirection.sqrMagnitude < 0.0001f)
                projectedBeamDirection = Vector3.Cross(hit.normal, Vector3.right);

            projectedBeamDirection.Normalize();

            Transform spot = groundSpotObject.transform;
            spot.position = hit.point + hit.normal * 0.025f;

            // Quad 的本地 Z 朝向地面法线，本地 Y 沿光束在地面上的投影方向。
            spot.rotation = Quaternion.LookRotation(hit.normal, projectedBeamDirection);

            float incidence = Mathf.Abs(Vector3.Dot(beamDirection, hit.normal));
            float stretch = Mathf.Clamp(1f / Mathf.Max(incidence, 0.2f), 1f, groundSpotMaxStretch);
            spot.localScale = new Vector3(groundSpotSize, groundSpotSize * stretch, 1f);

            groundSpotObject.SetActive(true);
        }

        private void CreateLightParticles()
        {
            if (!showLightParticles)
            {
                if (particleObject != null) particleObject.SetActive(false);
                return;
            }

            if (particleObject == null)
            {
                particleObject = new GameObject("Volumetric Light Dust");
                particleObject.hideFlags = HideFlags.HideAndDontSave;
                lightParticleSystem = particleObject.AddComponent<ParticleSystem>();
                particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
                ConfigureParticleSystem();
            }

            if (particleShader == null)
                particleShader = Shader.Find("MyShared/VolumetricLightParticles");

            if (particleShader != null && (particleMaterial == null || particleMaterial.shader != particleShader))
            {
                DestroyMaterial(particleMaterial);
                particleMaterial = new Material(particleShader) { name = "Volumetric Light Particles (Runtime)" };
                particleMaterial.hideFlags = HideFlags.HideAndDontSave;
                particleMaterial.SetFloat("_ParticleIntensity", particleIntensity);
            }

            if (particleRenderer != null && particleMaterial != null)
                particleRenderer.sharedMaterial = particleMaterial;

            particleObject.SetActive(true);
            UpdateLightParticles();
        }

        private void ConfigureParticleSystem()
        {
            if (lightParticleSystem == null) return;

            var main = lightParticleSystem.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = Mathf.Max(1, particleMaxCount);
            main.startLifetime = new ParticleSystem.MinMaxCurve(particleLifetime * 0.65f, particleLifetime * 1.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(particleRiseSpeed * 0.55f, particleRiseSpeed * 1.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(particleSize * 0.65f, particleSize * 1.35f);
            main.startColor = particleColor;
            main.gravityModifier = 0f;
            main.simulationSpeed = 1f;

            var emission = lightParticleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = particleEmissionRate;

            var shape = lightParticleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0f, -length * 0.48f, 0f);
            shape.scale = new Vector3(radius * 1.55f, 0.12f, radius * 1.55f);

            var colorOverLifetime = lightParticleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(1f, 0.9f, 0.68f), 0.5f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.85f, 0.15f),
                    new GradientAlphaKey(0.65f, 0.75f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = lightParticleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.65f),
                new Keyframe(0.25f, 1f),
                new Keyframe(1f, 0.2f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var velocity = lightParticleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.08f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

            if (particleRenderer != null)
            {
                particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                particleRenderer.alignment = ParticleSystemRenderSpace.View;
                particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
                particleRenderer.receiveShadows = false;
                particleRenderer.sortingFudge = 0.1f;
            }

            lightParticleSystem.Clear(true);
            lightParticleSystem.Play(true);
        }

        private void UpdateLightParticles()
        {
            if (particleObject == null || lightParticleSystem == null) return;

            particleObject.transform.SetPositionAndRotation(transform.position, transform.rotation);
            particleObject.transform.localScale = Vector3.one;
            particleObject.SetActive(showLightParticles);

            if (!showLightParticles) return;

            var main = lightParticleSystem.main;
            main.maxParticles = Mathf.Max(1, particleMaxCount);
            main.startLifetime = new ParticleSystem.MinMaxCurve(particleLifetime * 0.65f, particleLifetime * 1.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(particleRiseSpeed * 0.55f, particleRiseSpeed * 1.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(particleSize * 0.65f, particleSize * 1.35f);
            main.startColor = particleColor;

            var emission = lightParticleSystem.emission;
            emission.rateOverTime = particleEmissionRate;

            var shape = lightParticleSystem.shape;
            shape.position = new Vector3(0f, -length * 0.48f, 0f);
            shape.scale = new Vector3(radius * 1.55f, 0.12f, radius * 1.55f);

            if (particleMaterial != null)
                particleMaterial.SetFloat("_ParticleIntensity", particleIntensity);
        }

        private void UpdateProperties()
        {
            if (beamRenderer != null && beamMaterial != null)
            {
                if (beamProperties == null) beamProperties = new MaterialPropertyBlock();
                beamProperties.SetColor(BeamColorId, beamColor);
                beamProperties.SetFloat(IntensityId, intensity);
                beamProperties.SetFloat(DensityId, density);
                beamProperties.SetFloat(EdgeSoftnessId, edgeSoftness);
                beamProperties.SetFloat(NoiseStrengthId, noiseStrength);
                beamProperties.SetFloat(NoiseScaleId, noiseScale);
                beamProperties.SetFloat(StepCountId, raymarchSteps);
                beamRenderer.SetPropertyBlock(beamProperties);
            }

            if (groundRenderer != null && groundMaterial != null)
            {
                if (groundProperties == null) groundProperties = new MaterialPropertyBlock();
                groundProperties.SetColor(SpotColorId, beamColor);
                groundProperties.SetFloat(SpotIntensityId, groundSpotIntensity);
                groundProperties.SetFloat(SpotSoftnessId, groundSpotSoftness);
                groundProperties.SetFloat(SpotNoiseScaleId, noiseScale);
                groundRenderer.SetPropertyBlock(groundProperties);
            }
        }

        private void ReleaseRuntimeObjects()
        {
            DestroyMaterial(beamMaterial);
            DestroyMaterial(groundMaterial);
            DestroyMaterial(particleMaterial);
            beamMaterial = null;
            groundMaterial = null;
            particleMaterial = null;

            if (particleObject != null)
            {
                if (Application.isPlaying) Destroy(particleObject);
                else DestroyImmediate(particleObject);
                particleObject = null;
                lightParticleSystem = null;
                particleRenderer = null;
            }

            if (groundSpotObject != null)
            {
                if (Application.isPlaying) Destroy(groundSpotObject);
                else DestroyImmediate(groundSpotObject);
                groundSpotObject = null;
                groundRenderer = null;
            }

            if (beamRenderer != null && beamRenderer.sharedMaterial != null &&
                beamRenderer.sharedMaterial.name == "Volumetric Light Beam (Runtime)")
                beamRenderer.sharedMaterial = null;
        }

        private static void DestroyMaterial(Material material)
        {
            if (material == null) return;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.35f, 0.8f);
            Vector3 bottom = transform.position - transform.up * (length * 0.5f);
            Vector3 top = transform.position + transform.up * (length * 0.5f);
            Gizmos.DrawWireSphere(bottom, radius);
            Gizmos.DrawWireSphere(top, radius);
            Gizmos.DrawLine(bottom + transform.right * radius, top + transform.right * radius);
            Gizmos.DrawLine(bottom - transform.right * radius, top - transform.right * radius);
            Gizmos.DrawLine(bottom + transform.forward * radius, top + transform.forward * radius);
            Gizmos.DrawLine(bottom - transform.forward * radius, top - transform.forward * radius);
        }
    }
}