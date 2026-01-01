using System.Collections.Generic;
using UnityEngine;
using TheIsland.Core;
using TheIsland.Network;
using TheIsland.Models;

namespace TheIsland.Visual
{
    /// <summary>
    /// Manages the island environment visuals including sky, ground, water, and lighting.
    /// Creates a beautiful dynamic background that responds to time of day and weather.
    /// </summary>
    public class EnvironmentManager : MonoBehaviour
    {
        #region Singleton
        private static EnvironmentManager _instance;
        public static EnvironmentManager Instance => _instance;
        #endregion

        #region Sky Colors by Time of Day
        [Header("Dawn Colors")]
        [SerializeField] private Color dawnSkyTop = new Color(0.98f, 0.65f, 0.45f);
        [SerializeField] private Color dawnSkyBottom = new Color(1f, 0.85f, 0.6f);
        [SerializeField] private Color dawnAmbient = new Color(1f, 0.8f, 0.6f);

        [Header("Day Colors")]
        [SerializeField] private Color daySkyTop = new Color(0.4f, 0.7f, 1f);
        [SerializeField] private Color daySkyBottom = new Color(0.7f, 0.9f, 1f);
        [SerializeField] private Color dayAmbient = new Color(1f, 1f, 0.95f);

        [Header("Dusk Colors")]
        [SerializeField] private Color duskSkyTop = new Color(0.3f, 0.2f, 0.5f);
        [SerializeField] private Color duskSkyBottom = new Color(1f, 0.5f, 0.3f);
        [SerializeField] private Color duskAmbient = new Color(1f, 0.6f, 0.4f);

        [Header("Night Colors")]
        [SerializeField] private Color nightSkyTop = new Color(0.05f, 0.05f, 0.15f);
        [SerializeField] private Color nightSkyBottom = new Color(0.1f, 0.15f, 0.3f);
        [SerializeField] private Color nightAmbient = new Color(0.3f, 0.35f, 0.5f);
        #endregion

        #region Weather Modifiers
        [Header("Weather Color Modifiers")]
        [SerializeField] private Color cloudyTint = new Color(0.7f, 0.7f, 0.75f);
        [SerializeField] private Color rainyTint = new Color(0.5f, 0.55f, 0.6f);
        [SerializeField] private Color stormyTint = new Color(0.35f, 0.35f, 0.4f);
        [SerializeField] private Color foggyTint = new Color(0.8f, 0.8f, 0.85f);
        [SerializeField] private Color hotTint = new Color(1.1f, 0.95f, 0.85f);
        #endregion

        #region Ground & Water
        [Header("Ground Settings")]
        [SerializeField] private Color sandColor = new Color(0.95f, 0.87f, 0.7f);
        [SerializeField] private Color sandDarkColor = new Color(0.8f, 0.7f, 0.5f);

        [Header("Water Settings")]
        [SerializeField] private Color waterShallowColor = new Color(0.3f, 0.8f, 0.9f, 0.8f);
        [SerializeField] private Color waterDeepColor = new Color(0.1f, 0.4f, 0.6f, 0.9f);
        [SerializeField] private float waveSpeed = 0.5f;
        [SerializeField] private Material customWaterMaterial; // Custom shader support
        #endregion

        #region References
        private Camera _mainCamera;
        private Material _skyMaterial;
        private GameObject _groundPlane;
        private GameObject _waterPlane;
        private Material _groundMaterial;
        private Material _waterMaterial;
        private Light _mainLight;

        // Current state
        private string _currentTimeOfDay = "day";
        private string _currentWeather = "Sunny";
        private float _transitionProgress = 1f;
        private Color _targetSkyTop, _targetSkyBottom;
        private Color _currentSkyTop, _currentSkyBottom;
        private List<Transform> _palmTrees = new List<Transform>();
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

            LoadEnvironmentTexture();
            _mainCamera = Camera.main;
            CreateEnvironment();
        }

        private void Start()
        {
            // Subscribe to network events
            var network = NetworkManager.Instance;
            if (network != null)
            {
                network.OnPhaseChange += HandlePhaseChange;
                network.OnWeatherChange += HandleWeatherChange;
                network.OnTick += HandleTick;
            }

            // Set initial sky
            UpdateSkyColors();

            // Phase 19-B: Cache palm trees for animation
            CachePalmTrees();

            // Phase 19: Add Visual Effects Manager
            if (FindFirstObjectByType<VisualEffectsManager>() == null)
            {
                new GameObject("VisualEffectsManager").AddComponent<VisualEffectsManager>();
            }
        }

