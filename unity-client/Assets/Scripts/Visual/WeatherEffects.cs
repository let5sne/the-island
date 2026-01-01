using UnityEngine;
using TheIsland.Network;
using TheIsland.Models;

namespace TheIsland.Visual
{
    /// <summary>
    /// Creates and manages weather particle effects.
    /// Responds to weather changes from the server.
    /// </summary>
    public class WeatherEffects : MonoBehaviour
    {
        #region Singleton
        private static WeatherEffects _instance;
        public static WeatherEffects Instance => _instance;
        #endregion

        #region Configuration
        [Header("Rain Settings")]
        [SerializeField] private int rainParticleCount = 500;
        [SerializeField] private int stormParticleCount = 1000;
        [SerializeField] private Color rainColor = new Color(0.7f, 0.8f, 0.9f, 0.6f);

        [Header("Sun Settings")]
        [SerializeField] private int sunRayCount = 50;
        [SerializeField] private Color sunRayColor = new Color(1f, 0.95f, 0.8f, 0.3f);

        [Header("Fog Settings")]
        [SerializeField] private Color fogColor = new Color(0.85f, 0.85f, 0.9f, 0.5f);

        [Header("Hot Weather Settings")]
        [SerializeField] private int heatWaveCount = 30;
        [SerializeField] private Color heatColor = new Color(1f, 0.9f, 0.7f, 0.2f);
        #endregion

        #region References
        private ParticleSystem _rainSystem;
        private ParticleSystem _sunRaySystem;
        private ParticleSystem _fogSystem;
        private ParticleSystem _heatSystem;
        private ParticleSystem _cloudSystem;

        private string _currentWeather = "Sunny";
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

            CreateAllEffects();
        }

        private void Start()
        {
            var network = NetworkManager.Instance;
            if (network != null)
            {
                network.OnWeatherChange += HandleWeatherChange;
                network.OnTick += HandleTick;
            }

            // Start with sunny weather
            SetWeather("Sunny");
        }

        private void OnDestroy()
        {
            var network = NetworkManager.Instance;
            if (network != null)
            {
                network.OnWeatherChange -= HandleWeatherChange;
                network.OnTick -= HandleTick;
            }
        }
        #endregion

        #region Effect Creation
        private void CreateAllEffects()
        {
            CreateRainEffect();
            CreateSunRayEffect();
            CreateFogEffect();
            CreateHeatEffect();
            CreateCloudEffect();
        }

        private void CreateRainEffect()
        {
            var rainObj = new GameObject("RainEffect");
            rainObj.transform.SetParent(transform);
            rainObj.transform.position = new Vector3(0, 10, 5);

            _rainSystem = rainObj.AddComponent<ParticleSystem>();
            var main = _rainSystem.main;
            main.maxParticles = stormParticleCount;
            main.startLifetime = 1.5f;
            main.startSpeed = 15f;
            main.startSize = 0.05f;
            main.startColor = rainColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1.5f;

            var emission = _rainSystem.emission;
            emission.rateOverTime = rainParticleCount;

            var shape = _rainSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(25, 0.1f, 15);

            // Renderer settings
            var renderer = rainObj.GetComponent<ParticleSystemRenderer>();
            renderer.material = CreateParticleMaterial(rainColor);
            renderer.sortingOrder = 50;

            // Start stopped
            _rainSystem.Stop();
        }

