using UnityEditor;
using UnityEngine;

namespace Ethan.ActionEditor.Editor
{
    [CustomEditor(typeof(ActionPlayer))]
    public sealed class ActionPlayerInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if(!Application.isPlaying) return;
            var player=(ActionPlayer)target;
            player.InteractionDiagnosticsEnabled=EditorGUILayout.Toggle("Interaction diagnostics",player.InteractionDiagnosticsEnabled);
            EditorGUILayout.LabelField("Action / frame",(player.CurrentConfig==null ? "Idle" : player.CurrentConfig.name)+" / "+player.CurrentFrame);
            if(!string.IsNullOrEmpty(player.LastInteractionRejection)) EditorGUILayout.HelpBox(player.LastInteractionRejection,MessageType.None);
            foreach(var trace in player.LastInteractionEvaluation)
            {
                EditorGUILayout.LabelField($"[{trace.index}] {trace.id} @ {trace.frame}",trace.selected ? "Selected" : trace.accepted ? "Lower priority" : "Rejected");
                if(!string.IsNullOrEmpty(trace.reason)) EditorGUILayout.LabelField(trace.reason,EditorStyles.wordWrappedMiniLabel);
                foreach(var condition in trace.conditions) EditorGUILayout.LabelField(condition.path+": "+condition.accepted+" "+condition.reason,EditorStyles.wordWrappedMiniLabel);
            }
            Repaint();
        }
    }
}
