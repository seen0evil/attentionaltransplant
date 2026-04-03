using UnityEngine;

public class PlayerCameraLook : MonoBehaviour
{
    [SerializeField] private Transform playerBody;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 1.6f, 0f);
    [SerializeField] private float mouseYSensitivity = 2f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    private float pitch;

    private void Awake()
    {
        if (playerBody == null && transform.parent != null)
        {
            playerBody = transform.parent;
        }
    }

    private void LateUpdate()
    {
        if (playerBody == null)
        {
            return;
        }

        float mouseY = Input.GetAxis("Mouse Y") * mouseYSensitivity;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.position = playerBody.position + playerBody.TransformVector(cameraOffset);
        transform.rotation = playerBody.rotation * Quaternion.Euler(pitch, 0f, 0f);
    }
}
