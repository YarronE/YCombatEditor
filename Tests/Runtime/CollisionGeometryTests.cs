using Ethan.ActionEditor;
using NUnit.Framework;
using UnityEngine;

namespace Ethan.ActionEditor.Tests
{
    public sealed class CollisionGeometryTests
    {
        [Test]
        public void GeometryUsesFullHeightActorOffsetAndLocalBoxRotation()
        {
            var actor = new GameObject("Actor"); var target = new GameObject("Contact");
            try {
                actor.transform.SetPositionAndRotation(new Vector3(10,0,0), Quaternion.Euler(0,90,0));
                actor.transform.localScale = Vector3.one * 2;
                var c = new Global.Attack { shapeType = 1, parameter1 = 4, parameter2 = 1, boxHeight = 6,
                    offset = Vector3.forward, collisionRotation = new Vector3(0,90,0) };
                var center = ActionCollisionGeometry.Center(c, actor.transform, 0);
                Assert.That(Vector3.Distance(center, new Vector3(12,0,0)), Is.LessThan(.001f));
                Assert.That(ActionCollisionGeometry.BoxSize(c), Is.EqualTo(new Vector3(4,6,1)));
                Assert.That(Quaternion.Angle(ActionCollisionGeometry.Rotation(c,actor.transform),Quaternion.Euler(0,180,0)), Is.LessThan(.01f));
                var collider = target.AddComponent<BoxCollider>(); target.transform.position = center + Vector3.up * 2.5f;
                Physics.SyncTransforms();
                Assert.That(Physics.OverlapBox(center,ActionCollisionGeometry.BoxSize(c)*.5f,ActionCollisionGeometry.Rotation(c,actor.transform)), Does.Contain(collider));
            } finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(target); }
        }

        [Test]
        public void OldDataDefaultsToDamageAndTwoUnitBoxHeight()
        {
            var c = JsonUtility.FromJson<Global.Attack>("{\"keyNumber\":2,\"parameter1\":3,\"parameter2\":4}");
            Assert.That(c.responseMode, Is.EqualTo(Global.CollisionResponseMode.Damage));
            Assert.That(c.boxHeight, Is.EqualTo(2f));
            Assert.That(c.collisionRotation, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void SignalContactDoesNotRequireADamageableTarget()
        {
            var actor = new GameObject("Actor"); var target = new GameObject("Switch");
            try {
                var receiver = actor.AddComponent<CollisionTestReceiver>();
                var c = new Global.Attack { responseMode = Global.CollisionResponseMode.Signal, contactSignal = "Activate" };
                var contact = new ActionCollisionContact(actor,null,c,target.AddComponent<BoxCollider>(),Vector3.one,4);
                Assert.That(ActionCollisionContact.Notify(contact), Is.EqualTo(1));
                Assert.That(receiver.Signal, Is.EqualTo("Activate"));
                receiver.enabled = false;
                Assert.That(ActionCollisionContact.Notify(contact), Is.EqualTo(0));
            } finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(target); }
        }
    }

    public sealed class CollisionTestReceiver : MonoBehaviour, IActionCollisionReceiver
    {
        public string Signal;
        public void OnCollision(in ActionCollisionContact contact) { Signal = contact.Collision.contactSignal; }
    }
}
