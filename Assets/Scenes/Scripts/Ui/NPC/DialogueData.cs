using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewDialogue", menuName = "Dialogue/Conversation")]
public class DialogueData : ScriptableObject
{
    public string npcName;
    public Sprite npcPortrait;

    public List<DialogueEntryPoint> entryPoints = new List<DialogueEntryPoint>();

    public List<DialogueNode> nodes = new List<DialogueNode>();

    public DialogueNode GetStartNode(Func<DialogueCondition, bool> conditionEvaluator)
    {
        if (entryPoints != null && conditionEvaluator != null)
        {
            foreach (var entry in entryPoints)
            {
                if (entry.conditions == null || entry.conditions.Count == 0 ||
                    entry.conditions.TrueForAll(c => conditionEvaluator(c)))
                {
                    DialogueNode found = GetNode(entry.startNodeId);
                    if (found != null) return found;
                }
            }
        }

        return nodes.Count > 0 ? nodes[0] : null;
    }

    public DialogueNode GetNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId) && nodes.Count > 0)
            return nodes[0];

        return nodes.Find(n => n.nodeId == nodeId);
    }

    public DialogueNode GetNextSequentialNode(DialogueNode current)
    {
        if (current == null || nodes == null) return null;
        int idx = nodes.IndexOf(current);
        if (idx >= 0 && idx + 1 < nodes.Count)
            return nodes[idx + 1];
        return null;
    }
}

[Serializable]
public class DialogueEntryPoint
{
    public string startNodeId;
    public List<DialogueCondition> conditions = new List<DialogueCondition>();
}

[Serializable]
public class DialogueNode
{
    public string nodeId = "start";

    [TextArea(3, 6)]
    public string text;

    public string nextNodeId;

    public bool endDialogueHere = false;

    public List<DialogueChoice> choices = new List<DialogueChoice>();
}

[Serializable]
public class DialogueChoice
{
    public string choiceText;

    public string nextNodeId;

    public string actionTriggerId;

    public List<DialogueCondition> conditions = new List<DialogueCondition>();
}

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

[Serializable]
public class DialogueCondition
{
    public ConditionType type;
    public string stringParam;
    public ItemData itemParam;
    public int intParam = 1;
}