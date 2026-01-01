using UnityEngine;

namespace TheIsland.Visual
{
    /// <summary>
    /// Forces a 2D sprite or UI element to always face the camera.
    /// Attach to any GameObject that should billboard towards the main camera.
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        #region Configuration
        [Header("Billboard Settings")]
        [Tooltip("If true, locks the Y-axis rotation (sprite stays upright)")]
        [SerializeField] private bool lockYAxis = true;

        [Tooltip("If true, uses the main camera. Otherwise, assign a specific camera.")]
        [SerializeField] private bool useMainCamera = true;

        [Tooltip("Custom camera to face (only used if useMainCamera is false)")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("Flip the facing direction (useful for some sprite setups)")]
        [SerializeField] private bool flipFacing = false;
        #endregion

        #region Private Fields
        private Camera _camera;
        private Transform _cameraTransform;
        #endregion

        #region Unity Lifecycle
        private void Start()
        {
            CacheCamera();
        }

        private void LateUpdate()
        {
            if (_cameraTransform == null)
            {
                CacheCamera();
                if (_cameraTransform == null) return;
            }

            FaceCamera();
        }
        #endregion

        #region Private Methods
        private void CacheCamera()
        {
            _camera = useMainCamera ? Camera.main : targetCamera;
            if (_camera != null)
            {
                _cameraTransform = _camera.transform;
            }
        }

        private void FaceCamera()
        {
            if (lockYAxis)
            {
                // Only rotate around Y-axis (sprite stays upright)
                Vector3 lookDirection = _cameraTransform.position - transform.position;
                lookDirection.y = 0; // Ignore vertical difference

                if (lookDirection != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(
                        flipFacing ? lookDirection : -lookDirection
                    );
                    transform.rotation = targetRotation;
                }
            }
            else
            {
                // Full billboard - face camera completely
                transform.rotation = flipFacing
                    ? Quaternion.LookRotation(transform.position - _cameraTransform.position)
                    : _cameraTransform.rotation;
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Set a custom camera to face (disables useMainCamera).
        /// </summary>
        public void SetTargetCamera(Camera camera)
        {
            useMainCamera = false;
            targetCamera = camera;
            _camera = camera;
            _cameraTransform = camera?.transform;
        }

        /// <summary>
        /// Reset to use main camera.
        /// </summary>
        public void UseMainCamera()
        {
            useMainCamera = true;
            CacheCamera();
        }

        /// <summary>
        /// Configure billboard for UI elements (full facing, no Y-lock).
        /// </summary>
        public void ConfigureForUI()
        {
            lockYAxis = false;
            flipFacing = false;
        }

        /// <summary>
        /// Configure billboard for sprites (Y-axis locked, stays upright).
        /// </summary>
        public void ConfigureForSprite()
        {
            lockYAxis = true;
            flipFacing = false;
        }

        /// <summary>
        /// Set whether to lock Y-axis rotation.
        /// </summary>
        public void SetLockYAxis(bool locked)
        {
            lockYAxis = locked;
        }
        #endregion
    }
}
