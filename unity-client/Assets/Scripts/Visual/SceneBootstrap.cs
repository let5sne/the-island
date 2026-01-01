using UnityEngine;

namespace TheIsland.Visual
{
    /// <summary>
    /// Bootstraps the scene with all visual components.
    /// Attach this to an empty GameObject in your scene to automatically
    /// create the environment, weather effects, and other visual systems.
    /// </summary>
    public class SceneBootstrap : MonoBehaviour
    {
        [Header("Auto-Create Components")]
        [SerializeField] private bool createEnvironment = true;
        [SerializeField] private bool createWeatherEffects = true;

        [Header("Camera Settings")]
        [SerializeField] private bool configureCamera = true;
        [SerializeField] private Vector3 cameraPosition = new Vector3(0, 3, -8);
        [SerializeField] private Vector3 cameraRotation = new Vector3(15, 0, 0);
        [SerializeField] private float cameraFieldOfView = 60f;

        private void Awake()
        {
            // Configure camera
            if (configureCamera)
            {
                ConfigureMainCamera();
            }

            // Create environment
            if (createEnvironment && EnvironmentManager.Instance == null)
            {
                CreateEnvironmentManager();
            }

            // Create weather effects
            if (createWeatherEffects && WeatherEffects.Instance == null)
            {
                CreateWeatherEffects();
            }

            Debug.Log("[SceneBootstrap] Visual systems initialized");
        }

        private void ConfigureMainCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var camObj = new GameObject("Main Camera");
                mainCamera = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
                camObj.tag = "MainCamera";
            }

            mainCamera.transform.position = cameraPosition;
            mainCamera.transform.rotation = Quaternion.Euler(cameraRotation);
            mainCamera.fieldOfView = cameraFieldOfView;
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0.4f, 0.6f, 0.9f); // Fallback sky color

            Debug.Log("[SceneBootstrap] Camera configured");
        }

        private void CreateEnvironmentManager()
        {
            var envObj = new GameObject("EnvironmentManager");
            envObj.AddComponent<EnvironmentManager>();
            Debug.Log("[SceneBootstrap] EnvironmentManager created");
        }

        private void CreateWeatherEffects()
        {
            var weatherObj = new GameObject("WeatherEffects");
            weatherObj.AddComponent<WeatherEffects>();
            Debug.Log("[SceneBootstrap] WeatherEffects created");
        }

        /// <summary>
        /// Call this to manually refresh all visual systems.
        /// </summary>
        public void RefreshVisuals()
        {
            if (EnvironmentManager.Instance != null)
            {
                EnvironmentManager.Instance.SetEnvironment("day", "Sunny");
            }

            if (WeatherEffects.Instance != null)
            {
                WeatherEffects.Instance.SetWeather("Sunny");
            }
        }
    }
}
