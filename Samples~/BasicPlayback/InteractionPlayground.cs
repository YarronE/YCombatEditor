using UnityEngine;

namespace Ethan.ActionEditor.Samples
{
    [RequireComponent(typeof(ActionPlayer),typeof(ActionClipAnimator))]
    public sealed class InteractionPlayground : MonoBehaviour
    {
        public SkillConfigSO action;
        public Transform source;
        ActionPlayer player;
        public string LastResult { get; private set; } = "Start playback, then send a query inside a window.";
        void Awake() { player=GetComponent<ActionPlayer>(); player.InteractionDiagnosticsEnabled=true; }
        public bool StartAction()
        {
            bool result=player.TryPlay(new ActionPlayRequest(action),out var error);
            LastResult=result ? "Playing" : error.Message;
            return result;
        }
        public bool SendSignal(string signal)
        {
            bool result=player.TryResolveInteraction(new InteractionQuery(gameObject,source,signal,new Global.Attack { interactionTag="heavy" }),out var resolution);
            LastResult=result ? resolution.Window.id+": "+resolution.Window.response : player.LastInteractionRejection;
            return result;
        }
        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(15,15,540,Screen.height-30),GUI.skin.box);
            GUILayout.Label("ACT Interaction Playground | frame "+player.CurrentFrame);
            if(GUILayout.Button("Play / restart (2 seconds)")) StartAction();
            if(GUILayout.Button("Incoming hit: evade 5-14, parry 15-29")) SendSignal(InteractionQuery.IncomingHit);
            if(GUILayout.Button("Custom training-pulse: 30-50")) SendSignal("training-pulse");
            if(GUILayout.Button("Toggle source near / far")) source.position=transform.position+Vector3.forward*(Vector3.Distance(source.position,transform.position)>3 ? 1 : 6);
            GUILayout.Label(LastResult);
            foreach(var trace in player.LastInteractionEvaluation)
            {
                GUILayout.Label($"[{trace.index}] {trace.id} priority={trace.priority} accepted={trace.accepted} selected={trace.selected} {trace.reason}");
                foreach(var condition in trace.conditions) GUILayout.Label("  "+condition.path+": "+condition.accepted+" "+condition.reason);
            }
            GUILayout.EndArea();
        }
    }
}
