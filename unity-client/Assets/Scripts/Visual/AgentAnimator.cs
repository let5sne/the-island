using UnityEngine;
using System.Collections;

namespace TheIsland
{
    /// <summary>
    /// Procedural 2D animator for agents. 
    /// Handles idle breathing, movement bopping, and action-based squash/stretch.
    /// </summary>
    public class AgentAnimator : MonoBehaviour
    {
        [Header("Animation Settings")]
        public float idleSpeed = 2f;
        public float idleAmount = 0.05f;
        public float moveBopSpeed = 12f;
        public float moveBopAmount = 0.12f;
        public float moveTiltAmount = 8f;
        
        private SpriteRenderer _spriteRenderer;
        private Vector3 _originalScale;
        private Vector3 _targetLocalPos;
        private Quaternion _targetLocalRot;
        private Vector3 _targetScale;
        
        private float _velocityPercentage; // 0 to 1
        private bool _isMoving;

        private void Awake()
        {
            // Find in children if not on this object
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void Start()
        {
            if (_spriteRenderer != null)
            {
                _originalScale = _spriteRenderer.transform.localScale;
            }
            else
            {
                _originalScale = Vector3.one;
            }
        }

        public void SetMovement(float currentVelocity, float maxVelocity)
        {
            _velocityPercentage = Mathf.Clamp01(currentVelocity / Mathf.Max(0.1f, maxVelocity));
            _isMoving = _velocityPercentage > 0.05f;
        }

        public void TriggerActionEffect()
        {
            StopAllCoroutines();
            StartCoroutine(ActionPulseRoutine());
        }

        private void Update()
        {
            if (_spriteRenderer == null) return;

            if (_isMoving)
            {
                AnimateMove();
            }
            else
            {
                AnimateIdle();
            }

            // Smoothly apply transforms
            _spriteRenderer.transform.localPosition = Vector3.Lerp(_spriteRenderer.transform.localPosition, _targetLocalPos, Time.deltaTime * 10f);
            _spriteRenderer.transform.localRotation = Quaternion.Slerp(_spriteRenderer.transform.localRotation, _targetLocalRot, Time.deltaTime * 10f);
            _spriteRenderer.transform.localScale = Vector3.Lerp(_spriteRenderer.transform.localScale, _targetScale, Time.deltaTime * 10f);
        }

        private void AnimateIdle()
        {
            // Idle "Breathing"
            float breathe = Mathf.Sin(Time.time * idleSpeed) * idleAmount;
            
            _targetScale = new Vector3(_originalScale.x, _originalScale.y * (1f + breathe), _originalScale.z);
            _targetLocalPos = Vector3.zero;
            _targetLocalRot = Quaternion.identity;
        }

        private void AnimateMove()
        {
            // Movement "Bopping" - Sin wave for vertical bounce
            float cycle = Time.time * moveBopSpeed;
            float bop = Mathf.Abs(Mathf.Sin(cycle)) * moveBopAmount * _velocityPercentage;
            
            // Tilt based on the cycle to give a "walking" feel
            float tilt = Mathf.Sin(cycle) * moveTiltAmount * _velocityPercentage;
            
            _targetLocalPos = new Vector3(0, bop, 0);
            _targetLocalRot = Quaternion.Euler(0, 0, tilt);
            
            // Squash and stretch during the bop
            float stretch = 1f + bop;
            float squash = 1f / stretch;
            _targetScale = new Vector3(_originalScale.x * stretch, _originalScale.y * squash, _originalScale.z);
        }

        private IEnumerator ActionPulseRoutine()
        {
            float elapsed = 0;
            float duration = 0.25f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // Double pulse or overshoot
                float scale = 1.0f + Mathf.Sin(t * Mathf.PI) * 0.4f;
                
                // Override target scale briefly
                _spriteRenderer.transform.localScale = new Vector3(_originalScale.x * scale, _originalScale.y * (2f - scale), _originalScale.z);
                yield return null;
            }
            _targetScale = _originalScale;
        }
    }
}