        private void CreateSunRayEffect()
        {
            var sunObj = new GameObject("SunRayEffect");
            sunObj.transform.SetParent(transform);
            sunObj.transform.position = new Vector3(5, 8, 10);
            sunObj.transform.rotation = Quaternion.Euler(45, -30, 0);

            _sunRaySystem = sunObj.AddComponent<ParticleSystem>();
            var main = _sunRaySystem.main;
            main.maxParticles = sunRayCount;
            main.startLifetime = 3f;
            main.startSpeed = 0.5f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            main.startColor = sunRayColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = _sunRaySystem.emission;
            emission.rateOverTime = 10;

            var shape = _sunRaySystem.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15;
            shape.radius = 3;

            var colorOverLifetime = _sunRaySystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new GradientAlphaKey[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.3f, 0.3f), new GradientAlphaKey(0, 1) }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = _sunRaySystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0, 0.5f), new Keyframe(0.5f, 1f), new Keyframe(1, 1.5f)));

            var renderer = sunObj.GetComponent<ParticleSystemRenderer>();
            renderer.material = CreateSunRayMaterial();
            renderer.sortingOrder = 40;

            _sunRaySystem.Stop();
        }

        private void CreateFogEffect()
        {
            var fogObj = new GameObject("FogEffect");
            fogObj.transform.SetParent(transform);
            fogObj.transform.position = new Vector3(0, 1, 5);

            _fogSystem = fogObj.AddComponent<ParticleSystem>();
            var main = _fogSystem.main;
            main.maxParticles = 100;
            main.startLifetime = 8f;
            main.startSpeed = 0.3f;
            main.startSize = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startColor = fogColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = _fogSystem.emission;
            emission.rateOverTime = 5;

            var shape = _fogSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30, 2, 15);

            var velocityOverLifetime = _fogSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);

            var colorOverLifetime = _fogSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new GradientAlphaKey[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.5f, 0.3f), new GradientAlphaKey(0.5f, 0.7f), new GradientAlphaKey(0, 1) }
            );
            colorOverLifetime.color = gradient;

            var renderer = fogObj.GetComponent<ParticleSystemRenderer>();
            renderer.material = CreateFogMaterial();
            renderer.sortingOrder = 30;

            _fogSystem.Stop();
        }

        private void CreateHeatEffect()
        {
            var heatObj = new GameObject("HeatEffect");
            heatObj.transform.SetParent(transform);
            heatObj.transform.position = new Vector3(0, 0, 5);

            _heatSystem = heatObj.AddComponent<ParticleSystem>();
            var main = _heatSystem.main;
            main.maxParticles = heatWaveCount;
            main.startLifetime = 4f;
            main.startSpeed = 0.8f;
            main.startSize = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startColor = heatColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = _heatSystem.emission;
            emission.rateOverTime = 8;

            var shape = _heatSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(20, 0.1f, 10);

            var velocityOverLifetime = _heatSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.y = 1f;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

            var colorOverLifetime = _heatSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(heatColor, 0), new GradientColorKey(heatColor, 1) },
                new GradientAlphaKey[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.2f, 0.3f), new GradientAlphaKey(0, 1) }
            );
            colorOverLifetime.color = gradient;

            var renderer = heatObj.GetComponent<ParticleSystemRenderer>();
            renderer.material = CreateHeatMaterial();
            renderer.sortingOrder = 35;

            _heatSystem.Stop();
        }

        private void CreateCloudEffect()
        {
            var cloudObj = new GameObject("CloudEffect");
            cloudObj.transform.SetParent(transform);
            cloudObj.transform.position = new Vector3(0, 8, 15);

            _cloudSystem = cloudObj.AddComponent<ParticleSystem>();
            var main = _cloudSystem.main;
            main.maxParticles = 30;
            main.startLifetime = 20f;
            main.startSpeed = 0.2f;
            main.startSize = new ParticleSystem.MinMaxCurve(5f, 10f);
            main.startColor = new Color(1, 1, 1, 0.7f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = _cloudSystem.emission;
            emission.rateOverTime = 1;

            var shape = _cloudSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40, 2, 5);

            var velocityOverLifetime = _cloudSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.x = 0.3f;

            var renderer = cloudObj.GetComponent<ParticleSystemRenderer>();
            renderer.material = CreateCloudMaterial();
            renderer.sortingOrder = 25;

            _cloudSystem.Stop();
        }
        #endregion

        #region Material Creation
        private Material CreateParticleMaterial(Color color)
        {
            Material mat = new Material(Shader.Find("Particles/Standard Unlit"));
            mat.SetColor("_Color", color);
            mat.SetFloat("_Mode", 2); // Fade mode

            // Create simple white texture
            Texture2D tex = new Texture2D(8, 8);
            for (int i = 0; i < 64; i++) tex.SetPixel(i % 8, i / 8, Color.white);
            tex.Apply();
            mat.mainTexture = tex;

            return mat;
        }

        private Material CreateSunRayMaterial()
        {
            Material mat = new Material(Shader.Find("Particles/Standard Unlit"));
            mat.SetColor("_Color", sunRayColor);
            mat.SetFloat("_Mode", 1); // Additive

            // Create soft gradient texture
            Texture2D tex = new Texture2D(32, 32);
            Vector2 center = new Vector2(16, 16);
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / 16f;
                    float alpha = Mathf.Clamp01(1 - dist);
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha * alpha));
                }
            }
            tex.Apply();
            mat.mainTexture = tex;

            return mat;
        }

        private Material CreateFogMaterial()
        {
            Material mat = new Material(Shader.Find("Particles/Standard Unlit"));
            mat.SetColor("_Color", fogColor);
            mat.SetFloat("_Mode", 2); // Fade

            // Create soft cloud texture
            Texture2D tex = new Texture2D(64, 64);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float noise = Mathf.PerlinNoise(x * 0.1f, y * 0.1f);
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32)) / 32f;
                    float alpha = Mathf.Clamp01((1 - dist) * noise);
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }
            tex.Apply();
            mat.mainTexture = tex;

            return mat;
        }

        private Material CreateHeatMaterial()
        {
            Material mat = new Material(Shader.Find("Particles/Standard Unlit"));
            mat.SetColor("_Color", heatColor);
            mat.SetFloat("_Mode", 1); // Additive

            // Create wavy heat texture
            Texture2D tex = new Texture2D(32, 64);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float wave = Mathf.Sin((x + y * 0.3f) * 0.3f) * 0.5f + 0.5f;
                    float fade = 1 - Mathf.Abs(x - 16) / 16f;
                    float alpha = wave * fade * (1 - y / 64f);
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha * 0.3f));
                }
            }
            tex.Apply();
            mat.mainTexture = tex;

            return mat;
        }

        private Material CreateCloudMaterial()
        {
            Material mat = new Material(Shader.Find("Particles/Standard Unlit"));
            mat.SetColor("_Color", Color.white);
            mat.SetFloat("_Mode", 2); // Fade

            // Create fluffy cloud texture
            Texture2D tex = new Texture2D(64, 64);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float noise1 = Mathf.PerlinNoise(x * 0.08f, y * 0.08f);
                    float noise2 = Mathf.PerlinNoise(x * 0.15f + 100, y * 0.15f + 100) * 0.5f;
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32)) / 32f;
                    float alpha = Mathf.Clamp01((noise1 + noise2) * (1 - dist * dist));
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha * 0.8f));
                }
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            mat.mainTexture = tex;

            return mat;
        }
        #endregion

        #region Weather Control
        private void HandleWeatherChange(WeatherChangeData data)
        {
            SetWeather(data.new_weather);
        }

        private void HandleTick(TickData data)
        {
            if (!string.IsNullOrEmpty(data.weather) && data.weather != _currentWeather)
            {
                SetWeather(data.weather);
            }
        }

        public void SetWeather(string weather)
        {
            _currentWeather = weather;

            // Stop all effects first
            _rainSystem?.Stop();
            _sunRaySystem?.Stop();
            _fogSystem?.Stop();
            _heatSystem?.Stop();
            _cloudSystem?.Stop();

            // Enable appropriate effects
            switch (weather)
            {
                case "Sunny":
                    _sunRaySystem?.Play();
                    break;

                case "Cloudy":
                    _cloudSystem?.Play();
                    break;

                case "Rainy":
                    _rainSystem?.Play();
                    var rainMain = _rainSystem.main;
                    var rainEmission = _rainSystem.emission;
                    rainEmission.rateOverTime = rainParticleCount;
                    _cloudSystem?.Play();
                    break;

                case "Stormy":
                    _rainSystem?.Play();
                    var stormMain = _rainSystem.main;
                    var stormEmission = _rainSystem.emission;
                    stormEmission.rateOverTime = stormParticleCount;
                    stormMain.startSpeed = 20f;
                    _cloudSystem?.Play();
                    // Could add lightning flashes here
                    break;

                case "Foggy":
                    _fogSystem?.Play();
                    break;

                case "Hot":
                    _heatSystem?.Play();
                    _sunRaySystem?.Play();
                    break;
            }

            Debug.Log($"[WeatherEffects] Weather set to: {weather}");
        }
        #endregion
    }
}
