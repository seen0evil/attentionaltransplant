using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SimplePlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float mouseXSensitivity = 2f;

    [Header("Cursor")]
    [SerializeField] private bool lockCursorOnStart = true;

    private CharacterController characterController;
    private Vector3 verticalVelocity;
    private float yaw;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        yaw = transform.eulerAngles.y;
    }

    private void Update()
    {
        RotatePlayer();
        MovePlayer();
        ApplyGravity();
    }

    private void Start()
    {
        if (lockCursorOnStart)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void RotatePlayer()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseXSensitivity;
        yaw += mouseX;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void MovePlayer()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float forward = Input.GetAxisRaw("Vertical");

        Vector3 moveDirection = (transform.right * horizontal + transform.forward * forward).normalized;

        if (moveDirection.sqrMagnitude > 0f)
        {
            characterController.Move(moveDirection * moveSpeed * Time.deltaTime);
        }
    }

    private void ApplyGravity()
    {
        if (characterController.isGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = -2f;
        }

        verticalVelocity.y += gravity * Time.deltaTime;
        characterController.Move(verticalVelocity * Time.deltaTime);
    }
}
