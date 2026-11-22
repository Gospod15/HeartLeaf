using UnityEngine;
using UnityEngine.Events;

public class NPCInteractable : MonoBehaviour, IInteractable
{
    [Header("NPC Settings")]
    [SerializeField] private string npcDisplayName = "Villager";
    [SerializeField] private string interactionPrompt = "Talk";
    [SerializeField] private DialogueData defaultDialogue;
    [SerializeField] private bool facePlayerOnInteract = true;

    [Header("Dialogue Events")]
    public UnityEvent<string> onDialogueAction;

    private SpriteRenderer spriteRenderer;

    public string DisplayName => !string.IsNullOrEmpty(npcDisplayName)
        ? npcDisplayName
        : (defaultDialogue != null ? defaultDialogue.npcName : string.Empty);

    public string InteractionPrompt => !string.IsNullOrEmpty(interactionPrompt)
        ? $"{interactionPrompt} ({DisplayName})"
        : DisplayName;

    private void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public bool CanInteract(GameObject interactor)
    {
        return defaultDialogue != null &&
               DialogueManager.Instance != null &&
               !DialogueManager.Instance.IsDialogueActive;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor)) return;

        if (facePlayerOnInteract && spriteRenderer != null && interactor != null)
        {
            float dirX = interactor.transform.position.x - transform.position.x;
            if (Mathf.Abs(dirX) > 0.05f)
                spriteRenderer.flipX = dirX < 0f;
        }

        DialogueManager.Instance.StartDialogue(defaultDialogue, this);
    }

    public void SetDialogue(DialogueData newDialogue)
    {
        defaultDialogue = newDialogue;
    }

    public void TriggerAction(string actionId)
    {
        if (!string.IsNullOrEmpty(actionId))
            onDialogueAction?.Invoke(actionId);
    }
}