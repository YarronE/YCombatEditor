using UnityEngine;

namespace Ethan.ActionEditor
{
    /// <summary>A confirmed contact supplied by the host collision query, not by the timeline alone.</summary>
    public readonly struct ActionCollisionContact
    {
        public readonly GameObject Actor;
        public readonly SkillConfigSO Config;
        public readonly Global.Attack Collision;
        public readonly Collider Other;
        public readonly Vector3 Point;
        public readonly int Frame;
        public ActionCollisionContact(GameObject actor, SkillConfigSO config, Global.Attack collision, Collider other, Vector3 point, int frame)
        { Actor = actor; Config = config; Collision = collision; Other = other; Point = point; Frame = frame; }

        public static int Notify(in ActionCollisionContact contact)
        {
            if (contact.Actor == null || contact.Other == null) return 0;
            int count = 0;
            foreach (var behaviour in contact.Actor.GetComponents<MonoBehaviour>())
                if (behaviour != null && behaviour.isActiveAndEnabled && behaviour is IActionCollisionReceiver receiver)
                { receiver.OnCollision(contact); count++; }
            return count;
        }
    }

    public interface IActionCollisionReceiver
    {
        void OnCollision(in ActionCollisionContact contact);
    }
}
