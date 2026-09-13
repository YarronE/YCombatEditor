using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ethan.ActionEditor.Editor
{
    public static partial class ActionEditorAgent
    {
        [Serializable] public sealed class Field
        {
            public string path, kind, text, referenceGuid;
            public long integer, referenceLocalId;
            public double number;
            public bool boolean;
            public Vector4 vector;
            public AnimationCurve curve;
        }
        [Serializable] public sealed class Edit
        {
            public string operation = "set";
            public string path;
            public int index, destinationIndex;
            public Field value;
        }
        [Serializable] public sealed class Change { public string path, before, after; }
        [Serializable] sealed class FieldSet { public List<Field> fields; }

        // Only fields declared by the public config can be addressed. Unity internals and timing migration are excluded.
        static bool Allowed(string path, bool writing = false)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string root = path.Split('.')[0];
            return typeof(SkillConfigSO).GetField(root) != null &&
                (!writing || (root != "timingVersion" && root != "timelineFrameRate"));
        }

        static List<Field> ReadFields(SkillConfigSO config)
        {
            var fields = new List<Field>();
            using (var serialized = new SerializedObject(config))
            {
                var p = serialized.GetIterator();
                bool descend = true;
                while (p.Next(descend))
                {
                    descend = p.propertyType == SerializedPropertyType.Generic;
                    if (!Allowed(p.propertyPath)) { descend = false; continue; }
                    if (p.propertyType == SerializedPropertyType.Generic) continue;
                    var f = new Field { path = p.propertyPath, kind = p.propertyType.ToString() };
                    switch (p.propertyType)
                    {
                        case SerializedPropertyType.Integer: case SerializedPropertyType.ArraySize: f.integer = p.longValue; break;
                        case SerializedPropertyType.Enum: f.integer = p.intValue; f.text = p.enumValueIndex >= 0 ? p.enumNames[p.enumValueIndex] : null; break;
                        case SerializedPropertyType.Float: f.number = p.doubleValue; break;
                        case SerializedPropertyType.Boolean: f.boolean = p.boolValue; break;
                        case SerializedPropertyType.String: f.text = p.stringValue; break;
                        case SerializedPropertyType.Vector2: f.vector = p.vector2Value; break;
                        case SerializedPropertyType.Vector3: f.vector = p.vector3Value; break;
                        case SerializedPropertyType.Vector4: f.vector = p.vector4Value; break;
                        case SerializedPropertyType.Quaternion: var q = p.quaternionValue; f.vector = new Vector4(q.x,q.y,q.z,q.w); break;
                        case SerializedPropertyType.Color: f.vector = p.colorValue; break;
                        case SerializedPropertyType.AnimationCurve: f.curve = p.animationCurveValue; break;
                        case SerializedPropertyType.ObjectReference:
                            if (p.objectReferenceValue != null && !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(p.objectReferenceValue, out f.referenceGuid, out f.referenceLocalId))
                                throw new ArgumentException("Only persistent asset references are supported: " + p.propertyPath);
                            break;
                        default: f.text = p.type; break;
                    }
                    fields.Add(f);
                }
            }
            return fields;
        }
        static string Digest(List<Field> fields)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new FieldSet { fields = fields })))).Replace("-", "").ToLowerInvariant();
        }

        static void EditAction(Request request, Result result)
        {
            if (request.assetPaths == null || request.assetPaths.Length != 1) throw new ArgumentException("Provide exactly one source assetPath.");
            string path = request.assetPaths[0];
            if (!ActionEditorSettings.TryNormalizeAssetDirectory(path, out var normalized) || normalized != path || !path.EndsWith(".asset", StringComparison.Ordinal))
                throw new ArgumentException("Source must be a canonical .asset path below Assets.");
            var source = AssetDatabase.LoadAssetAtPath<SkillConfigSO>(path);
            if (source == null) throw new ArgumentException("Action asset not found.");
            result.fields = ReadFields(source);
            result.contentDigest = Digest(result.fields);
            if (request.operation == "read_action") { AddIssues(result,path,ActionConfigValidator.Validate(source)); return; }
            if (request.expectedDigest != result.contentDigest) throw new InvalidOperationException("Stale or missing expectedDigest; read the action again.");
            if (request.operation == "clone_action")
            {
                if (!ActionEditorSettings.TryNormalizeAssetDirectory(request.outputAssetPath, out var destination) || !destination.EndsWith(".asset", StringComparison.Ordinal))
                    throw new ArgumentException("Destination must be a new .asset path below Assets.");
                if (File.Exists(destination) || File.Exists(destination + ".meta") || Directory.Exists(destination) || AssetDatabase.LoadMainAssetAtPath(destination) != null)
                    throw new IOException("Destination exists.");
                normalized = destination;
            }
            var draft = Object.Instantiate(source);
            draft.name = source.name;
            try
            {
                foreach (var edit in request.edits ?? Array.Empty<Edit>()) ApplyEdit(draft, edit);
                var after = ReadFields(draft);
                var beforeByPath = result.fields.ToDictionary(f => f.path, f => JsonUtility.ToJson(f));
                var afterByPath = after.ToDictionary(f => f.path, f => JsonUtility.ToJson(f));
                foreach (var key in beforeByPath.Keys.Union(afterByPath.Keys).OrderBy(k => k, StringComparer.Ordinal))
                {
                    beforeByPath.TryGetValue(key, out var beforeValue); afterByPath.TryGetValue(key, out var afterValue);
                    if (beforeValue != afterValue) result.changes.Add(new Change { path = key, before = beforeValue, after = afterValue });
                }
                AddIssues(result, path, ActionConfigValidator.Validate(draft));
                if (result.issues.Any(i => i.severity == "Error")) return;
                result.fields = after; result.contentDigest = Digest(after);
                if (request.dryRun) return;
                Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Agent Edit Action");
                var recovery = Object.Instantiate(source); recovery.name = source.name;
                try
                {
                    if (request.operation == "clone_action")
                    {
                        ActionConfigMigrationService.EnsureAssetDirectory(Path.GetDirectoryName(normalized).Replace('\\','/'));
                        AssetDatabase.CreateAsset(draft, normalized);
                        Undo.RegisterCreatedObjectUndo(draft, "Agent Clone Action");
                        AssetDatabase.SaveAssetIfDirty(draft);
                        result.createdAssetPath = normalized;
                        draft = null;
                    }
                    else
                    {
                        Undo.RegisterCompleteObjectUndo(source, "Agent Edit Action");
                        EditorUtility.CopySerialized(draft, source);
                        EditorUtility.SetDirty(source); AssetDatabase.SaveAssetIfDirty(source);
                    }
                    Undo.CollapseUndoOperations(group);
                }
                catch
                {
                    Undo.RevertAllDownToGroup(group);
                    if (request.operation == "edit_action") { EditorUtility.CopySerialized(recovery, source); EditorUtility.SetDirty(source); AssetDatabase.SaveAssetIfDirty(source); }
                    else if (AssetDatabase.Contains(draft)) AssetDatabase.DeleteAsset(normalized);
                    throw;
                }
                finally { Object.DestroyImmediate(recovery); }
            }
            finally { if (draft != null && !AssetDatabase.Contains(draft)) Object.DestroyImmediate(draft); }
        }

        static void ApplyEdit(SkillConfigSO draft, Edit edit)
        {
            if (edit == null || !Allowed(edit.path, true)) throw new ArgumentException("Unsupported or protected field path.");
            if (edit.operation != "set")
            {
                if (edit.path.Contains(".")) throw new ArgumentException("Structural edits require a root track list.");
                var field = typeof(SkillConfigSO).GetField(edit.path);
                if (!(field.GetValue(draft) is IList list) || !field.FieldType.IsGenericType) throw new ArgumentException("Not a track list.");
                if (edit.index < 0 || edit.index > list.Count) throw new ArgumentOutOfRangeException("index");
                if (edit.operation == "insert") list.Insert(edit.index, Activator.CreateInstance(field.FieldType.GetGenericArguments()[0]));
                else if (edit.operation == "remove" && edit.index < list.Count) list.RemoveAt(edit.index);
                else if (edit.operation == "move" && edit.index < list.Count && edit.destinationIndex >= 0 && edit.destinationIndex < list.Count)
                { var item = list[edit.index]; list.RemoveAt(edit.index); list.Insert(edit.destinationIndex,item); }
                else throw new ArgumentException("Invalid structural edit.");
                return;
            }
            using (var serialized = new SerializedObject(draft))
            {
                var p = serialized.FindProperty(edit.path);
                var v = edit.value;
                if (p == null || v == null || p.propertyType.ToString() != v.kind) throw new ArgumentException("Field is missing or value kind mismatches.");
                if (double.IsNaN(v.number) || double.IsInfinity(v.number)) throw new ArgumentException("Numbers must be finite.");
                for(int component=0;component<4;component++)
                    if(float.IsNaN(v.vector[component]) || float.IsInfinity(v.vector[component])) throw new ArgumentException("Vector components must be finite.");
                switch (p.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        if(p.type == "int" && (v.integer < int.MinValue || v.integer > int.MaxValue)) throw new ArgumentException("Integer is outside the field range.");
                        p.longValue = v.integer; break;
                    case SerializedPropertyType.Enum:
                        if (!p.enumNames.Contains(v.text)) throw new ArgumentException("Set enum using its exact name in value.text.");
                        p.enumValueIndex = Array.IndexOf(p.enumNames,v.text); break;
                    case SerializedPropertyType.Float: p.doubleValue = v.number; break;
                    case SerializedPropertyType.Boolean: p.boolValue = v.boolean; break;
                    case SerializedPropertyType.String: p.stringValue = v.text ?? ""; break;
                    case SerializedPropertyType.Vector2: p.vector2Value = v.vector; break;
                    case SerializedPropertyType.Vector3: p.vector3Value = v.vector; break;
                    case SerializedPropertyType.Vector4: p.vector4Value = v.vector; break;
                    case SerializedPropertyType.Color: p.colorValue = v.vector; break;
                    case SerializedPropertyType.Quaternion: p.quaternionValue = new Quaternion(v.vector.x,v.vector.y,v.vector.z,v.vector.w); break;
                    case SerializedPropertyType.AnimationCurve: p.animationCurveValue = v.curve; break;
                    case SerializedPropertyType.ObjectReference:
                        Object reference = null;
                        if (!string.IsNullOrEmpty(v.referenceGuid))
                        {
                            reference = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(v.referenceGuid)).FirstOrDefault(o =>
                                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o,out string guid,out long local) && guid == v.referenceGuid && local == v.referenceLocalId);
                            if (reference == null) throw new ArgumentException("Asset GUID/local file ID does not resolve.");
                            Type expected=typeof(SkillConfigSO);
                            foreach(string part in edit.path.Split('.'))
                            {
                                if(part=="Array") continue;
                                if(part.StartsWith("data[",StringComparison.Ordinal)) expected=expected.IsArray ? expected.GetElementType() : expected.GetGenericArguments()[0];
                                else expected=expected.GetField(part)?.FieldType ?? throw new ArgumentException("Cannot resolve reference field type.");
                            }
                            if(!expected.IsInstanceOfType(reference)) throw new ArgumentException("Reference type does not match the field.");
                        }
                        p.objectReferenceValue = reference;
                        if (p.objectReferenceValue != reference) throw new ArgumentException("Reference type does not match the field.");
                        break;
                    default: throw new ArgumentException("Use a supported leaf field or a structural track edit.");
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
