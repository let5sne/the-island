using UnityEngine;
using UnityEngine.EventSystems;

namespace TheIsland.Core
{
    /// <summary>
    /// RTS-style camera controller for free-roaming over the island.
    /// Supports WASD movement, mouse scroll zoom, and optional edge scrolling.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        #region Movement Settings
        [Header("Movement")]
        [Tooltip("Camera movement speed (units per second)")]
        [SerializeField] private float moveSpeed = 15f;

        [Tooltip("Movement speed multiplier when holding Shift")]
        [SerializeField] private float fastMoveMultiplier = 2f;

        [Tooltip("Smooth movement interpolation (0 = instant, 1 = very smooth)")]
        [Range(0f, 0.99f)]
        [SerializeField] private float moveSmoothness = 0.1f;
        #endregion

        #region Zoom Settings
        [Header("Zoom")]
        [Tooltip("Zoom speed (scroll sensitivity)")]
        [SerializeField] private float zoomSpeed = 10f;

        [Tooltip("Minimum camera height (closest zoom)")]
        [SerializeField] private float minZoom = 5f;

        [Tooltip("Maximum camera height (farthest zoom)")]
        [SerializeField] private float maxZoom = 50f;

        [Tooltip("Smooth zoom interpolation")]
        [Range(0f, 0.99f)]
        [SerializeField] private float zoomSmoothness = 0.1f;
        #endregion

        #region Rotation Settings
        [Header("Rotation (Optional)")]
        [Tooltip("Enable middle mouse button rotation")]
        [SerializeField] private bool enableRotation = true;

        [Tooltip("Rotation speed")]
        [SerializeField] private float rotationSpeed = 100f;
        #endregion

        #region Edge Scrolling
        [Header("Edge Scrolling (Optional)")]
        [Tooltip("Enable screen edge scrolling")]
        [SerializeField] private bool enableEdgeScrolling = false;

        [Tooltip("Edge threshold in pixels")]
        [SerializeField] private float edgeThreshold = 20f;
        #endregion

        #region Bounds
        [Header("Movement Bounds")]
        [Tooltip("Limit camera movement to a specific area")]
        [SerializeField] private bool useBounds = false;

        [SerializeField] private Vector2 boundsMin = new Vector2(-50f, -50f);
        [SerializeField] private Vector2 boundsMax = new Vector2(50f, 50f);
        #endregion

        #region Private Fields
        private Vector3 _targetPosition;
        private float _targetZoom;
        private float _currentYRotation;
        private Camera _camera;
        #endregion

