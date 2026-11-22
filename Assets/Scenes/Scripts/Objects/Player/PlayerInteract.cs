using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactionRadius = 2.0f;
    [SerializeField] private LayerMask worldItemsLayer;
    [SerializeField] private LayerMask interactablesLayer;

    [Header("System References")]
    [SerializeField] private PlantPlantsScript plantSystem;

    private void Awake()
    {
        if (plantSystem == null) plantSystem = FindFirstObjectByType<PlantPlantsScript>();
    }

    private void Update()
    {
        if (UIManager.Instance != null && UIManager.Instance.IsGameplayInputBlocked)
        {
            if (NPCNameplateUI.Instance != null)
                NPCNameplateUI.Instance.Hide();
            return;
        }

        UpdateInteractableHighlight();

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryInteract();
        }
    }

    private void UpdateInteractableHighlight()
    {
        if (NPCNameplateUI.Instance == null) return;

        IInteractable closest = FindClosestInteractable();
        if (closest is MonoBehaviour mb && mb.TryGetComponent(out NPCInteractable npc))
        {
            NPCNameplateUI.Instance.ShowForTarget(npc.transform, npc.DisplayName, true);
        }
        else
        {
            NPCNameplateUI.Instance.Hide();
        }
    }

    public bool TryOpenNearbyChest()
    {
        if (interactablesLayer.value == 0) return false;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactionRadius, interactablesLayer);
        ChestStorage bestChest = null;
        float bestDistSqr = float.MaxValue;
        Vector2 origin = transform.position;

        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent(out ChestStorage chest))
                chest = hit.GetComponentInParent<ChestStorage>();

            if (chest == null || !chest.CanInteract(gameObject))
                continue;

            float distSqr = ((Vector2)hit.transform.position - origin).sqrMagnitude;
            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                bestChest = chest;
            }
        }

        if (bestChest != null)
        {
            bestChest.Interact(gameObject);
            return true;
        }

        return false;
    }

    private void TryInteract()
    {
        ItemInWorldManager closestItem = FindClosestWorldItem();
        if (closestItem != null)
        {
            closestItem.Pickup();
            return;
        }

        IInteractable closestInteractable = FindClosestInteractable();
        if (closestInteractable != null)
        {
            closestInteractable.Interact(gameObject);
            return;
        }

        if (plantSystem != null && plantSystem.TryHarvestPlant(transform.position))
        {
            return;
        }
    }

    private ItemInWorldManager FindClosestWorldItem()
    {
        if (worldItemsLayer.value == 0) return null;

        Collider2D[] itemHits = Physics2D.OverlapCircleAll(transform.position, interactionRadius, worldItemsLayer);
        ItemInWorldManager bestItem = null;
        float bestDistSqr = float.MaxValue;
        Vector2 origin = transform.position;

        foreach (var hit in itemHits)
        {
            if (!hit.TryGetComponent(out ItemInWorldManager itemInWorld))
                itemInWorld = hit.GetComponentInParent<ItemInWorldManager>();

            if (itemInWorld == null) continue;

            float distSqr = ((Vector2)hit.transform.position - origin).sqrMagnitude;
            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                bestItem = itemInWorld;
            }
        }

        return bestItem;
    }

    private IInteractable FindClosestInteractable()
    {
        if (interactablesLayer.value == 0) return null;

        Collider2D[] interactableHits = Physics2D.OverlapCircleAll(transform.position, interactionRadius, interactablesLayer);
        IInteractable bestTarget = null;
        float bestDistSqr = float.MaxValue;
        Vector2 origin = transform.position;

        foreach (var hit in interactableHits)
        {
            if (!hit.TryGetComponent(out IInteractable interactable))
                interactable = hit.GetComponentInParent<IInteractable>();

            if (interactable == null || interactable is ChestStorage || !interactable.CanInteract(gameObject))
                continue;

            float distSqr = ((Vector2)hit.transform.position - origin).sqrMagnitude;
            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                bestTarget = interactable;
            }
        }

        return bestTarget;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}