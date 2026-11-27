using System;
using System.Collections.Generic;
using UnityEngine;

// Список емоцій для портретів NPC
public enum DialogueEmotion
{
    Default,
    Smile,
    Angry,
    Sad,
    Surprised
}

// Пара спрайтів для однієї емоції (звичайний + з відкритим ротом)
[Serializable]
public class EmotionPortrait
{
    public DialogueEmotion emotion = DialogueEmotion.Default;
    public Sprite idleSprite;
    public Sprite talkingSprite;
}

// Створює пункт в меню Unity щоб робити файли діалогів
[CreateAssetMenu(fileName = "NewDialogue", menuName = "Dialogue/Conversation")]
public class DialogueData : ScriptableObject
{
    public string npcName;
    public Sprite npcPortrait;

    [Header("Emotion Portraits")]
    public List<EmotionPortrait> emotionPortraits = new List<EmotionPortrait>();

    // Список точок старту
    public List<DialogueEntryPoint> entryPoints = new List<DialogueEntryPoint>();

    // Усі репліки які є в цьому діалозі
    public List<DialogueNode> nodes = new List<DialogueNode>();

    // Повертає пару спрайтів для вибраної емоції
    public EmotionPortrait GetEmotionPortrait(DialogueEmotion emotion)
    {
        if (emotionPortraits != null)
        {
            for (int i = 0; i < emotionPortraits.Count; i++)
            {
                if (emotionPortraits[i].emotion == emotion)
                {
                    return emotionPortraits[i];
                }
            }

            // Якщо потрібну емоцію не знайдено — повертаємо Default або першу зі списку
            for (int i = 0; i < emotionPortraits.Count; i++)
            {
                if (emotionPortraits[i].emotion == DialogueEmotion.Default)
                {
                    return emotionPortraits[i];
                }
            }

            if (emotionPortraits.Count > 0)
            {
                return emotionPortraits[0];
            }
        }

        return null;
    }

    // Шукає з якої репліки почати діалог при розмові з NPC
    public DialogueNode GetStartNode()
    {
        if (entryPoints != null)
        {
            for (int i = 0; i < entryPoints.Count; i++)
            {
                DialogueEntryPoint entry = entryPoints[i];
                bool allConditionsOk = true;

                if (entry.conditions != null && entry.conditions.Count > 0)
                {
                    for (int j = 0; j < entry.conditions.Count; j++)
                    {
                        DialogueCondition cond = entry.conditions[j];
                        bool isOk = DialogueManager.Instance.EvaluateCondition(cond);

                        if (!isOk)
                        {
                            allConditionsOk = false;
                            break;
                        }
                    }
                }

                if (allConditionsOk)
                {
                    DialogueNode found = GetNode(entry.startNodeId);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
        }

        if (nodes.Count > 0)
        {
            return nodes[0];
        }
        return null;
    }

    // Знаходить потрібну репліку (нод) у списку за її текстовим ID
    public DialogueNode GetNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId) && nodes.Count > 0)
        {
            return nodes[0];
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].nodeId == nodeId)
            {
                return nodes[i];
            }
        }

        return null;
    }

    // Бере наступну по списку репліку
    public DialogueNode GetNextSequentialNode(DialogueNode current)
    {
        if (current == null || nodes == null)
        {
            return null;
        }

        int idx = nodes.IndexOf(current);

        if (idx >= 0 && idx + 1 < nodes.Count)
        {
            return nodes[idx + 1];
        }

        return null;
    }
}

// Налаштування однієї точки входу в діалог
[Serializable]
public class DialogueEntryPoint
{
    public string startNodeId;
    public List<DialogueCondition> conditions = new List<DialogueCondition>();
}

// Одна репліка NPC у діалозі
[Serializable]
public class DialogueNode
{
    public string nodeId = "start";
    public DialogueEmotion emotion = DialogueEmotion.Default;

    [TextArea(3, 6)]
    public string text;

    public string nextNodeId;
    public bool endDialogueHere = false;

    public List<DialogueChoice> choices = new List<DialogueChoice>();
}

// Один варіант відповіді для гравця
[Serializable]
public class DialogueChoice
{
    public string choiceText;
    public string nextNodeId;
    public string actionTriggerId;
    public List<DialogueCondition> conditions = new List<DialogueCondition>();
}

// Одна умова перевірки
[Serializable]
public class DialogueCondition
{
    public ConditionType type;
    public string stringParam;
    public ItemData itemParam;
    public int intParam = 1;
}

// Список усіх можливих типів перевірок у діалозі
public enum ConditionType
{
    HasFlag,
    NotHasFlag,
    HasItem,
    QuestNotStarted,
    QuestInProgress,
    QuestReadyToTurnIn,
    QuestCompleted
}