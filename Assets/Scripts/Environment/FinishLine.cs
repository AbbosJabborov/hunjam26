using UnityEngine;
using Shift.Core;
using Shift.Player;

namespace Shift.Environment
{
    /// <summary>
    /// Trigger volume placed at the end of the level.
    /// When player enters, triggers level completion in LevelManager.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class FinishLine : MonoBehaviour
    {
        private bool _triggered;

        private void Start()
        {
            var col = GetComponent<BoxCollider>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered) return;

            if (other.GetComponent<PlayerController>() != null || 
                (other.attachedRigidbody != null && other.attachedRigidbody.GetComponent<PlayerController>() != null))
            {
                _triggered = true;
                if (LevelManager.Instance != null)
                {
                    LevelManager.Instance.CompleteLevel();
                }
                else
                {
                    Debug.Log("[FinishLine] Player reached the finish line!");
                }
            }
        }

        public void ResetTrigger()
        {
            _triggered = false;
        }
    }
}

