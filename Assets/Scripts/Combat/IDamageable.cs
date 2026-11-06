using UnityEngine;

namespace Shift.Combat
{
    /// <summary>
    /// Interface for any entity that can take damage.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal);
    }
}

