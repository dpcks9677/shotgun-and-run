using ShotgunAndRun.Player;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class ShotgunEnemyAlert : MonoBehaviour
{
    private Camera _camera;
    private PlayerWeapon _weapon;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        Subscribe();
    }

    private void Update()
    {
        if (_weapon == null)
            Subscribe();
    }

    private void OnDisable()
    {
        if (_weapon != null)
            _weapon.Fired -= HandleFired;

        _weapon = null;
    }

    private void Subscribe()
    {
        if (_weapon != null || Player.PlayerInstance == null) return;

        _weapon = Player.PlayerInstance.GetComponentInChildren<PlayerWeapon>(true);
        if (_weapon != null)
            _weapon.Fired += HandleFired;
    }

    private void HandleFired(bool isCritical)
    {
        if (!_camera.isActiveAndEnabled) return;

        Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(_camera);
        foreach (Enemy enemy in EnemyLayer.ActiveEnemies)
        {
            if (GeometryUtility.TestPlanesAABB(frustum, enemy.IndicatorAnchorBounds))
                enemy.GetComponent<EnemyStateMachine>().InvestigateShot();
        }
    }
}
