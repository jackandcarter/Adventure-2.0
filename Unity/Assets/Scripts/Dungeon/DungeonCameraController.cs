using UnityEngine;

namespace Adventure.Dungeon
{
    /// <summary>
    /// Follows the player smoothly and orients toward movement for responsive prediction visuals.
    /// </summary>
    public class DungeonCameraController : MonoBehaviour
    {
        [SerializeField]
        private Transform target;

        [SerializeField]
        private Vector3 offset = new(0f, 8f, -6f);

        [SerializeField]
        private float followSpeed = 8f;

        [SerializeField]
        private float rotationLerp = 10f;

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            var desiredPosition = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);

            var forward = target.forward;
            if (forward.sqrMagnitude > 0.001f)
            {
                var desiredRotation = Quaternion.LookRotation(forward, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationLerp * Time.deltaTime);
            }
        }

        public void SetTarget(Transform followTarget)
        {
            target = followTarget;
        }
    }
}
