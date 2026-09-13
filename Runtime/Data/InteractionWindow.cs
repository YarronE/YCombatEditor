using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ethan.ActionEditor
{
    public enum InteractionResponse { Custom, Evade, Block, Parry }

    /// <summary>Inclusive frame range. Empty lists preserve the consumer's legacy policy.</summary>
    [Serializable]
    public sealed class InteractionWindow
    {
        [Tooltip("Stable window ID; can also identify a host response.")]
        public string id = "interaction";
        public int keyNumber;
        public int endKeyNumber = 5;
        [Tooltip("Only matching signals are evaluated. Use incoming-hit for defensive hit queries.")]
        public string signal = InteractionQuery.IncomingHit;
        [Tooltip("Highest priority wins; equal priorities use list order.")]
        public int priority;
        [Tooltip("Full source cone around actor forward; 360 accepts all directions.")]
        public float sourceAngle = 360;
        public InteractionResponse response = InteractionResponse.Custom;
        [Tooltip("Incoming damage multiplier. Zero is immune; 0.2 takes twenty percent.")]
        public float damageMultiplier;
        public bool suppressHitReaction = true;
        [Tooltip("Maximum successful activations per action. Zero is unlimited.")]
        public int maxActivations;
        [Tooltip("All conditions must pass. Reuse condition assets or extend InteractionCondition.")]
        public List<InteractionCondition> conditions = new List<InteractionCondition>();
    }

    public readonly struct InteractionQuery
    {
        public const string IncomingHit = "incoming-hit";
        public InteractionQuery(GameObject actor, Transform other, string signal, Global.Attack attack = null)
        { Actor = actor; Other = other; Signal = signal; Attack = attack; }
        public GameObject Actor { get; }
        public Transform Other { get; }
        public string Signal { get; }
        public Global.Attack Attack { get; }
    }

    public readonly struct InteractionResolution
    {
        public InteractionResolution(InteractionWindow window, int index, int frame, bool firstActivation)
        { Window = window; Index = index; Frame = frame; FirstActivation = firstActivation; }
        public InteractionWindow Window { get; }
        public int Index { get; }
        public int Frame { get; }
        public bool FirstActivation { get; }
    }
}
