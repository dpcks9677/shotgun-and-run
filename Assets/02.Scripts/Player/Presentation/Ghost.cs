using System;
using System.Collections;
using UnityEngine;

namespace ShotgunAndRun.Player
{
    public class Ghost : MonoBehaviour
    {
        private float _ghostDelay = -1f;
        private float _ghostDelaySeconds = -1f;
        private bool _isInitialized;

        public void Initialize(float dashDuration, bool weaponUsable)
        {
            _ghostDelay = Mathf.Max(0f, dashDuration);
            _ghostDelaySeconds = _ghostDelay;

            _isInitialized = true;
        }

        private void Update()
        {
            if (!_isInitialized) return;

            if (_ghostDelaySeconds > 0)
            {
                _ghostDelaySeconds -= Time.deltaTime;
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
