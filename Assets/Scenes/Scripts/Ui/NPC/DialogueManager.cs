using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("Text Settings")]
    [SerializeField] private float typingSpeed = 0.025f;
    [SerializeField] private DialogueUI dialogueUI;

    public bool IsDialogueActive { get; private set; }

    public event Action<string, NPCInteractable> OnGlobalDialogueAction;
    public Func<DialogueCondition, bool> CustomConditionEvaluator;

    private DialogueData currentDialogue;
    private DialogueNode currentNode;
    private NPCInteractable currentNPC;

    private Coroutine typingCoroutine;
    private bool isTyping = false;
    private bool isShowingChoices = false;
    private int dialogueStartFrame = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        if (!IsDialogueActive) return;
        if (Time.frameCount == dialogueStartFrame) return;

        bool advancePressed = Keyboard.current != null &&
                              (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame);

        if (advancePressed)
        {
            if (isTyping)
            {
                CompleteTypingImmediately();
            }
            else if (!isShowingChoices && currentNode != null)
            {
                List<DialogueChoice> availableChoices = GetAvailableChoices(currentNode);
                if (availableChoices.Count > 0)
                {
                    isShowingChoices = true;
                    if (dialogueUI != null)
                        dialogueUI.DisplayChoices(availableChoices, OnChoiceSelected);
                }
                else
                {
                    AdvanceToNextNode();
                }
            }
        }
    }

    public void StartDialogue(DialogueData dialogue, NPCInteractable npc = null)
    {
        if (dialogue == null || dialogue.nodes == null || dialogue.nodes.Count == 0) return;

        currentDialogue = dialogue;
        currentNPC = npc;
        IsDialogueActive = true;
        dialogueStartFrame = Time.frameCount;

        if (NPCNameplateUI.Instance != null)
            NPCNameplateUI.Instance.Hide();

        if (UIManager.Instance != null)
            UIManager.Instance.SetDialogueState(true);

        if (dialogueUI != null)
        {
            dialogueUI.Show();
            string speaker = npc != null && !string.IsNullOrEmpty(npc.DisplayName)
                ? npc.DisplayName
                : dialogue.npcName;
            dialogueUI.SetSpeaker(speaker, dialogue.npcPortrait);
        }

        DialogueNode startNode = dialogue.GetStartNode(EvaluateCondition);
        ShowNode(startNode);
    }

    public void ShowNode(DialogueNode node)
    {
        if (node == null)
        {
            EndDialogue();
            return;
        }

        currentNode = node;
        isShowingChoices = false;

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        List<DialogueChoice> availableChoices = GetAvailableChoices(node);

        if (string.IsNullOrEmpty(node.text) && availableChoices.Count > 0)
        {
            isTyping = false;
            isShowingChoices = true;
            if (dialogueUI != null)
                dialogueUI.DisplayChoices(availableChoices, OnChoiceSelected);
            return;
        }

        typingCoroutine = StartCoroutine(TypeTextRoutine(node.text));
    }

    private void AdvanceToNextNode()
    {
        if (currentNode == null || currentDialogue == null)
        {
            EndDialogue();
            return;
        }

        if (currentNode.endDialogueHere)
        {
            EndDialogue();
            return;
        }

        DialogueNode nextNode = !string.IsNullOrEmpty(currentNode.nextNodeId)
            ? currentDialogue.GetNode(currentNode.nextNodeId)
            : currentDialogue.GetNextSequentialNode(currentNode);

        if (nextNode != null)
            ShowNode(nextNode);
        else
            EndDialogue();
    }

    private IEnumerator TypeTextRoutine(string fullText)
    {
        isTyping = true;
        if (dialogueUI != null)
        {
            dialogueUI.ClearChoices();
            dialogueUI.SetBodyText(string.Empty);
        }

        string safeText = fullText ?? string.Empty;
        for (int i = 0; i <= safeText.Length; i++)
        {
            if (dialogueUI != null)
                dialogueUI.SetBodyText(safeText.Substring(0, i));

            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;
        typingCoroutine = null;
    }

    private void CompleteTypingImmediately()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        isTyping = false;
        if (dialogueUI != null && currentNode != null)
            dialogueUI.SetBodyText(currentNode.text ?? string.Empty);
    }

    private List<DialogueChoice> GetAvailableChoices(DialogueNode node)
    {
        List<DialogueChoice> result = new List<DialogueChoice>();
        if (node.choices == null) return result;

        foreach (var choice in node.choices)
        {
            if (choice.conditions == null || choice.conditions.Count == 0 ||
                choice.conditions.TrueForAll(EvaluateCondition))
            {
                result.Add(choice);
            }
        }
        return result;
    }

    public bool EvaluateCondition(DialogueCondition cond)
    {
        if (cond == null) return true;

        if (cond.type == ConditionType.HasItem)
        {
            if (cond.itemParam == null || InventoryManager.instance == null) return false;
            return InventoryManager.instance.HasItem(cond.itemParam, cond.intParam);
        }

        if (CustomConditionEvaluator != null)
        {
            return CustomConditionEvaluator(cond);
        }

        return true;
    }

    private void OnChoiceSelected(DialogueChoice choice)
    {
        if (choice == null)
        {
            EndDialogue();
            return;
        }

        if (!string.IsNullOrEmpty(choice.actionTriggerId))
        {
            if (currentNPC != null)
                currentNPC.TriggerAction(choice.actionTriggerId);

            OnGlobalDialogueAction?.Invoke(choice.actionTriggerId, currentNPC);
        }

        if (string.IsNullOrEmpty(choice.nextNodeId))
        {
            EndDialogue();
        }
        else
        {
            DialogueNode nextNode = currentDialogue.GetNode(choice.nextNodeId);
            ShowNode(nextNode);
        }
    }

    public void EndDialogue()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        IsDialogueActive = false;
        isTyping = false;
        isShowingChoices = false;
        currentDialogue = null;
        currentNode = null;
        currentNPC = null;

        if (dialogueUI != null)
            dialogueUI.Hide();

        if (UIManager.Instance != null)
            UIManager.Instance.SetDialogueState(false);
    }
}