        private void Update()
        {
            // Smooth sky transition
            if (_transitionProgress < 1f)
            {
                _transitionProgress += Time.deltaTime * 0.5f;
                _currentSkyTop = Color.Lerp(_currentSkyTop, _targetSkyTop, _transitionProgress);
                _currentSkyBottom = Color.Lerp(_currentSkyBottom, _targetSkyBottom, _transitionProgress);
                UpdateSkyMaterial();
            }

            // Phase 19: Cinematic Lighting
            AnimateLighting();

            // Animate environment (Water & Trees)
            AnimateEnvironment();
            AnimateClouds();
        }

        private void AnimateLighting()
        {
            if (_mainLight == null) return;

            // Simple 120s cycle for demonstration (30s per phase)
            float cycleDuration = 120f;
            float t = (Time.time % cycleDuration) / cycleDuration;

            // t: 0=Dawn, 0.25=Noon, 0.5=Dusk, 0.75=Midnight
            float intensity = 1f;
            Color lightColor = Color.white;

            if (t < 0.2f) // Dawn
            {
                float p = t / 0.2f;
                intensity = Mathf.Lerp(0.5f, 1.2f, p);
                lightColor = Color.Lerp(new Color(1f, 0.6f, 0.4f), Color.white, p);
            }
            else if (t < 0.5f) // Day
            {
                intensity = 1.2f;
                lightColor = Color.white;
            }
            else if (t < 0.7f) // Dusk
            {
                float p = (t - 0.5f) / 0.2f;
                intensity = Mathf.Lerp(1.2f, 0.4f, p);
                lightColor = Color.Lerp(Color.white, new Color(1f, 0.4f, 0.2f), p);
            }
            else // Night
            {
                float p = (t - 0.7f) / 0.3f;
                intensity = Mathf.Lerp(0.4f, 0.2f, p);
                lightColor = new Color(0.4f, 0.5f, 1f); // Moonlight
            }

            _mainLight.intensity = intensity;
            _mainLight.color = lightColor;

            // Rotate sun
            float sunAngle = t * 360f - 90f;
            _mainLight.transform.rotation = Quaternion.Euler(sunAngle, -30f, 0);
        }

        private void OnDestroy()
        {
            var network = NetworkManager.Instance;
            if (network != null)
            {
                network.OnPhaseChange -= HandlePhaseChange;
                network.OnWeatherChange -= HandleWeatherChange;
                network.OnTick -= HandleTick;
            }
        }
        #endregion

        #region Environment Creation
        private void CreateEnvironment()
        {
            CreateSky();
            CreateGround();
            CreateWater();
            CreateLighting();
            CreateDecorations();
            CreateClouds();
        }

        private void CreateSky()
        {
            // Create a gradient sky using a camera background shader
            _skyMaterial = new Material(Shader.Find("Unlit/Color"));

            // Create sky quad that fills the background
            var skyObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            skyObj.name = "SkyBackground";
            skyObj.transform.SetParent(transform);
            skyObj.transform.position = new Vector3(0, 5, 20);
            skyObj.transform.localScale = new Vector3(60, 30, 1);

            // Remove collider
            Destroy(skyObj.GetComponent<Collider>());

            // Create gradient material
            _skyMaterial = CreateGradientMaterial();
            skyObj.GetComponent<Renderer>().material = _skyMaterial;
            skyObj.GetComponent<Renderer>().sortingOrder = -100;

            // Set initial colors
            _currentSkyTop = daySkyTop;
            _currentSkyBottom = daySkyBottom;
            _targetSkyTop = daySkyTop;
            _targetSkyBottom = daySkyBottom;
            UpdateSkyMaterial();
        }

        private Material CreateGradientMaterial()
        {
            // Since we can't create shaders at runtime easily, use a texture-based approach
            return CreateGradientTextureMaterial();
        }

        private Material CreateGradientTextureMaterial()
        {
            // Create gradient texture
            Texture2D gradientTex = new Texture2D(1, 256);
            gradientTex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < 256; y++)
            {
                float t = y / 255f;
                Color color = Color.Lerp(_currentSkyBottom, _currentSkyTop, t);
                gradientTex.SetPixel(0, y, color);
            }
            gradientTex.Apply();

