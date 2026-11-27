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

    //отримання імені NPC (локального або з файлу діалогу)
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrEmpty(npcDisplayName))
            {
                return npcDisplayName;
            }
            if (defaultDialogue != null)
            {
                return defaultDialogue.npcName;
            }
            return string.Empty;
        }
    }

    //текст підказки при наближення до NPC
    public string InteractionPrompt
    {
        get
        {
            if (!string.IsNullOrEmpty(interactionPrompt))
            {
                return $"{interactionPrompt} ({DisplayName})";
            }
            return DisplayName;
        }
    }

    private void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    //перевірка чи можна почати розмову
    public bool CanInteract(GameObject interactor)
    {
        if (defaultDialogue == null || DialogueManager.Instance == null)
        {
            return false;
        }
        return !DialogueManager.Instance.IsDialogueActive;
    }

    //початок взаємодії з NPC
    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        DialogueManager.Instance.StartDialogue(defaultDialogue, this);
    }

    //зміна поточного діалогу NPC під час гри
    public void SetDialogue(DialogueData newDialogue)
    {
        defaultDialogue = newDialogue;
    }

    //запуск події з діалогу на цьому NPC
    public void TriggerAction(string actionId)
    {
        if (!string.IsNullOrEmpty(actionId))
        {
            onDialogueAction?.Invoke(actionId);
        }
    }
}