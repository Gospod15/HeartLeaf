using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class DoorController : MonoBehaviour, IInteractable
{
    [Header("Visuals")]
    [SerializeField] private SpriteRenderer doorRenderer;
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite openSprite;

    [Header("Physics & Interaction Colliders")]
    [SerializeField] private BoxCollider2D interactionTrigger;
    [SerializeField] private BoxCollider2D blockingCollider;

    [Header("Safety Check")]
    [Tooltip("Слои персонажей (Player, PlayerInteractions и т.д.), которых нельзя зажать дверью")]
    [SerializeField] private LayerMask blockerCheckMask = ~0;

    [Header("NPC Navigation Points")]
    [Tooltip("Точка перед дверью со стороны улицы")]
    public Transform outsidePoint;
    [Tooltip("Точка внутри дома сразу за дверью")]
    public Transform insidePoint;

    [Header("State & Prompts")]
    [SerializeField] private bool startsOpen = false;
    [SerializeField] private string openPrompt = "Відчинити двері";
    [SerializeField] private string closePrompt = "Зачинити двері";
    [SerializeField] private string blockedPrompt = "Прохід зайнято";

    [Header("Optional Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;

    private bool isOpen;
    private Coroutine delayedCloseCoroutine;
    private readonly Collider2D[] overlapBuffer = new Collider2D[16];

    public bool IsOpen => isOpen;
    public string InteractionPrompt
    {
        get
        {
            if (!isOpen) return openPrompt;
            return IsDoorwayBlocked() ? blockedPrompt : closePrompt;
        }
    }

    private void Awake()
    {
        if (doorRenderer == null)
            doorRenderer = GetComponent<SpriteRenderer>();

        ResolveColliders();
        SetState(startsOpen, playSound: false);
    }

    private void ResolveColliders()
    {
        if (interactionTrigger != null && blockingCollider != null) return;

        BoxCollider2D[] colliders = GetComponents<BoxCollider2D>();
        foreach (var col in colliders)
        {
            if (col.isTrigger && interactionTrigger == null)
                interactionTrigger = col;
            else if (!col.isTrigger && blockingCollider == null)
                blockingCollider = col;
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        doorRenderer = GetComponent<SpriteRenderer>();

        BoxCollider2D[] colliders = GetComponents<BoxCollider2D>();
        if (colliders.Length == 0)
            interactionTrigger = gameObject.AddComponent<BoxCollider2D>();
        else
            interactionTrigger = colliders[0];

        interactionTrigger.isTrigger = true;

        if (colliders.Length < 2)
            blockingCollider = gameObject.AddComponent<BoxCollider2D>();
        else
            blockingCollider = colliders[1];

        blockingCollider.isTrigger = false;
    }
#endif

    public bool CanInteract(GameObject interactor)
    {
        if (isOpen && IsDoorwayBlocked())
            return false;

        return true;
    }

    public void Interact(GameObject interactor)
    {
        Toggle();
    }

    public void Toggle()
    {
        if (isOpen && IsDoorwayBlocked())
            return;

        SetState(!isOpen, playSound: true);
    }

    public void SetState(bool open, bool playSound = false)
    {
        if (delayedCloseCoroutine != null)
        {
            StopCoroutine(delayedCloseCoroutine);
            delayedCloseCoroutine = null;
        }

        if (!open && isActiveAndEnabled && IsDoorwayBlocked())
        {
            delayedCloseCoroutine = StartCoroutine(WaitAndCloseRoutine(playSound));
            return;
        }

        ApplyDoorState(open, playSound);
    }

    private IEnumerator WaitAndCloseRoutine(bool playSound)
    {
        while (IsDoorwayBlocked())
        {
            yield return new WaitForSeconds(0.1f);
        }

        delayedCloseCoroutine = null;
        ApplyDoorState(false, playSound);
    }

    private void ApplyDoorState(bool open, bool playSound)
    {
        isOpen = open;

        if (doorRenderer != null)
            doorRenderer.sprite = isOpen ? openSprite : closedSprite;

        if (interactionTrigger != null)
        {
            interactionTrigger.isTrigger = true;
            interactionTrigger.enabled = true;
        }

        if (blockingCollider != null)
        {
            blockingCollider.isTrigger = false;
            blockingCollider.enabled = !isOpen;
        }

        if (playSound && audioSource != null)
        {
            AudioClip clip = isOpen ? openSound : closeSound;
            if (clip != null)
                audioSource.PlayOneShot(clip);
        }
    }

    public bool IsDoorwayBlocked()
    {
        if (blockingCollider == null) return false;

        Vector2 worldCenter = blockingCollider.transform.TransformPoint(blockingCollider.offset);
        Vector2 lossyScale = blockingCollider.transform.lossyScale;
        Vector2 worldSize = new Vector2(
            Mathf.Abs(blockingCollider.size.x * lossyScale.x) + 0.08f,
            Mathf.Abs(blockingCollider.size.y * lossyScale.y) + 0.08f
        );
        float angle = blockingCollider.transform.eulerAngles.z;

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false; 
        filter.SetLayerMask(blockerCheckMask);

        int count = Physics2D.OverlapBox(worldCenter, worldSize, angle, filter, overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider2D col = overlapBuffer[i];
            if (col == null) continue;
            if (col.transform == transform || col.transform.IsChildOf(transform)) continue;

            if (col.CompareTag("Player") || col.GetComponentInParent<NpcMovement>() != null)
            {
                return true;
            }
        }

        return false;
    }
}