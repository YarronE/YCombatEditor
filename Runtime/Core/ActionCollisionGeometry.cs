using UnityEngine;

namespace Ethan.ActionEditor
{
    /// <summary>Shared authoring/query geometry. Dimensions use world units; offsets use actor-local coordinates.</summary>
    public static class ActionCollisionGeometry
    {
        public static Vector3 BoxSize(Global.Attack collision, float rangeMultiplier = 1f) =>
            new Vector3(collision.parameter1 * rangeMultiplier, collision.boxHeight, collision.parameter2 * rangeMultiplier);

        public static Vector3 Center(Global.Attack collision, Transform actor, int frame) =>
            actor != null ? actor.TransformPoint(collision.GetOffsetAtFrame(frame)) : collision.GetOffsetAtFrame(frame);

        public static Quaternion Rotation(Global.Attack collision, Transform actor) =>
            (actor != null ? actor.rotation : Quaternion.identity) * Quaternion.Euler(collision.collisionRotation);

        public static bool IsActive(Global.Attack collision, int frame) => collision != null &&
            frame >= collision.keyNumber && frame <= Mathf.Max(collision.keyNumber, collision.endKeyNumber);

        public static bool IsFinite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
