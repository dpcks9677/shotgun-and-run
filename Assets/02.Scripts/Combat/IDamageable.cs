namespace ShotgunAndRun.Combat
{
    public interface IDamageable
    {
        public bool TryTakeDamage(DamageInfo damageInfo);
    }
}
