using UnityEngine;
using System.Collections;

namespace TheIsland
{
    /// <summary>
    /// Procedural 2D animator for agents. 
    /// Handles idle breathing, movement bopping, and action-based squash/stretch.
    /// Phase 19-E: Added banking turns and anticipation/overshoot.
    /// </summary>
    public class AgentAnimator : MonoBehaviour
    {
        [Header("Animation Settings")]
        public float idleSpeed = 2f;
        public float idleAmount = 0.04f;
        public float moveBopSpeed = 12f;
        public float moveBopAmount = 0.1f;
        public float moveTiltAmount = 10f;
        public float bankingAmount = 15f;
        public float jiggleIntensity = 0.15f;
        
        private SpriteRenderer _spriteRenderer;
        private Vector3 _originalScale;
        private Vector3 _targetLocalPos;
        private Quaternion _targetLocalRot;
        private Vector3 _targetScale;
        
        private Vector3 _currentVelocity;
        private float _velocityPercentage; // 0 to 1
        private bool _isMoving;
        private float _jiggleOffset;
        private float _jiggleVelocity;

        private void Awake()
        {
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
            _targetScale = _originalScale;
        }

        public void SetMovement(Vector3 velocity, float maxVelocity = 3f)
        {
            _currentVelocity = velocity;
            float targetVelPct = Mathf.Clamp01(velocity.magnitude / Mathf.Max(0.1f, maxVelocity));
            
            bool nowMoving = targetVelPct > 0.05f;
            if (nowMoving && !_isMoving)
            {
                TriggerAnticipation();
                _jiggleVelocity = -2f; // Initial kickback
            }
            else if (!nowMoving && _isMoving)
            {
                TriggerOvershoot();
                _jiggleVelocity = 2f; // Snap forward
            }
            
            _velocityPercentage = targetVelPct;
            _isMoving = nowMoving;
        }

        // Compatibility for older code
        public void SetMovement(float currentVelocity, float maxVelocity)
        {
            SetMovement(new Vector3(currentVelocity, 0, 0), maxVelocity);
        }

        public void TriggerActionEffect()
        {
            StopAllCoroutines();
            StartCoroutine(ActionPulseRoutine(0.4f, 1.3f));
        }

        private void TriggerAnticipation()
        {
            StartCoroutine(ActionPulseRoutine(0.15f, 0.8f)); // Squash
        }

        private void TriggerOvershoot()
        {
            StartCoroutine(ActionPulseRoutine(0.2f, 1.15f)); // Slight stretch
        }

        private void Update()
        {
            if (_spriteRenderer == null) return;

            // Phase 19-F: Secondary Jiggle Physics (Simple Spring)
            float jiggleStrength = 150f;
            float jiggleDamping = 10f;
            float force = -jiggleStrength * _jiggleOffset;
            _jiggleVelocity += (force * Time.deltaTime);
            _jiggleVelocity -= _jiggleVelocity * jiggleDamping * Time.deltaTime;
            _jiggleOffset += _jiggleVelocity * Time.deltaTime;

            if (_isMoving)
            {
                AnimateMove();
            }
            else
            {
                AnimateIdle();
            }

            // Smoothly apply transforms
            float lerpSpeed = 12f;
            var t = _spriteRenderer.transform;
            t.localPosition = Vector3.Lerp(t.localPosition, _targetLocalPos + new Vector3(0, _jiggleOffset * 0.1f, 0), Time.deltaTime * lerpSpeed);
            t.localRotation = Quaternion.Slerp(t.localRotation, _targetLocalRot * Quaternion.Euler(0, 0, _jiggleOffset * 2f), Time.deltaTime * lerpSpeed);
            
            // Apply scale with jiggle squash/stretch
            Vector3 jiggleScale = new Vector3(1f + _jiggleOffset, 1f - _jiggleOffset, 1f);
            t.localScale = Vector3.Lerp(t.localScale, Vector3.Scale(_targetScale, jiggleScale), Time.deltaTime * lerpSpeed);
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
            
            // Traditional Bop Tilt
            float bopTilt = Mathf.Sin(cycle) * moveTiltAmount * _velocityPercentage;
            
            // Phase 19-E: Banking Turn Tilt 
            // Lean into the direction of X velocity
            float bankingTilt = -(_currentVelocity.x / 3f) * bankingAmount;
            
            _targetLocalPos = new Vector3(0, bop, 0);
            _targetLocalRot = Quaternion.Euler(0, 0, bopTilt + bankingTilt);
            
            // Squash and stretch during the bop
            float stretch = 1f + bop;
            float squash = 1f / stretch;
            _targetScale = new Vector3(_originalScale.x * squash, _originalScale.y * stretch, _originalScale.z);
        }

        private IEnumerator ActionPulseRoutine(float duration, float targetScaleY)
        {
            float elapsed = 0;
            Vector3 peakScale = new Vector3(_originalScale.x * (2f - targetScaleY), _originalScale.y * targetScaleY, _originalScale.z);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                float sin = Mathf.Sin(progress * Mathf.PI);
                
                // Temp override of targetScale for the pulse
                _spriteRenderer.transform.localScale = Vector3.Lerp(_originalScale, peakScale, sin);
                yield return null;
            }
        }
    }
}