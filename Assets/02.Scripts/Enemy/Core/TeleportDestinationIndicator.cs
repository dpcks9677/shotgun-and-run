using Consumable;
using UnityEngine;

public sealed class TeleportDestinationIndicator : MonoBehaviour
{
    [SerializeField] private ExplosionRangeIndicator _rangeIndicator;
    [SerializeField] private float _radius = 1f;
    [SerializeField] private float _radiusPulse = 0.15f;
    [SerializeField] private float _pulsePeriod = 0.6f;
    [SerializeField] private Color _silver = new Color(0.75f, 0.75f, 0.75f, 1f);

    private float _elapsed;

    private void Start()
    {
        Refresh();
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        Refresh();
    }

    private void Refresh()
    {
        float blend = (1f - Mathf.Cos(_elapsed * Mathf.PI * 2f / _pulsePeriod)) * 0.5f;
        _rangeIndicator.Show(_radius * Mathf.Lerp(1f - _radiusPulse, 1f + _radiusPulse, blend));
        _rangeIndicator.SetColor(Color.Lerp(Color.white, _silver, blend));
    }
}
