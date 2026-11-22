using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(PlayerStats))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Speed")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float runSpeed = 7f;
    [SerializeField] private float sprintStaminaCostPerSecond = 15f;

    private Rigidbody2D rb;
    private Animator animator;
    private PlayerStats stats;

    private Vector2 movementInput;
    private float currentSpeed;

    public Vector2 MovementDirection => movementInput;
    public float CurrentSpeed => currentSpeed;
    public bool IsSprinting => currentSpeed > walkSpeed + 0.1f && movementInput.sqrMagnitude > 0.01f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        stats = GetComponent<PlayerStats>();
        currentSpeed = walkSpeed;
    }

    private void OnEnable()
    {
        if (stats == null) stats = GetComponent<PlayerStats>();
        if (stats != null) stats.OnDied += HandleDeath;
    }

    private void OnDisable()
    {
        if (stats != null) stats.OnDied -= HandleDeath;
    }

    private void Update()
    {
        if (stats != null && stats.IsDead) return;

        if (UIManager.Instance != null && UIManager.Instance.IsGameplayInputBlocked)
        {
            movementInput = Vector2.zero;
            UpdateAnimation();
            return;
        }

        ReadInput();
        HandleSprint();
        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        if (stats != null && stats.IsDead) return;
        if (rb != null)
        {
            rb.MovePosition(rb.position + movementInput * currentSpeed * Time.fixedDeltaTime);
        }
    }

    private void ReadInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        float x = 0f;
        float y = 0f;

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;

        movementInput = new Vector2(x, y).normalized;
    }

    private void HandleSprint()
    {
        var keyboard = Keyboard.current;
        bool isSprinting = keyboard != null && keyboard.shiftKey.isPressed;
        bool isMoving = movementInput.sqrMagnitude > 0f;

        if (stats != null && isMoving && isSprinting && !stats.IsStarving && stats.TrySpendStamina(sprintStaminaCostPerSecond * Time.deltaTime))
        {
            currentSpeed = runSpeed;
        }
        else
        {
            currentSpeed = walkSpeed;
            if (stats != null) stats.RegenerateStamina();
        }
    }

    private void UpdateAnimation()
    {
        if (animator == null) return;

        animator.SetFloat("X", movementInput.x);
        animator.SetFloat("Y", movementInput.y);
        animator.SetFloat("Speed", movementInput.sqrMagnitude);
    }

    private void HandleDeath()
    {
        movementInput = Vector2.zero;
        if (animator != null) animator.SetBool("IsDead", true);
        enabled = false;
    }

    public void ReviveMovement()
    {
        enabled = true;
        if (animator != null) animator.SetBool("IsDead", false);
    }
}