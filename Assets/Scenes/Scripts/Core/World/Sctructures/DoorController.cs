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

    [Header("State & Prompts")]
    [SerializeField] private bool startsOpen = false;
    [SerializeField] private string openPrompt = "Відчинити двері";
    [SerializeField] private string closePrompt = "Зачинити двері";

    [Header("Optional Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;

    private bool isOpen;

    public bool IsOpen => isOpen;
    public string InteractionPrompt => isOpen ? closePrompt : openPrompt;

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
        return true;
    }

    public void Interact(GameObject interactor)
    {
        Toggle();
    }

    public void Toggle()
    {
        SetState(!isOpen, playSound: true);
    }

    public void SetState(bool open, bool playSound = false)
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
}