using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
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

    public override void OnNetworkSpawn()
    {
        controller = GetComponent<CharacterController>();
        ColorOwner();

        if (IsServer)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(new Vector3(-17f,1f, 7f), Quaternion.identity);
            controller.enabled = true;
        }

        if (IsOwner)
            {
                // create camera only for local owner
                var cam = new GameObject("PlayerCamera").AddComponent<Camera>();
                cam.transform.SetParent(transform);
                cam.transform.localPosition = new Vector3(0, 1.6f, -3.5f);
                cam.transform.localRotation = Quaternion.Euler(10, 0, 0);

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
    }

    void Update()
    {
        if (!IsOwner) return;

        // collect local input
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        bool jumpDown = Input.GetButtonDown("Jump");
        float mouseX = Input.GetAxis("Mouse X");
        bool runKey = Input.GetKey(KeyCode.LeftShift);
        SendInputServerRpc(new Vector2(h, v), mouseX * rotationSpeed, jumpDown, runKey);
    }

    void FixedUpdate()
    {
        if (!IsServer) return;

        // rotation
        transform.Rotate(0, lastYawDelta * Time.fixedDeltaTime, 0);

        // speed choice
        float targetSpeed;
        if (runKeyHeld)
        {
            targetSpeed = runningSpeed;
        }
        else
        {
            targetSpeed = movementSpeed;
        }
        Vector3 moveDir = transform.TransformDirection(new Vector3(lastMoveInput.x, 0, lastMoveInput.y));
        Vector3 horizontal = moveDir * targetSpeed;

        // jumping & gravity
        if (controller.isGrounded)
        {
            if (serverVelocity.y < 0) serverVelocity.y = -5f; // stick
            if (jumpPressed)
            {
                serverVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpPressed = false;
            }
        }

        serverVelocity.y += gravity * Time.fixedDeltaTime;

        // apply motion
        Vector3 motion = (horizontal + new Vector3(0, serverVelocity.y, 0)) * Time.fixedDeltaTime;
        controller.Move(motion);
    }

    void ColorOwner()
    {
        // unique color by client id
        float hue = (float)(OwnerClientId % 16) / 16f;
        Color col = Color.HSVToRGB(hue, 0.7f, 0.9f);

        var r = GetComponent<Renderer>();
        if (r != null)
        {
            var m = r.material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            else m.color = col;
        }
    }

    [ServerRpc(Delivery = RpcDelivery.Reliable)]
    void SendInputServerRpc(Vector2 move, float yawPerSec, bool jumpEdge, bool runKey)
    {
        lastMoveInput = move;
        lastYawDelta = yawPerSec;
        if (jumpEdge) jumpPressed = true;
        runKeyHeld = runKey;
    }
}
