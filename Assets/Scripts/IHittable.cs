namespace SpaceShooter
{
    /// <summary>
    /// Implemented by anything that can receive damage — player, enemies, destructible props, etc.
    /// </summary>
    public interface IHittable
    {
        void TakeDamage(int damage);
    }
}
