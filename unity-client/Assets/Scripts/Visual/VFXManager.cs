using UnityEngine;

namespace TheIsland.Visual
{
    /// <summary>
    /// Singleton VFX Manager for handling particle effects.
    /// Creates procedural particle systems for gift effects.
    /// </summary>
    public class VFXManager : MonoBehaviour
    {
        #region Singleton
        private static VFXManager _instance;
        public static VFXManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<VFXManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("VFXManager");
                        _instance = go.AddComponent<VFXManager>();
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Settings
        [Header("Gold Rain Settings")]
        [SerializeField] private Color goldColor = new Color(1f, 0.84f, 0f); // Gold
        [SerializeField] private int goldParticleCount = 50;
        [SerializeField] private float goldDuration = 2f;

        [Header("Heart Explosion Settings")]
        [SerializeField] private Color heartColor = new Color(1f, 0.2f, 0.3f); // Red/Pink
        [SerializeField] private int heartParticleCount = 30;
        [SerializeField] private float heartDuration = 1.5f;

        [Header("Footstep Dust Settings")]
        [SerializeField] private Color dustColor = new Color(0.6f, 0.5f, 0.4f, 0.5f);
        [SerializeField] private int dustParticleCount = 8;
        [SerializeField] private float dustDuration = 0.5f;

        [Header("Food Poof Settings")]
        [SerializeField] private Color foodColor = new Color(1f, 1f, 1f, 0.7f); // White smoke
        [SerializeField] private int foodParticleCount = 15;
        [SerializeField] private float foodDuration = 1.0f;

        [Header("General Settings")]
        [SerializeField] private float effectScale = 1f;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Play gold coin rain effect at position.
        /// Used for Bits donations.
        /// </summary>
        public void PlayGoldRain(Vector3 position)
        {
            Debug.Log($"[VFXManager] Playing Gold Rain at {position}");
            var ps = CreateGoldRainSystem(position);
            ps.Play();
            Destroy(ps.gameObject, goldDuration + 0.5f);
        }

        /// <summary>
        /// Play heart explosion effect at position.
        /// Used for subscription/heart gifts.
        /// </summary>
        public void PlayHeartExplosion(Vector3 position)
        {
            Debug.Log($"[VFXManager] Playing Heart Explosion at {position}");
            var ps = CreateHeartExplosionSystem(position);
            ps.Play();
            Destroy(ps.gameObject, heartDuration + 0.5f);
        }

        /// <summary>
        /// Spawn footstep dust effect at position.
        /// Used when agents are walking.
        /// </summary>
        public void SpawnFootstepDust(Vector3 position)
        {
            var ps = CreateFootstepDustSystem(position);
            ps.Play();
            Destroy(ps.gameObject, dustDuration + 0.2f);
        }

        /// <summary>
        /// Play food poof effect (white smoke).
        /// Used for feeding.
        /// </summary>
        public void PlayFoodPoof(Vector3 position)
        {
             var ps = CreateFoodPoofSystem(position);
             ps.Play();
             Destroy(ps.gameObject, foodDuration + 0.5f);
        }

        /// <summary>
        /// Play an effect by type name.
        /// </summary>
        public void PlayEffect(string effectType, Vector3 position)
        {
            switch (effectType.ToLower())
            {
                case "bits":
                case "gold":
                case "goldrain":
                    PlayGoldRain(position);
                    break;
                case "heart":
                case "hearts":
                case "sub":
                case "subscription":
                    PlayHeartExplosion(position);
                    break;
                case "food":
                case "feed":
                    PlayFoodPoof(position);
                    break;
                default:
                    // Default to gold rain
                    PlayGoldRain(position);
                    break;
            }
        }
        #endregion

