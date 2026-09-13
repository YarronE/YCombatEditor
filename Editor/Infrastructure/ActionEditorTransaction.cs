using UnityEditor;
using UnityEngine;

namespace Ethan.ActionEditor.Editor
{
    /// <summary>Records every drag update but exposes one native Undo operation per gesture.</summary>
    internal sealed class ActionEditorGesture
    {
        Object target;
        int group = -1;

        internal void Record(Object value, string operation)
        {
            if (value == null) return;
            if (target != value || group < 0)
            {
                Complete();
                Undo.IncrementCurrentGroup();
                group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(operation);
                target = value;
            }
            Undo.RecordObject(value, operation);
        }

        internal void Complete()
        {
            if (group < 0) return;
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            target = null;
            group = -1;
        }
    }

    internal static class ActionEditorTransaction
    {
        internal static void Record(Object target, string operation)
        {
            if (target == null) return;
            Undo.RecordObject(target, operation);
        }

        internal static void MarkChanged(Object target)
        {
            if (target == null) return;
            EditorUtility.SetDirty(target);
        }

        internal static void SaveIfDirty(Object target)
        {
            if (target == null || !EditorUtility.IsDirty(target)) return;
            AssetDatabase.SaveAssetIfDirty(target);
        }
    }
}
