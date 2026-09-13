using Ethan.ActionEditor;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

public partial class SkillEditorWindow
{
    void DrawCollisionScene(Transform actor)
    {
        if (Event.current.type == EventType.MouseUp) _editGesture.Complete();
        var selected = SelectedCollision;
        foreach (var collision in atkList)
            if (collision != null && (collision == selected || ActionCollisionGeometry.IsActive(collision, frameSelectIndex)))
                DrawCollisionVolume(collision, actor, collision == selected);
        if (selType == SelType.Phase2Attack && selected != null) DrawCollisionVolume(selected, actor, true);
    }

    void DrawCollisionVolume(Global.Attack collision, Transform actor, bool selected)
    {
        var center = ActionCollisionGeometry.Center(collision, actor, frameSelectIndex);
        var rotation = ActionCollisionGeometry.Rotation(collision, actor);
        var size = collision.shapeType == 0 ? Vector3.one * collision.parameter1 : ActionCollisionGeometry.BoxSize(collision);
        if (!ActionCollisionGeometry.IsFinite(center) || !ActionCollisionGeometry.IsFinite(size) || !ActionCollisionGeometry.IsFinite(collision.collisionRotation)) return;
        size = Vector3.Max(size, Vector3.one * .001f);
        bool active = ActionCollisionGeometry.IsActive(collision, frameSelectIndex);
        Color color = selected ? new Color(.3f, .85f, 1f, 1f) : new Color(1f, .65f, .2f, .6f);
        var previousDepth = Handles.zTest;
        try
        {
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            using (new Handles.DrawingScope(color, Matrix4x4.TRS(center, rotation, Vector3.one)))
            {
                if (collision.shapeType == 0) {
                    Handles.DrawWireDisc(Vector3.zero, Vector3.up, size.x);
                    Handles.DrawWireDisc(Vector3.zero, Vector3.right, size.x);
                    Handles.DrawWireDisc(Vector3.zero, Vector3.forward, size.x);
                } else Handles.DrawWireCube(Vector3.zero, size);
            }
            using (new Handles.DrawingScope(color, Matrix4x4.identity))
            {
                Handles.Label(center + Vector3.up * (collision.shapeType == 0 ? size.x : size.y * .5f),
                    (string.IsNullOrWhiteSpace(collision.collisionName) ? "Collision" : collision.collisionName) +
                    (active ? "" : " (outside window)"));
                if (selected && editCollisionInScene && collisionPage == CollisionPage.Shape && !Ethan.ActionEditor.Editor.ActionConfigMigrationService.NeedsMigration(configFile))
                {
                    EditorGUI.BeginChangeCheck();
                    var newCenter = center; var newRotation = rotation;
                    if (collisionHandleMode == CollisionHandleMode.Move)
                    {
                        if (!collision.useBoneTracking || active) newCenter = Handles.PositionHandle(center, actor != null ? actor.rotation : Quaternion.identity);
                    }
                    else if (collisionHandleMode == CollisionHandleMode.Rotate && collision.shapeType == 1)
                        newRotation = Handles.RotationHandle(rotation, center);
                    else if (collisionHandleMode == CollisionHandleMode.Resize)
                    {
                        using (new Handles.DrawingScope(color, Matrix4x4.TRS(center, rotation, Vector3.one)))
                        {
                            if (collision.shapeType == 0) {
                                sphereHandle.center = Vector3.zero; sphereHandle.radius = size.x;
                                sphereHandle.axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y | PrimitiveBoundsHandle.Axes.Z; sphereHandle.DrawHandle();
                                size = Vector3.one * sphereHandle.radius; newCenter += rotation * sphereHandle.center;
                            } else {
                                boxHandle.center = Vector3.zero; boxHandle.size = size;
                                boxHandle.axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y | PrimitiveBoundsHandle.Axes.Z; boxHandle.DrawHandle();
                                size = boxHandle.size; newCenter += rotation * boxHandle.center;
                            }
                        }
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        var offset = actor != null ? actor.InverseTransformPoint(newCenter) : newCenter;
                        var localRotation = actor != null ? Quaternion.Inverse(actor.rotation) * newRotation : newRotation;
                        ApplyCollisionGeometryEdit(collision, offset, size, localRotation.eulerAngles);
                    }
                    if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F)
                    { FrameSelectedCollision(); Event.current.Use(); }
                }
            }
        }
        finally { Handles.zTest = previousDepth; }
    }

    void ApplyCollisionGeometryEdit(Global.Attack collision, Vector3 offset, Vector3 size, Vector3 rotation)
    {
        if (configFile == null || Ethan.ActionEditor.Editor.ActionConfigMigrationService.NeedsMigration(configFile) || collision == null || !ActionCollisionGeometry.IsFinite(offset) || !ActionCollisionGeometry.IsFinite(size) || !ActionCollisionGeometry.IsFinite(rotation)) return;
        _editGesture.Record(configFile, "Edit Collision Volume");
        collision.isReWrite = true;
        if (collision.useBoneTracking && collision.boneOffsets != null && collision.boneOffsets.Count > 0)
        {
            if (ActionCollisionGeometry.IsActive(collision, frameSelectIndex))
                collision.boneOffsets[Mathf.Clamp(frameSelectIndex - collision.keyNumber, 0, collision.boneOffsets.Count - 1)] = offset;
        }
        else collision.offset = offset;
        collision.parameter1 = Mathf.Max(.001f, size.x);
        if (collision.shapeType == 1) { collision.boxHeight = Mathf.Max(.001f, size.y); collision.parameter2 = Mathf.Max(.001f, size.z); collision.collisionRotation = rotation; }
        Ethan.ActionEditor.Editor.ActionEditorTransaction.MarkChanged(configFile);
        _serializedConfig?.Update(); RefreshValidation(); Repaint(); SceneView.RepaintAll();
    }
}