        #region Particle System Creation
        /// <summary>
        /// Create a procedural gold coin rain particle system.
        /// </summary>
        private ParticleSystem CreateGoldRainSystem(Vector3 position)
        {
            GameObject go = new GameObject("GoldRain_VFX");
            go.transform.position = position + Vector3.up * 3f; // Start above

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            // Stop immediately to prevent "duration while playing" warning
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = goldDuration;
            main.startLifetime = 1.5f;
            main.startSpeed = 2f;
            main.startSize = 0.15f * effectScale;
            main.startColor = goldColor;
            main.gravityModifier = 1f;
            main.maxParticles = goldParticleCount;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // Emission - burst at start
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, goldParticleCount)
            });

            // Shape - spread from point
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1f * effectScale;

            // Size over lifetime - shrink slightly
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 1f);
            sizeCurve.AddKey(1f, 0.5f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Color over lifetime - fade out
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(goldColor, 0f), 
                    new GradientColorKey(goldColor, 0.7f) 
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(1f, 0f), 
                    new GradientAlphaKey(1f, 0.5f), 
                    new GradientAlphaKey(0f, 1f) 
                }
            );
            colorOverLifetime.color = gradient;

            // Rotation - spin
            var rotationOverLifetime = ps.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-180f, 180f);

            // Renderer - use default sprite
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = CreateParticleMaterial(goldColor);

            return ps;
        }

        /// <summary>
        /// Create a procedural heart explosion particle system.
        /// </summary>
        private ParticleSystem CreateHeartExplosionSystem(Vector3 position)
        {
            GameObject go = new GameObject("HeartExplosion_VFX");
            go.transform.position = position + Vector3.up * 1.5f;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            // Stop immediately to prevent "duration while playing" warning
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = heartDuration;
            main.startLifetime = 1.2f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5f);
            main.startSize = 0.2f * effectScale;
            main.startColor = heartColor;
            main.gravityModifier = -0.3f; // Float up slightly
            main.maxParticles = heartParticleCount;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // Emission - burst at start
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, heartParticleCount)
            });

            // Shape - explode outwards from sphere
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f * effectScale;

            // Size over lifetime - grow then shrink
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.5f);
            sizeCurve.AddKey(0.3f, 1.2f);
            sizeCurve.AddKey(1f, 0.2f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Color over lifetime - vibrant to fade
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(heartColor, 0f), 
                    new GradientColorKey(new Color(1f, 0.5f, 0.6f), 0.5f),
                    new GradientColorKey(heartColor, 1f) 
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(1f, 0f), 
                    new GradientAlphaKey(1f, 0.4f), 
                    new GradientAlphaKey(0f, 1f) 
                }
            );
            colorOverLifetime.color = gradient;

            // Rotation - gentle spin
            var rotationOverLifetime = ps.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-90f, 90f);

            // Renderer
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = CreateParticleMaterial(heartColor);

            return ps;
        }

        /// <summary>
        /// Create a procedural footstep dust particle system.
        /// </summary>
        private ParticleSystem CreateFootstepDustSystem(Vector3 position)
        {
            GameObject go = new GameObject("FootstepDust_VFX");
            go.transform.position = position;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            // Stop immediately to prevent "duration while playing" warning
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = dustDuration;
            main.startLifetime = 0.4f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f * effectScale, 0.25f * effectScale);
            main.startColor = dustColor;
            main.gravityModifier = -0.1f;
            main.maxParticles = dustParticleCount;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, dustParticleCount)
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.2f * effectScale;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.5f);
            sizeCurve.AddKey(0.5f, 1f);
            sizeCurve.AddKey(1f, 0.3f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(dustColor, 0f),
                    new GradientColorKey(dustColor, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.5f, 0f),
                    new GradientAlphaKey(0.3f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = CreateParticleMaterial(dustColor);

            return ps;
        }

        private ParticleSystem CreateFoodPoofSystem(Vector3 position)
        {
            GameObject go = new GameObject("FoodPoof_VFX");
            go.transform.position = position + Vector3.up * 1.5f;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            // Stop immediately to prevent "duration while playing" warning
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = foodDuration;
            main.startLifetime = 0.8f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f * effectScale, 0.6f * effectScale);
            main.startColor = foodColor;
            main.gravityModifier = -0.05f; // Slight float
            main.maxParticles = foodParticleCount;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            // Burst
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)foodParticleCount) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.4f * effectScale;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.2f);
            sizeCurve.AddKey(0.5f, 1f);
            sizeCurve.AddKey(1f, 0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(foodColor, 0f), new GradientColorKey(foodColor, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = CreateParticleMaterial(foodColor);

            return ps;
        }

        /// <summary>
        /// Create a simple additive particle material.
        /// </summary>
        private Material CreateParticleMaterial(Color color)
        {
            // Use a built-in shader that works well for particles
            Shader shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            Material mat = new Material(shader);
            mat.color = color;
            
            // Enable additive blending for glow effect
            if (shader.name.Contains("Particles"))
            {
                mat.SetFloat("_Mode", 2); // Additive
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            }

            // Use default particle texture
            Texture2D particleTex = CreateDefaultParticleTexture();
            mat.mainTexture = particleTex;

            return mat;
        }

        /// <summary>
        /// Create a simple circular particle texture procedurally.
        /// </summary>
        private Texture2D CreateDefaultParticleTexture()
        {
            int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            
            float center = size / 2f;
            float radius = size / 2f - 1;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01(1f - (dist / radius));
                    alpha = alpha * alpha; // Softer falloff
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
        #endregion
    }
}
