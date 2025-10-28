using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using TMPro;
using UnityEngine.UI;

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

    public NetworkVariable<FixedString128Bytes> PlayerName = new NetworkVariable<FixedString128Bytes>
    (default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // private TMP_Text nameText;

    public override void OnNetworkSpawn()
    {
        controller = GetComponent<CharacterController>();
        ColorOwner();

        if (IsServer)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(new Vector3(-17f, 1f, 7f), Quaternion.identity);
            controller.enabled = true;
        }

        if (IsOwner)
        {
            var cam = Camera.main;
            cam.transform.SetParent(transform);
            cam.transform.localPosition = new Vector3(0, 1.6f, -3.5f);
            cam.transform.localRotation = Quaternion.Euler(10, 0, 0);

            var canvas = GetComponentInChildren<Canvas>();
            if (canvas) canvas.worldCamera = cam;
        }

        // === Name label: initialize for EVERYONE and subscribe to updates ===
        // nameText = GetComponentInChildren<TMP_Text>(true);
        // if (nameText)
        // {
        //     nameText.transform.position = transform.position + new Vector3(0, 1.25f, 0);
        //     var rend = GetComponent<Renderer>();
        //     if (rend) nameText.color = rend.material.color;

        //     // initial value (empty by default until server writes)
        //     nameText.text = PlayerName.Value.ToString();

        //     // keep it synced when the server sets PlayerName
        //     PlayerName.OnValueChanged += OnPlayerNameChanged;
        // }

        // // === Owner pushes their cached name to the server (host writes directly) ===
        // if (IsOwner)
        // {
        //     var cached = GameState.Instance ? GameState.Instance.localPlayerName : "";
        //     Debug.Log($"[{OwnerClientId}] cached name at spawn: '{cached}' (obj {NetworkObjectId})");
        //     if (!string.IsNullOrWhiteSpace(cached))
        //     {
        //         if (IsServer)
        //             PlayerName.Value = new FixedString128Bytes(cached);
        //         else
        //             SetNameServerRpc(cached);
        //     }
        // }

    }

    // private void OnPlayerNameChanged(FixedString128Bytes oldV, FixedString128Bytes newV)
    // {
    //     if (nameText) nameText.text = newV.ToString();
    // }

    // public override void OnNetworkDespawn()
    // {
    //     // avoid dangling delegates
    //     PlayerName.OnValueChanged -= OnPlayerNameChanged;
    // }

    // Update is called once per frame
    void Update()
    {
        if (!IsOwner) return;

        // Collect user input
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        bool jumpDown = Input.GetButtonDown("Jump");
        float mouseX = Input.GetAxis("Mouse X");
        bool runKey = Input.GetKey(KeyCode.LeftShift);

        // Send input to server
        SendInputServerRpc(new Vector2(h, v), mouseX * rotationSpeed, jumpDown, runKey);

    }

    void FixedUpdate()
    {
        if (!IsServer) return;

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

    void ColorOwner()
    {
        float hue = OwnerClientId % 16 / 16f;
        Color c = Color.HSVToRGB(hue, 0.7f, 0.9f);
        var r = GetComponent<Renderer>();
        if (r) r.material.color = c;
    }

    [ServerRpc(Delivery = RpcDelivery.Reliable)]
    void SendInputServerRpc(Vector2 move, float yawPerSec, bool jumpEdge, bool runKey)
    {
        lastMoveInput = move;
        lastYawDelta = yawPerSec;
        if (jumpEdge) jumpPressed = true;
        runKeyHeld = runKey;
    }

    // [ServerRpc(RequireOwnership = false)]
    // private void SetNameServerRpc(string newName)
    // {
    //     Debug.Log("SetNameServerRpc: " + newName);
    //     PlayerName.Value = new FixedString128Bytes(newName);
    // }

}
