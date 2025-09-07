using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;

public class SantaFirstPersonController : MonoBehaviourPunCallbacks, IPunObservable
{
    // === Variables and References ===
    [Header("Input Actions")]
    public InputActionAsset inputActions;
    private InputActionMap santaActionMap;
    private Vector2 moveInput;
    private bool isRunningInput;

    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float runMultiplier = 1.5f;
    public float mouseSensitivity = 2f;
    public float turnSpeed = 8f;
    public float moveSmoothTime = 0.1f;

    [Header("Jump Settings")]
    public float jumpForce = 5f;
    public float jumpCooldown = 3f;
    private float lastJumpTime = -3f;
    private bool _isGrounded;
    public LayerMask playerLayer;

    // ... (All other public variables for stamina, camera, etc. go here)
    // You can copy them from your previous script to this section as we build upon it.

    private Rigidbody rb;
    private Animator animator;
    private float xRotation = 0f;

    // === Core Unity Methods ===
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        if (inputActions != null)
        {
            
            if (santaActionMap != null) 
            {
                SetupInputCallbacks();
            }
        }
    }

    void OnEnable()
    {
        if (photonView.IsMine && santaActionMap != null)
        {
            santaActionMap.Enable();
        }
    }

    void OnDisable()
    {
        if (photonView.IsMine && santaActionMap != null)
        {
            santaActionMap.Disable();
        }
    }

    void FixedUpdate()
    {
        if (photonView.IsMine)
        {
            _isGrounded = false;
            // ApplyMovement() will be called from here
        }
    }

    void Update()
    {
        if (photonView.IsMine)
        {
            Look();
            // All other Update logic for local player here
        }
        else
        {
            // Network synchronization logic for other players here
        }
    }

    // === Input Callback Setup ===
    private void SetupInputCallbacks()
    {
        // Move and Run
        santaActionMap.FindAction("Move").performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        santaActionMap.FindAction("Move").canceled += ctx => moveInput = Vector2.zero;
        santaActionMap.FindAction("Run").performed += ctx => isRunningInput = true;
        santaActionMap.FindAction("Run").canceled += ctx => isRunningInput = false;

        // Jump
        santaActionMap.FindAction("Jump").performed += _ =>
        {
            if (Time.time >= lastJumpTime + jumpCooldown)
            {
                photonView.RPC("JumpRPC", RpcTarget.All);
                lastJumpTime = Time.time;
            }
        };

        // Punch
        santaActionMap.FindAction("Punch").performed += _ => Punch();

        // Interact
        santaActionMap.FindAction("Interact").performed += _ => Interact();
    }

    // === Player Actions (RPCs and Local Logic) ===
    private void ApplyMovement()
    {
        // This method will use moveInput and isRunningInput to control movement
    }

    [PunRPC]
    void JumpRPC()
    {
        // Jump logic to be implemented here
    }

    void Punch()
    {
        // Punch logic to be implemented here
    }

    void Interact()
    {
        // Interact logic to be implemented here
    }

    void Look()
    {
        // Camera look logic to be implemented here
    }

    private void OnCollisionStay(Collision collision)
    {
        // Ground check logic to be implemented here
    }

    // === Photon Synchronization ===
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // Data synchronization logic to be implemented here
    }

    // ... (All other RPCs and helper methods from the previous script go here)
}