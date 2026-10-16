using System.Collections.Generic;
using UnityEngine;

namespace ShotgunAndRun.Player
{
    public class DashPool : MonoBehaviour
    {
        [SerializeField] private DashDustVFX _dashPrefab;
        [SerializeField] private Transform _dashRoot;

        [SerializeField] private Ghost _ghostPrefab;
        [SerializeField] private Transform _ghostRoot;

        [SerializeField] private int _prewarmDash = 5;
        [SerializeField] private int _prewarmGhost = 12;

        [SerializeField] private int _maxDash = 10;
        [SerializeField] private int _maxGhost = 20;

        private List<DashDustVFX> _dashPool;
        private List<Ghost> _ghostPool;
        private bool _ownsGhostRoot;

        private void Awake()
        {
            _dashRoot = transform;
            _ghostRoot = transform;

            _dashPool = new List<DashDustVFX>();
            _ghostPool = new List<Ghost>();

            PrewarmDash();
            PrewarmGhost();
        }

        private void PrewarmDash()
        {
            while (_dashPool.Count < _prewarmDash && _dashPool.Count < _maxDash)
            {
                CreateDash();
            }
        }

        private DashDustVFX CreateDash()
        {
            if (_dashPool.Count >= _maxDash)
            {
                return null;
            }

            DashDustVFX dash = Instantiate(_dashPrefab, _dashRoot);
            dash.gameObject.SetActive(false);
            _dashPool.Add(dash);

            return dash;
        }

        public DashDustVFX AcquireDash()
        {
            for (int i = 0; i < _dashPool.Count; i++)
            {
                DashDustVFX dash = _dashPool[i];
                if (!dash.gameObject.activeSelf)
                {
                    return dash;
                }
            }

            return CreateDash();
        }

        private void PrewarmGhost()
        {
            while (_ghostPool.Count < _prewarmGhost && _ghostPool.Count < _maxGhost)
            {
                CreateGhost();
            }
        }

        private Ghost CreateGhost()
        {
            if (_ghostPool.Count >= _maxGhost)
            {
                return null;
            }

            Ghost ghost = Instantiate(_ghostPrefab, _ghostRoot);
            ghost.gameObject.SetActive(false);
            _ghostPool.Add(ghost);

            return ghost;
        }

        public Ghost AcquireGhost()
        {
            for (int i = 0; i < _ghostPool.Count; i++)
            {
                Ghost ghost = _ghostPool[i];
                if (!ghost.gameObject.activeSelf)
                {
                    return ghost;
                }
            }

            return CreateGhost();
        }
    }
}
