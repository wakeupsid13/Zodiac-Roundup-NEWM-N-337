using UnityEngine;

public class TutorialPlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float rotationSpeed = 200f;
    public float movementSpeed = 5f;
    public float runningSpeed = 10f;
    public float jumpHeight = 2f;
    public float gravity = -9.81f;

    private CharacterController controller;
    private Vector3 serverVelocity;
    private Vector2 lastMoveInput;
    private float lastYawDelta;
    private bool jumpPressed;
    private bool runKeyHeld;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    // Update is called once per frame
    void Update()
    {

        // Collect user input
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        bool jumpDown = Input.GetButtonDown("Jump");
        float mouseX = Input.GetAxis("Mouse X");
        bool runKey = Input.GetKey(KeyCode.LeftShift);
        
        UpdateInput(new Vector2(h, v), mouseX * rotationSpeed, jumpDown, runKey);
    }

    void FixedUpdate()
    {
        // Apply rotation
        transform.Rotate(0, lastYawDelta * Time.fixedDeltaTime, 0f);

        // Apply movement
        float targetSpeed;
        if (runKeyHeld)
        {
            targetSpeed = runningSpeed;
        }
        else
        {
            targetSpeed = movementSpeed;
        }
        Vector3 normalizedDir = transform.TransformDirection(new Vector3(lastMoveInput.x, 0, lastMoveInput.y)).normalized;
        Vector3 horizontal = normalizedDir * targetSpeed;

        // Apply Jump and Gravity
        if (controller.isGrounded)
        {
            // Small downward force to keep grounded
            if (serverVelocity.y < 0) serverVelocity.y = -5f;

            if (jumpPressed)
            {
                serverVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpPressed = false;
            }
        }

        // Apply gravity
        serverVelocity.y += gravity * Time.fixedDeltaTime;

        // Combined Movement
        Vector3 motion = (horizontal + new Vector3(0, serverVelocity.y, 0)) * Time.fixedDeltaTime;
        controller.Move(motion);
    }

    void UpdateInput(Vector2 move, float yawPerSec, bool jumpEdge, bool runKey)
    {
        lastMoveInput = move;
        lastYawDelta = yawPerSec;
        if (jumpEdge) jumpPressed = true;
        runKeyHeld = runKey;
    }
}
