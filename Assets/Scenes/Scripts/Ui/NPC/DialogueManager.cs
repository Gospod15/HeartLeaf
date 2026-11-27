using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("Text & Portrait Settings")]
    [SerializeField] private float typingSpeed = 0.025f;
    [SerializeField] private float mouthToggleInterval = 0.09f;
    [SerializeField] private DialogueUI dialogueUI;

    public bool IsDialogueActive { get; private set; }

    public event Action<string, NPCInteractable> OnGlobalDialogueAction;
    public Func<DialogueCondition, bool> CustomConditionEvaluator;

    private DialogueData currentDialogue;
    private DialogueNode currentNode;
    private NPCInteractable currentNPC;

    private Sprite currentIdleSprite;
    private Sprite currentTalkingSprite;

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
        if (!IsDialogueActive || Time.frameCount == dialogueStartFrame)
        {
            return;
        }

        if (Keyboard.current == null)
        {
            return;
        }

        bool advancePressed = Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame;
        if (!advancePressed)
        {
            return;
        }

        if (isTyping)
        {
            CompleteTypingImmediately();
            return;
        }

        if (!isShowingChoices && currentNode != null)
        {
            List<DialogueChoice> availableChoices = GetAvailableChoices(currentNode);
            if (availableChoices.Count > 0)
            {
                ShowChoices(availableChoices);
            }
            else
            {
                AdvanceToNextNode();
            }
        }
    }

    public void StartDialogue(DialogueData dialogue, NPCInteractable npc = null)
    {
        if (dialogue == null || dialogue.nodes == null || dialogue.nodes.Count == 0)
        {
            return;
        }

        currentDialogue = dialogue;
        currentNPC = npc;
        IsDialogueActive = true;
        dialogueStartFrame = Time.frameCount;

        if (NPCNameplateUI.Instance != null)
        {
            NPCNameplateUI.Instance.Hide();
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.SetDialogueState(true);
        }

        if (dialogueUI != null)
        {
            dialogueUI.Show();

            string speaker = dialogue.npcName;
            if (npc != null && !string.IsNullOrEmpty(npc.DisplayName))
            {
                speaker = npc.DisplayName;
            }

            dialogueUI.SetSpeaker(speaker, dialogue.npcPortrait);
        }

        DialogueNode startNode = dialogue.GetStartNode();
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
        StopTypingCoroutine();

        UpdatePortraitSpritesForNode(node);

        List<DialogueChoice> availableChoices = GetAvailableChoices(node);
        if (string.IsNullOrEmpty(node.text) && availableChoices.Count > 0)
        {
            isTyping = false;
            ShowChoices(availableChoices);
            return;
        }

        typingCoroutine = StartCoroutine(TypeTextRoutine(node.text));
    }

    // Підбирає пару спрайтів (звичайний + розмовний) для поточної репліки
    private void UpdatePortraitSpritesForNode(DialogueNode node)
    {
        currentIdleSprite = currentDialogue != null ? currentDialogue.npcPortrait : null;
        currentTalkingSprite = currentIdleSprite;

        if (currentDialogue != null)
        {
            EmotionPortrait pair = currentDialogue.GetEmotionPortrait(node.emotion);
            if (pair != null)
            {
                if (pair.idleSprite != null)
                    currentIdleSprite = pair.idleSprite;

                currentTalkingSprite = pair.talkingSprite != null ? pair.talkingSprite : currentIdleSprite;
            }
        }

        if (dialogueUI != null)
        {
            dialogueUI.SetPortrait(currentIdleSprite);
        }
    }

    private void AdvanceToNextNode()
    {
        if (currentNode == null || currentDialogue == null || currentNode.endDialogueHere)
        {
            EndDialogue();
            return;
        }

        DialogueNode nextNode;
        if (!string.IsNullOrEmpty(currentNode.nextNodeId))
        {
            nextNode = currentDialogue.GetNode(currentNode.nextNodeId);
        }
        else
        {
            nextNode = currentDialogue.GetNextSequentialNode(currentNode);
        }

        if (nextNode != null)
        {
            ShowNode(nextNode);
        }
        else
        {
            EndDialogue();
        }
    }

    // Друкує текст по літерах і анімує відкриття/закриття рота
    private IEnumerator TypeTextRoutine(string fullText)
    {
        isTyping = true;

        if (dialogueUI != null)
        {
            dialogueUI.ClearChoices();
            dialogueUI.SetBodyText(string.Empty);
            dialogueUI.SetPortrait(currentIdleSprite);
        }

        string safeText = fullText ?? string.Empty;
        WaitForSeconds wait = new WaitForSeconds(typingSpeed);

        float mouthTimer = 0f;
        bool mouthOpen = false;

        for (int i = 0; i <= safeText.Length; i++)
        {
            if (dialogueUI != null)
            {
                dialogueUI.SetBodyText(safeText.Substring(0, i));

                // Перевіряємо поточну літеру: на пробілах і розділових знаках рот закритий
                if (i > 0 && i <= safeText.Length)
                {
                    char currentChar = safeText[i - 1];
                    if (char.IsLetterOrDigit(currentChar))
                    {
                        mouthTimer += typingSpeed;
                        if (mouthTimer >= mouthToggleInterval)
                        {
                            mouthTimer = 0f;
                            mouthOpen = !mouthOpen;
                            dialogueUI.SetPortrait(mouthOpen ? currentTalkingSprite : currentIdleSprite);
                        }
                    }
                    else
                    {
                        mouthOpen = false;
                        dialogueUI.SetPortrait(currentIdleSprite);
                    }
                }
            }

            yield return wait;
        }

        // Після завершення фрази закриваємо рот
        if (dialogueUI != null)
        {
            dialogueUI.SetPortrait(currentIdleSprite);
        }

        isTyping = false;
        typingCoroutine = null;
    }

    private void CompleteTypingImmediately()
    {
        StopTypingCoroutine();
        isTyping = false;

        if (dialogueUI != null && currentNode != null)
        {
            dialogueUI.SetBodyText(currentNode.text ?? string.Empty);
            dialogueUI.SetPortrait(currentIdleSprite);
        }
    }

    private List<DialogueChoice> GetAvailableChoices(DialogueNode node)
    {
        List<DialogueChoice> result = new List<DialogueChoice>();
        if (node.choices == null)
        {
            return result;
        }

        for (int i = 0; i < node.choices.Count; i++)
        {
            DialogueChoice choice = node.choices[i];
            if (AreConditionsMet(choice.conditions))
            {
                result.Add(choice);
            }
        }

        return result;
    }

    private bool AreConditionsMet(List<DialogueCondition> conditions)
    {
        if (conditions == null || conditions.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < conditions.Count; i++)
        {
            if (!EvaluateCondition(conditions[i]))
            {
                return false;
            }
        }

        return true;
    }

    public bool EvaluateCondition(DialogueCondition cond)
    {
        if (cond == null)
        {
            return true;
        }

        if (cond.type == ConditionType.HasItem)
        {
            if (cond.itemParam == null || InventoryManager.instance == null)
            {
                return false;
            }
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
            {
                currentNPC.TriggerAction(choice.actionTriggerId);
            }
            OnGlobalDialogueAction?.Invoke(choice.actionTriggerId, currentNPC);
        }

        if (string.IsNullOrEmpty(choice.nextNodeId))
        {
            EndDialogue();
            return;
        }

        DialogueNode nextNode = currentDialogue.GetNode(choice.nextNodeId);
        ShowNode(nextNode);
    }

    public void EndDialogue()
    {
        StopTypingCoroutine();

        IsDialogueActive = false;
        isTyping = false;
        isShowingChoices = false;
        currentDialogue = null;
        currentNode = null;
        currentNPC = null;

        if (dialogueUI != null)
        {
            dialogueUI.Hide();
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.SetDialogueState(false);
        }
    }

    private void ShowChoices(List<DialogueChoice> choices)
    {
        isShowingChoices = true;
        if (dialogueUI != null)
        {
            dialogueUI.DisplayChoices(choices, OnChoiceSelected);
        }
    }

    private void StopTypingCoroutine()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
    }
}