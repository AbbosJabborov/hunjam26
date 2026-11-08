using System.Collections;
using UnityEngine;
using Shift.Core;
using Shift.Player;
using Shift.UI;

namespace Shift.Environment
{
    /// <summary>
    /// Trigger volume placed at the end of the level.
    /// When player enters, triggers level completion and smooth Iris Transition to next scene.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class FinishLine : MonoBehaviour
    {
        [Header("Scene Transition")]
        [SerializeField] private bool transitionOnFinish = true;
        [SerializeField] private string nextSceneName = "GameScene";
        [SerializeField] private float transitionDelay = 0.35f;

        private bool _triggered;

        private void Start()
        {
            var col = GetComponent<BoxCollider>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered) return;

            var player = other.GetComponent<PlayerController>() ?? 
                         (other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<PlayerController>() : null);
            if (player != null)
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

                if (transitionOnFinish && !string.IsNullOrEmpty(nextSceneName))
                {
                    StartCoroutine(TransitionRoutine(player.transform));
                }
            }
        }

        private IEnumerator TransitionRoutine(Transform playerTransform)
        {
            if (transitionDelay > 0f)
                yield return new WaitForSeconds(transitionDelay);

            Vector2 screenUv = new Vector2(0.5f, 0.5f);
            Camera cam = Camera.main;
            if (cam != null && playerTransform != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(playerTransform.position);
                screenUv = new Vector2(Mathf.Clamp01(vp.x), Mathf.Clamp01(vp.y));
            }

            if (IrisTransition.Instance != null)
            {
                IrisTransition.Instance.TransitionToScene(nextSceneName, screenUv, "Player", 0.75f, 0.85f);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
            }
        }

        public void ResetTrigger()
        {
            _triggered = false;
        }
    }
}
