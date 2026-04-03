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
        ResolvePlayerBody();
        pitch = NormalizeAngle(transform.eulerAngles.x);
    }

    private void LateUpdate()
    {
        ResolvePlayerBody();

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

    public void BindToPlayer(Transform target)
    {
        playerBody = target;
        pitch = NormalizeAngle(transform.eulerAngles.x);
    }

    private void ResolvePlayerBody()
    {
        if (playerBody != null)
        {
            return;
        }

        if (transform.parent != null)
        {
            playerBody = transform.parent;
            return;
        }

        SimplePlayerMovement movement = FindAnyObjectByType<SimplePlayerMovement>();
        if (movement != null)
        {
            playerBody = movement.transform;
        }
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f)
        {
            angle -= 360f;
        }

        while (angle < -180f)
        {
            angle += 360f;
        }

        return angle;
    }
}
