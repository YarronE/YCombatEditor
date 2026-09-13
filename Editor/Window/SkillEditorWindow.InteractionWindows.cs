using System;
using Ethan.ActionEditor;
using UnityEditor;
using UnityEngine;

public partial class SkillEditorWindow
{
    string UniqueWindowId(string prefix)
    {
        string id = prefix;
        int suffix = 1;
        while (interactionWindows.Exists(w => w != null && w.id == id)) id = prefix + "-" + suffix++;
        return id;
    }

    void AddInteractionHere()
    {
        PushUndo();
        interactionWindows.Add(new InteractionWindow {
            id = UniqueWindowId("interaction"), keyNumber = frameSelectIndex,
            endKeyNumber = frameSelectIndex + 5, damageMultiplier = 1, suppressHitReaction = false });
        selType = SelType.Interaction;
        selIdx = interactionWindows.Count - 1;
    }

    void DrawInteractionInspector(int index)
    {
        EditorGUILayout.HelpBox("Inclusive frame window. Matching signals evaluate reusable conditions; the highest-priority valid window wins.", MessageType.None);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Evade preset")) ApplyInteractionPreset(index, InteractionResponse.Evade);
        if (GUILayout.Button("Block preset")) ApplyInteractionPreset(index, InteractionResponse.Block);
        if (GUILayout.Button("Parry preset")) ApplyInteractionPreset(index, InteractionResponse.Parry);
        EditorGUILayout.EndHorizontal();
        DrawSerializedSelection("interactionWindows", index);
        EditorGUILayout.HelpBox("All conditions must pass. Missing references block playback. Create reusable conditions through Create > Combat > Interaction Conditions. The host submits signals through TryResolveInteraction.", MessageType.None);
    }

    void ApplyInteractionPreset(int index, InteractionResponse response)
    {
        if (index < 0 || index >= interactionWindows.Count) return;
        RunAuthoringOperation("Apply interaction preset", () => {
            var w = interactionWindows[index];
            w.id = UniqueWindowId(response.ToString().ToLowerInvariant());
            w.signal = InteractionQuery.IncomingHit;
            w.response = response;
            w.priority = response == InteractionResponse.Parry ? 30 : response == InteractionResponse.Evade ? 20 : 10;
            w.sourceAngle = response == InteractionResponse.Evade ? 360 : 120;
            w.damageMultiplier = response == InteractionResponse.Block ? .2f : 0;
            w.suppressHitReaction = true;
            w.maxActivations = 0;
        });
    }
}
