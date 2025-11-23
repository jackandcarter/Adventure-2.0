using Adventure.Networking;
using UnityEngine;

namespace Adventure.Dungeon
{
    /// <summary>
    /// Captures movement/ability input and relays to the server while applying light local prediction.
    /// </summary>
    public class PlayerInputRelay : MonoBehaviour
    {
        [SerializeField]
        private NetworkClient networkClient;

        [SerializeField]
        private float predictionSpeed = 5f;

        [SerializeField]
        private Transform predictedTransform;

        private Vector3 predictedVelocity;

        private void Awake()
        {
            if (networkClient == null)
            {
                networkClient = FindObjectOfType<NetworkClient>();
            }

            if (predictedTransform == null)
            {
                predictedTransform = transform;
            }
        }

        private void Update()
        {
            Vector2 moveInput = new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            bool primaryAction = Input.GetButtonDown("Fire1");
            bool secondaryAction = Input.GetButtonDown("Fire2");

            RelayInput(moveInput, primaryAction, secondaryAction);
            ApplyLocalPrediction(moveInput);
        }

        private void RelayInput(Vector2 move, bool primaryAction, bool secondaryAction)
        {
            if (networkClient == null)
            {
                return;
            }

            var payload = new
            {
                moveX = move.x,
                moveY = move.y,
                primary = primaryAction,
                secondary = secondaryAction,
                timestamp = Time.time
            };

            networkClient.SendReliable("input/frame", payload);
        }

        private void ApplyLocalPrediction(Vector2 move)
        {
            if (predictedTransform == null || move == Vector2.zero)
            {
                return;
            }

            predictedVelocity = new Vector3(move.x, 0f, move.y) * predictionSpeed;
            predictedTransform.position += predictedVelocity * Time.deltaTime;
        }
    }
}