            Material mat = new Material(Shader.Find("Unlit/Texture"));
            mat.mainTexture = gradientTex;
            return mat;
        }

        private void UpdateSkyMaterial()
        {
            if (_skyMaterial == null || _skyMaterial.mainTexture == null) return;

            Texture2D tex = (Texture2D)_skyMaterial.mainTexture;
            for (int y = 0; y < 256; y++)
            {
                float t = y / 255f;
                Color color = Color.Lerp(_currentSkyBottom, _currentSkyTop, t);
                tex.SetPixel(0, y, color);
            }
            tex.Apply();
        }

        private void CreateGround()
        {
            // Create sandy beach ground
            _groundPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _groundPlane.name = "GroundPlane";
            _groundPlane.transform.SetParent(transform);
            _groundPlane.transform.position = new Vector3(0, -0.5f, 5);
            _groundPlane.transform.rotation = Quaternion.Euler(90, 0, 0);
            _groundPlane.transform.localScale = new Vector3(40, 20, 1);

            // Create sand texture
            _groundMaterial = new Material(Shader.Find("Unlit/Texture"));
            _groundMaterial.mainTexture = CreateSandTexture();
            _groundPlane.GetComponent<Renderer>().material = _groundMaterial;
            _groundPlane.GetComponent<Renderer>().sortingOrder = -50;

            // Remove collider (we don't need physics)
            Destroy(_groundPlane.GetComponent<Collider>());
        }

        private Texture2D CreateSandTexture()
        {
            int size = 128;
            Texture2D tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Create sandy noise pattern
                    float noise = Mathf.PerlinNoise(x * 0.1f, y * 0.1f) * 0.3f;
                    float detail = Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 0.1f;

                    Color baseColor = Color.Lerp(sandDarkColor, sandColor, 0.5f + noise + detail);

                    // Add some sparkle/grain
                    if (Random.value > 0.95f)
                    {
                        baseColor = Color.Lerp(baseColor, Color.white, 0.3f);
                    }

                    tex.SetPixel(x, y, baseColor);
                }
            }
            tex.Apply();
            return tex;
        }

        private void CreateWater()
        {
            // Create water plane at the horizon
            _waterPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _waterPlane.name = "WaterPlane";
            _waterPlane.transform.SetParent(transform);
            _waterPlane.transform.position = new Vector3(0, -0.3f, 12);
            _waterPlane.transform.rotation = Quaternion.Euler(90, 0, 0);
            _waterPlane.transform.localScale = new Vector3(60, 15, 1);

            // Create water material
            if (customWaterMaterial != null)
            {
                _waterMaterial = customWaterMaterial;
                _waterPlane.GetComponent<Renderer>().material = _waterMaterial;
            }
            else
            {
                _waterMaterial = new Material(Shader.Find("Unlit/Transparent"));
                _waterMaterial.mainTexture = CreateWaterTexture();
                _waterPlane.GetComponent<Renderer>().material = _waterMaterial;
            }
            _waterPlane.GetComponent<Renderer>().sortingOrder = -40;

            Destroy(_waterPlane.GetComponent<Collider>());
        }

        private Texture2D CreateWaterTexture()
        {
            int size = 128;
            Texture2D tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Repeat;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float t = (float)y / size;
                    // Add some noise to the base color
                    float n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.1f;
                    Color baseColor = Color.Lerp(waterShallowColor, waterDeepColor, t + n);

                    // Add caustic-like highlights
                    float wave1 = Mathf.Sin(x * 0.15f + y * 0.05f + Time.time * 0.2f) * 0.5f + 0.5f;
                    float wave2 = Mathf.Cos(x * 0.08f - y * 0.12f + Time.time * 0.15f) * 0.5f + 0.5f;
                    baseColor = Color.Lerp(baseColor, Color.white, (wave1 * wave2) * 0.15f);

                    tex.SetPixel(x, y, baseColor);
                }
            }
            tex.Apply();
            return tex;
        }

        private void AnimateWater()
        {
            if (_waterMaterial == null) return;

            // Simple UV scrolling for wave effect
            float offset = Time.time * waveSpeed * 0.05f;
            _waterMaterial.mainTextureOffset = new Vector2(offset, offset * 0.3f);
            
            // Periodically update texture for dynamic caustic effect (expensive but looks premium)
            // Or just use the original UV scrolling if performance is an issue.
        }

        private void CreateLighting()
        {
            // Find or create main directional light
            _mainLight = FindFirstObjectByType<Light>();
            if (_mainLight == null)
            {
                var lightObj = new GameObject("MainLight");
                lightObj.transform.SetParent(transform);
                _mainLight = lightObj.AddComponent<Light>();
                _mainLight.type = LightType.Directional;
            }

            _mainLight.transform.rotation = Quaternion.Euler(50, -30, 0);
            _mainLight.intensity = 1f;
            _mainLight.color = dayAmbient;

            // Set ambient light
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = dayAmbient;
        }

        private void CreateDecorations()
        {
            // Create palm tree silhouettes
            CreatePalmTree(new Vector3(-8, 0, 8), 2.5f);
            CreatePalmTree(new Vector3(-10, 0, 10), 3f);
            CreatePalmTree(new Vector3(9, 0, 7), 2.2f);
            CreatePalmTree(new Vector3(11, 0, 9), 2.8f);

            // Create rocks
            CreateRock(new Vector3(-5, 0, 4), 0.5f);
            CreateRock(new Vector3(6, 0, 5), 0.7f);
            CreateRock(new Vector3(-7, 0, 6), 0.4f);

            CreateGroundDetails();
        }

        private void CreatePalmTree(Vector3 position, float scale)
        {
            var treeObj = new GameObject("PalmTree");
            treeObj.transform.SetParent(transform);
            treeObj.transform.position = position;

            // Create trunk (stretched capsule-ish shape using sprite)
            var trunkSprite = new GameObject("Trunk");
            trunkSprite.transform.SetParent(treeObj.transform);
            trunkSprite.transform.localPosition = new Vector3(0, scale * 0.5f, 0);

            var trunkRenderer = trunkSprite.AddComponent<SpriteRenderer>();
            trunkRenderer.sprite = CreateTreeSprite();
            trunkRenderer.sortingOrder = -20;
            
            // Phase 19-C: Add Billboard for 2.5D perspective
            trunkSprite.AddComponent<Billboard>();
            
            // Phase 19-C: Normalize scale based on world units. 
            // If the sprite is large, we want it to fit the intended 'scale' height.
            // A typical tree sprite at 100 PPU might be 10 units high. 
            // We want it to be 'scale' units high (e.g. 3 units).
            float spriteHeightUnits = trunkRenderer.sprite.rect.height / trunkRenderer.sprite.pixelsPerUnit;
            float normScale = scale / spriteHeightUnits;
            trunkSprite.transform.localScale = new Vector3(normScale, normScale, 1);
        }

        private Texture2D _envTexture;

        private void LoadEnvironmentTexture()
        {
            string path = Application.dataPath + "/Sprites/Environment.png";
            if (System.IO.File.Exists(path))
            {
                byte[] data = System.IO.File.ReadAllBytes(path);
                Texture2D sourceTex = new Texture2D(2, 2);
                sourceTex.LoadImage(data);
                
                // Phase 19-C: Robust transparency transcoding
                _envTexture = ProcessTransparency(sourceTex);
            }
        }

        private Texture2D ProcessTransparency(Texture2D source)
        {
            if (source == null) return null;
            
            // Create a new texture with Alpha channel
            Texture2D tex = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            Color[] pixels = source.GetPixels();
            
            for (int i = 0; i < pixels.Length; i++)
            {
                Color p = pixels[i];
                // Chroma-key: If pixel is very close to white, make it transparent
                if (p.r > 0.9f && p.g > 0.9f && p.b > 0.9f)
                {
                    pixels[i] = new Color(0, 0, 0, 0);
                }
                else
                {
                    pixels[i] = new Color(p.r, p.g, p.b, 1.0f);
                }
            }
            
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private Sprite CreateTreeSprite()
        {
            if (_envTexture != null)
            {
                // Slice palm tree (Assuming it's in the top-left quadrant of the collection)
                return Sprite.Create(_envTexture, new Rect(0, _envTexture.height / 2f, _envTexture.width / 2f, _envTexture.height / 2f), new Vector2(0.5f, 0f), 100f);
            }

            int width = 64;
            int height = 128;
            Texture2D tex = new Texture2D(width, height);

            Color trunk = new Color(0.4f, 0.25f, 0.15f);
            Color trunkDark = new Color(0.3f, 0.18f, 0.1f);
            Color leaf = new Color(0.2f, 0.5f, 0.2f);
            Color leafBright = new Color(0.3f, 0.65f, 0.25f);

            // Clear
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            // Draw trunk
            int trunkWidth = 8;
            int trunkStart = width / 2 - trunkWidth / 2;
            for (int y = 0; y < height * 0.6f; y++)
            {
                for (int x = trunkStart; x < trunkStart + trunkWidth; x++)
                {
                    float noise = Mathf.PerlinNoise(x * 0.2f, y * 0.1f);
                    pixels[y * width + x] = Color.Lerp(trunkDark, trunk, noise);
                }
            }

            // Draw palm fronds
            DrawPalmFronds(pixels, width, height, leaf, leafBright);

            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Point;

            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0));
        }

        private void CachePalmTrees()
        {
            _palmTrees.Clear();
            foreach (Transform child in transform)
            {
                if (child.name == "PalmTree")
                {
                    _palmTrees.Add(child);
                }
            }
        }

        private void AnimateEnvironment()
        {
            // Water animation
            if (_waterMaterial != null)
            {
                float offset = Time.time * waveSpeed * 0.1f;
                _waterMaterial.mainTextureOffset = new Vector2(offset, offset * 0.5f);
            }

            // Tree swaying animation
            float weatherIntensity = (_currentWeather == "Stormy" || _currentWeather == "Rainy") ? 2.5f : 1.0f;
            float time = Time.time;
            
            foreach (var tree in _palmTrees)
            {
                if (tree == null) continue;
                // Sway rotation with slight variation per tree position
                float sway = Mathf.Sin(time * 1.5f + tree.position.x * 0.5f) * 2.0f * weatherIntensity;
                tree.rotation = Quaternion.Euler(0, 0, sway);
            }
        }

        private void DrawPalmFronds(Color[] pixels, int width, int height, Color leaf, Color leafBright)
        {
            Vector2 center = new Vector2(width / 2, height * 0.65f);

            // Draw several fronds
            float[] angles = { -60, -30, 0, 30, 60, -80, 80 };
            foreach (float angle in angles)
            {
                DrawFrond(pixels, width, height, center, angle, leaf, leafBright);
            }
        }

        private void DrawFrond(Color[] pixels, int width, int height, Vector2 start, float angle, Color leaf, Color leafBright)
        {
            float rad = angle * Mathf.Deg2Rad;
            int length = 35;

            for (int i = 0; i < length; i++)
            {
                float t = i / (float)length;
                float droop = t * t * 15; // Fronds droop more at the end

                int x = (int)(start.x + Mathf.Sin(rad) * i);
                int y = (int)(start.y + Mathf.Cos(rad) * i - droop);

                // Draw thick frond
                for (int dx = -2; dx <= 2; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int px = x + dx;
                        int py = y + dy;
                        if (px >= 0 && px < width && py >= 0 && py < height)
                        {
                            float brightness = Mathf.PerlinNoise(px * 0.1f, py * 0.1f);
                            pixels[py * width + px] = Color.Lerp(leaf, leafBright, brightness);
                        }
                    }
                }
            }
        }

        private void CreateRock(Vector3 position, float scale)
        {
            var rockObj = new GameObject("Rock");
            rockObj.transform.SetParent(transform);
            rockObj.transform.position = position;

            var rockRenderer = rockObj.AddComponent<SpriteRenderer>();
            rockRenderer.sprite = CreateRockSprite();
            rockRenderer.sortingOrder = -15;
            
            // Phase 19-C: Add Billboard
            rockObj.AddComponent<Billboard>();
            
            // Phase 19-C: Normalize scale
            float spriteWidthUnits = rockRenderer.sprite.rect.width / rockRenderer.sprite.pixelsPerUnit;
            float normScale = scale / spriteWidthUnits;
            rockObj.transform.localScale = Vector3.one * normScale;
        }

        private Sprite CreateRockSprite()
        {
            if (_envTexture != null)
            {
                // Slice rock from Environment.png (Assuming bottom-right quadrant)
                return Sprite.Create(_envTexture, new Rect(_envTexture.width / 2f, 0, _envTexture.width / 2f, _envTexture.height / 2f), new Vector2(0.5f, 0.5f), 100f);
            }

            int size = 32;
            Texture2D tex = new Texture2D(size, size);

            Color rockDark = new Color(0.3f, 0.3f, 0.35f);
            Color rockLight = new Color(0.5f, 0.5f, 0.55f);

            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            // Draw rock shape
            Vector2 center = new Vector2(size / 2, size / 3);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center.x) / (size * 0.4f);
                    float dy = (y - center.y) / (size * 0.3f);
                    float dist = dx * dx + dy * dy;

                    if (dist < 1 && y < size * 0.7f)
                    {
                        float noise = Mathf.PerlinNoise(x * 0.2f, y * 0.2f);
                        pixels[y * size + x] = Color.Lerp(rockDark, rockLight, noise);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Point;

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0));
        }
        #endregion

        #region Event Handlers
        private void HandlePhaseChange(PhaseChangeData data)
        {
            _currentTimeOfDay = data.new_phase;
            UpdateSkyColors();
        }

        private void HandleWeatherChange(WeatherChangeData data)
        {
            _currentWeather = data.new_weather;
            Debug.Log($"[EnvironmentManager] Weather changed to: {_currentWeather}");
            
            // Notify VFX manager
            if (VisualEffectsManager.Instance != null)
            {
                VisualEffectsManager.Instance.SetWeather(_currentWeather);
            }

            // Adjust lighting based on weather
            UpdateSkyColors(); // This will use the new weather in its logic
        }

        private void HandleTick(TickData data)
        {
            if (!string.IsNullOrEmpty(data.time_of_day) && data.time_of_day != _currentTimeOfDay)
            {
                _currentTimeOfDay = data.time_of_day;
                UpdateSkyColors();
            }
            if (!string.IsNullOrEmpty(data.weather) && data.weather != _currentWeather)
            {
                _currentWeather = data.weather;
                UpdateSkyColors();
            }
        }

        private void UpdateSkyColors()
        {
            // Get base colors for time of day
            Color baseTop, baseBottom, ambient;

            switch (_currentTimeOfDay)
            {
                case "dawn":
                    baseTop = dawnSkyTop;
                    baseBottom = dawnSkyBottom;
                    ambient = dawnAmbient;
                    break;
                case "dusk":
                    baseTop = duskSkyTop;
                    baseBottom = duskSkyBottom;
                    ambient = duskAmbient;
                    break;
                case "night":
                    baseTop = nightSkyTop;
                    baseBottom = nightSkyBottom;
                    ambient = nightAmbient;
                    break;
                default: // day
                    baseTop = daySkyTop;
                    baseBottom = daySkyBottom;
                    ambient = dayAmbient;
                    break;
            }

            // Apply weather tint
            Color weatherTint = Color.white;
            switch (_currentWeather)
            {
                case "Cloudy": weatherTint = cloudyTint; break;
                case "Rainy": weatherTint = rainyTint; break;
                case "Stormy": weatherTint = stormyTint; break;
                case "Foggy": weatherTint = foggyTint; break;
                case "Hot": weatherTint = hotTint; break;
            }

            _targetSkyTop = baseTop * weatherTint;
            _targetSkyBottom = baseBottom * weatherTint;
            _transitionProgress = 0f;

            // Update lighting
            if (_mainLight != null)
            {
                _mainLight.color = ambient * weatherTint;
                _mainLight.intensity = _currentTimeOfDay == "night" ? 0.3f : 1f;
            }

            RenderSettings.ambientLight = ambient * weatherTint * 0.8f;
        }
        #endregion

        private void CreateGroundDetails()
        {
            // Scatter shells
            for (int i = 0; i < 20; i++)
            {
                float x = Random.Range(-25f, 25f);
                float z = Random.Range(3f, 7f); // Near water line
                
                var shell = new GameObject("Shell");
                shell.transform.SetParent(transform);
                shell.transform.position = new Vector3(x, -0.45f, z);
                // Lie flat
                shell.transform.rotation = Quaternion.Euler(90, Random.Range(0, 360), 0);
                
                var renderer = shell.AddComponent<SpriteRenderer>();
                renderer.sprite = CreateShellSprite();
                renderer.sortingOrder = -39; 
                shell.transform.localScale = Vector3.one * Random.Range(0.2f, 0.4f);
            }
            
            // Scatter pebbles
            for (int i = 0; i < 30; i++)
            {
                float x = Random.Range(-25f, 25f);
                float z = Random.Range(-2f, 10f); // Wider range
                
                var pebble = new GameObject("Pebble");
                pebble.transform.SetParent(transform);
                pebble.transform.position = new Vector3(x, -0.48f, z);
                pebble.transform.rotation = Quaternion.Euler(90, Random.Range(0, 360), 0);
                
                var renderer = pebble.AddComponent<SpriteRenderer>();
                renderer.sprite = CreatePebbleSprite();
                renderer.sortingOrder = -39;
                renderer.color = new Color(0.7f, 0.7f, 0.7f);
                pebble.transform.localScale = Vector3.one * Random.Range(0.1f, 0.2f);
            }
        }

        private Sprite CreateShellSprite()
        {
            int size = 32;
            Texture2D tex = new Texture2D(size, size);
            Color[] pixels = new Color[size*size];
            for(int i=0; i<pixels.Length; i++) pixels[i] = Color.clear;

            Vector2 center = new Vector2(size/2, size/2);
            for(int y=0; y<size; y++){
                for(int x=0; x<size; x++){
                    float dist = Vector2.Distance(new Vector2(x,y), center);
                    if(dist < 12) {
                        float angle = Mathf.Atan2(y-center.y, x-center.x);
                        // Simple spiral or scallop shape
                        float radius = 10 + Mathf.Sin(angle * 5) * 2;
                        if(dist < radius)
                            pixels[y*size+x] = new Color(1f, 0.95f, 0.85f);
                    }
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private Sprite CreatePebbleSprite()
        {
            int size = 16;
            Texture2D tex = new Texture2D(size, size);
            Color[] pixels = new Color[size*size];
            for(int i=0; i<pixels.Length; i++) pixels[i] = Color.clear;

            Vector2 center = new Vector2(size/2, size/2);
            for(int y=0; y<size; y++){
                for(int x=0; x<size; x++){
                    if(Vector2.Distance(new Vector2(x,y), center) < 5 + Random.Range(-1f, 1f)) {
                        pixels[y*size+x] = Color.white;
                    }
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private void CreateClouds()
        {
            for (int i = 0; i < 5; i++)
            {
                var cloud = new GameObject("Cloud");
                cloud.transform.SetParent(transform);
                
                // Random position in sky
                float startX = Random.Range(-25f, 25f);
                float startY = Random.Range(3f, 8f);
                float depth = Random.Range(15f, 25f);
                cloud.transform.position = new Vector3(startX, startY, depth);
                
                var renderer = cloud.AddComponent<SpriteRenderer>();
                renderer.sprite = CreateCloudSprite();
                renderer.sortingOrder = -90; // Behind everything but sky
                
                // Random size and opacity
                float scale = Random.Range(3f, 6f);
                cloud.transform.localScale = new Vector3(scale * 1.5f, scale, 1f);
                renderer.color = new Color(1f, 1f, 1f, Random.Range(0.4f, 0.8f));
            }
        }

        private Sprite CreateCloudSprite()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];

            // Procedural fluffy cloud
            Vector2 center = new Vector2(size/2, size/2);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float noise = Mathf.PerlinNoise(x * 0.15f, y * 0.15f); // Noise frequency
                    float dist = Vector2.Distance(new Vector2(x, y), center) / (size * 0.4f);
                    
                    // Soft circle with noise
                    float density = Mathf.Clamp01(1f - dist);
                    density *= (0.5f + noise * 0.5f);
                    // Threshold for fluffiness
                    density = Mathf.SmoothStep(0.2f, 0.8f, density);
                    
                    pixels[y * size + x] = new Color(1, 1, 1, density * density);
                }
            }
            
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private void AnimateClouds()
        {
            // Move clouds slowly
            foreach (Transform child in transform)
            {
                if (child.name == "Cloud")
                {
                    Vector3 pos = child.transform.position;
                    // Wind speed depends on cloud distance for parallax
                    float speed = 0.5f + (25f - pos.z) * 0.05f; 
                    pos.x += Time.deltaTime * speed; 
                    
                    // Wrap around
                    if (pos.x > 30f) pos.x = -30f;
                    
                    child.transform.position = pos;
                }
            }
        }

        #region Public API
        /// <summary>
        /// Force update the environment to specific conditions.
        /// </summary>
        public void SetEnvironment(string timeOfDay, string weather)
        {
            _currentTimeOfDay = timeOfDay;
            _currentWeather = weather;
            UpdateSkyColors();
        }
        #endregion
    }
}