        #region Unity Lifecycle
        private void Start()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            _targetPosition = transform.position;
            _targetZoom = transform.position.y;
            _currentYRotation = transform.eulerAngles.y;
        }

        private void Update()
        {
            // Skip keyboard input when UI input field is focused
            if (!IsUIInputFocused())
            {
                HandleMovementInput();
                HandleRotationInput();
            }

            // Zoom always works (mouse scroll doesn't conflict with typing)
            HandleZoomInput();

            ApplyMovement();
        }

        /// <summary>
        /// Check if a UI input field is currently focused.
        /// </summary>
        private bool IsUIInputFocused()
        {
            if (EventSystem.current == null) return false;

            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null) return false;

            // Check if the selected object has an input field component
            return selected.GetComponent<TMPro.TMP_InputField>() != null
                || selected.GetComponent<UnityEngine.UI.InputField>() != null;
        }
        #endregion

        #region Input Handling
        private void HandleMovementInput()
        {
            Vector3 moveDirection = Vector3.zero;

            // WASD / Arrow keys input
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                moveDirection += GetForward();

            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                moveDirection -= GetForward();

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                moveDirection -= GetRight();

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                moveDirection += GetRight();

            // Edge scrolling
            if (enableEdgeScrolling)
            {
                Vector3 edgeMove = GetEdgeScrollDirection();
                moveDirection += edgeMove;
            }

            // Apply movement
            if (moveDirection != Vector3.zero)
            {
                float speed = moveSpeed;
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    speed *= fastMoveMultiplier;
                }

                moveDirection.Normalize();
                _targetPosition += moveDirection * speed * Time.deltaTime;
            }

            // Clamp to bounds
            if (useBounds)
            {
                _targetPosition.x = Mathf.Clamp(_targetPosition.x, boundsMin.x, boundsMax.x);
                _targetPosition.z = Mathf.Clamp(_targetPosition.z, boundsMin.y, boundsMax.y);
            }
        }

        private void HandleZoomInput()
        {
            float scrollInput = Input.GetAxis("Mouse ScrollWheel");

            if (Mathf.Abs(scrollInput) > 0.01f)
            {
                _targetZoom -= scrollInput * zoomSpeed;
                _targetZoom = Mathf.Clamp(_targetZoom, minZoom, maxZoom);
            }
        }

        private void HandleRotationInput()
        {
            if (!enableRotation) return;

            // Middle mouse button rotation
            if (Input.GetMouseButton(2))
            {
                float rotateInput = Input.GetAxis("Mouse X");
                _currentYRotation += rotateInput * rotationSpeed * Time.deltaTime;
            }

            // Q/E rotation (alternative)
            if (Input.GetKey(KeyCode.Q))
            {
                _currentYRotation -= rotationSpeed * Time.deltaTime;
            }
            if (Input.GetKey(KeyCode.E))
            {
                _currentYRotation += rotationSpeed * Time.deltaTime;
            }
        }

        private Vector3 GetEdgeScrollDirection()
        {
            Vector3 direction = Vector3.zero;
            Vector3 mousePos = Input.mousePosition;

            if (mousePos.x < edgeThreshold)
                direction -= GetRight();
            else if (mousePos.x > Screen.width - edgeThreshold)
                direction += GetRight();

            if (mousePos.y < edgeThreshold)
                direction -= GetForward();
            else if (mousePos.y > Screen.height - edgeThreshold)
                direction += GetForward();

            return direction;
        }
        #endregion

        #region Movement Application
        private void ApplyMovement()
        {
            // Smooth position
            Vector3 currentPos = transform.position;
            Vector3 newPos = new Vector3(
                Mathf.Lerp(currentPos.x, _targetPosition.x, 1f - moveSmoothness),
                Mathf.Lerp(currentPos.y, _targetZoom, 1f - zoomSmoothness),
                Mathf.Lerp(currentPos.z, _targetPosition.z, 1f - moveSmoothness)
            );
            transform.position = newPos;

            // Update target Y to match current (for initialization)
            _targetPosition.y = _targetZoom;

            // Apply rotation
            if (enableRotation)
            {
                Quaternion targetRotation = Quaternion.Euler(
                    transform.eulerAngles.x,  // Keep current X (pitch)
                    _currentYRotation,
                    0f
                );
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    1f - moveSmoothness
                );
            }
        }

        private Vector3 GetForward()
        {
            // Get forward direction on XZ plane (ignoring pitch)
            Vector3 forward = transform.forward;
            forward.y = 0;
            return forward.normalized;
        }

        private Vector3 GetRight()
        {
            // Get right direction on XZ plane
            Vector3 right = transform.right;
            right.y = 0;
            return right.normalized;
        }
        #endregion

        #region Public API
        /// <summary>
        /// Move camera to focus on a specific world position.
        /// </summary>
        public void FocusOn(Vector3 worldPosition, bool instant = false)
        {
            _targetPosition = new Vector3(worldPosition.x, _targetZoom, worldPosition.z);

            if (instant)
            {
                transform.position = new Vector3(worldPosition.x, _targetZoom, worldPosition.z);
            }
        }

        /// <summary>
        /// Set zoom level directly.
        /// </summary>
        public void SetZoom(float zoomLevel, bool instant = false)
        {
            _targetZoom = Mathf.Clamp(zoomLevel, minZoom, maxZoom);

            if (instant)
            {
                Vector3 pos = transform.position;
                pos.y = _targetZoom;
                transform.position = pos;
            }
        }

        /// <summary>
        /// Set movement bounds at runtime.
        /// </summary>
        public void SetBounds(Vector2 min, Vector2 max)
        {
            useBounds = true;
            boundsMin = min;
            boundsMax = max;
        }

        /// <summary>
        /// Disable movement bounds.
        /// </summary>
        public void DisableBounds()
        {
            useBounds = false;
        }
        #endregion

        #region Editor Gizmos
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (useBounds)
            {
                Gizmos.color = Color.yellow;
                Vector3 center = new Vector3(
                    (boundsMin.x + boundsMax.x) / 2f,
                    transform.position.y,
                    (boundsMin.y + boundsMax.y) / 2f
                );
                Vector3 size = new Vector3(
                    boundsMax.x - boundsMin.x,
                    0.1f,
                    boundsMax.y - boundsMin.y
                );
                Gizmos.DrawWireCube(center, size);
            }
        }
#endif
        #endregion
    }
}
