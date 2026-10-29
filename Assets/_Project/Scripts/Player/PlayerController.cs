using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 60f;
    [SerializeField] private float deceleration = 80f;

    private Rigidbody2D rb;
    private InputAction moveAction;
    private Vector2 moveInput;

    /// <summary>Last non-zero movement direction (used later for animation and combat).</summary>
    public Vector2 Facing { get; private set; } = Vector2.down;

    public Vector2 Velocity => rb != null ? rb.linearVelocity : Vector2.zero;

    /// <summary>Set to true by question UI, cutscenes, death, etc. to freeze movement.</summary>
    public bool InputLocked { get; set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        BuildInput();
    }

    /// <summary>Instantly moves the player (room transitions) and kills any momentum.</summary>
    public void Teleport(Vector2 position)
    {
        transform.position = position;
        rb.position = position;
        rb.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
    }

    private void BuildInput()
    {
        moveAction = new InputAction("Move", InputActionType.Value);

        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");

        moveAction.AddBinding("<Gamepad>/leftStick");
        moveAction.AddBinding("<Gamepad>/dpad");
    }

    private void OnEnable()
    {
        moveAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        moveInput = Vector2.zero;
    }

    private void OnDestroy()
    {
        moveAction?.Dispose();
    }

    private void Update()
    {
        if (InputLocked)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);

        if (moveInput.sqrMagnitude > 0.01f)
            Facing = moveInput.normalized;
    }

    private void FixedUpdate()
    {
        Vector2 target = moveInput * moveSpeed;
        float rate = moveInput.sqrMagnitude > 0.01f ? acceleration : deceleration;
        rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, target, rate * Time.fixedDeltaTime);
    }
}
