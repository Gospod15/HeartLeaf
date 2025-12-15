using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Здоров'я та Голод")]
    public float maxHealth = 100.0f;
    public float currentHealth;
    
    public float maxFeed = 100.0f;
    public float currentFeed;
    
    public float FeedDrain = 0.02f; 
    public float starvationDamage = 1f;

    [Header("Стаміна")]
    public float maxStamina = 100f;
    public float currentStamina;
    public float staminaRegen = 7f;
    public float staminaDrain = 15f; 

    [Header("UI")]
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI feedText; 
    public TextMeshProUGUI staminaText;
    public Image UIInventory; 
    public Image UIInventoryArmory; 

    [Header("Збирання (Руками на E)")]
    public float interactionRadius = 2.0f; 
    public LayerMask interactionLayer;

    private ToolController toolController;
    private Rigidbody2D rb;
    private Animator animator;
    
    private Vector2 movementInput;
    public float runSpeed = 7f;
    public float walkSpeed = 4f;
    private float currentSpeed;

void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        toolController = GetComponent<ToolController>(); 

        currentHealth = maxHealth;
        currentFeed = maxFeed;
        currentStamina = maxStamina;
        currentSpeed = walkSpeed;

        if (UIInventory != null) 
        {
            UIInventory.gameObject.SetActive(false);
        }
        
        UpdateUI();
    }

    void Update()
    {
        if (currentHealth <= 0) {Debug.Log("Ви померли"); return;}

        var keyboard = Keyboard.current;

        movementInput = Vector2.zero;
        if (keyboard.wKey.isPressed) movementInput.y += 1;
        if (keyboard.sKey.isPressed) movementInput.y -= 1;
        if (keyboard.aKey.isPressed) movementInput.x -= 1;
        if (keyboard.dKey.isPressed) movementInput.x += 1;
        movementInput.Normalize();

        if (keyboard.iKey.wasPressedThisFrame && UIInventory != null)
        {
            bool isActive = !UIInventory.gameObject.activeSelf;
            UIInventory.gameObject.SetActive(isActive);
            if (isActive && InventoryManager.instance != null) InventoryManager.instance.RefreshUI();
            if (!isActive && ContextMenuController.instance != null) ContextMenuController.instance.ClearSelection();
        }

        if (keyboard.eKey.wasPressedThisFrame)
        {
            TryInteract();
        }

        bool isSprinting = keyboard.shiftKey.isPressed;
        HandleMovementAndStamina(isSprinting);
        
        HandleHunger();
        UpdateAnimation();
    }

    void FixedUpdate() 
    { 
        if (currentHealth > 0) 
        {
            rb.MovePosition(rb.position + movementInput * currentSpeed * Time.fixedDeltaTime); 
        }
    }

    void TryInteract() 
    { 
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactionRadius, interactionLayer);
        foreach (var hit in hits)
        {
            ItemInWorldManager itemInWorld = hit.GetComponent<ItemInWorldManager>();
            
            if (itemInWorld != null)
            {
                itemInWorld.Pickup();
                return; 
            }
        }
        
        if (WorldGenerator.instance != null) 
        { 
            bool mined = WorldGenerator.instance.TryMineStone(transform.position);
            if (mined) return; 
        } 

        if (WorldGenerator.instance != null)
        {
            PlantPlantsScript plantScript = WorldGenerator.instance.GetComponent<PlantPlantsScript>();
            
            if (plantScript != null)
            {
                bool harvested = plantScript.TryHarvestPlant(transform.position);
                if (harvested) return;
            }
        }
    }

    public void EquipItem(ItemData item) 
    { 
        if (toolController != null) toolController.Equip(item); 
    }

    public void ReduceStamina(float amount) 
    { 
        currentStamina -= amount; 
        if (currentStamina < 0) currentStamina = 0; 
        UpdateUI(); 
    }

    public void Eat(float amount) 
    { 
        currentFeed += amount; 
        if (currentFeed > maxFeed) currentFeed = maxFeed; 
        UpdateUI(); 
    }

    void HandleHunger() 
    { 
        if (currentFeed > 0) 
        {
            currentFeed -= FeedDrain/20 * Time.deltaTime; 
        }
        else 
        { 
            currentFeed = 0; 
            TakeDamage(starvationDamage * Time.deltaTime); 
        } 
        UpdateUI(); 
    }

    public void TakeDamage(float damage) 
    { 
        currentHealth -= damage; 
        UpdateUI(); 
        if (currentHealth <= 0) Die(); 
    }

    //Функція бігу та витривалості
    void HandleMovementAndStamina(bool isSprinting) 
    { 
        bool isMoving = movementInput.sqrMagnitude > 0; 
        bool isStarving = currentFeed <= 0; 

        if (isMoving && isSprinting && currentStamina > 0 && !isStarving) 
        { 
            currentSpeed = runSpeed; 
            currentStamina -= staminaDrain * Time.deltaTime; 
        } 
        else 
        { 
            currentSpeed = walkSpeed; 
            if (currentStamina < maxStamina) 
            {
                currentStamina += staminaRegen * Time.deltaTime; 
            }
        } 
        
        if (currentStamina < 0) currentStamina = 0;
        if (currentStamina > maxStamina) currentStamina = maxStamina;
    }

    void Die() 
    { 
        if (animator != null) animator.SetBool("IsDead", true); 
        this.enabled = false; 
        Debug.Log("Гравець помер.");
    }

    void UpdateAnimation() 
    { 
        if (animator != null) 
        { 
            animator.SetFloat("X", movementInput.x); 
            animator.SetFloat("Y", movementInput.y); 
            animator.SetFloat("Speed", movementInput.sqrMagnitude); 
        } 
    }

    void UpdateUI() 
    { 
        if (healthText != null) healthText.text = currentHealth.ToString("F0"); 
        if (feedText != null) feedText.text = currentFeed.ToString("F0"); 
        if (staminaText != null) staminaText.text = currentStamina.ToString("F0"); 
    }
}