using System.Collections.Generic;
using Ethan.ActionEditor;
using UnityEditor;
using UnityEngine;

public partial class SkillEditorWindow
{
    List<ActionFlowNode> flowNodes = new List<ActionFlowNode>();
    struct FlowItem { public SelType selection; public int index, start, end, dragEnd, lane; public string label; }
    readonly List<FlowItem> flowItems = new List<FlowItem>();
    bool flowExpanded = true;
    int FlowCount => flowNodes.Count + jumpList.Count + cancelList.Count + (configFile != null && configFile.exitFrame > 0 ? 1 : 0);

    static string FlowKindLabel(ActionFlowKind kind) => kind == ActionFlowKind.AllowExit ? "Allow Exit" : kind == ActionFlowKind.Branch ? "Branch" : "Complete";
    void ShowFlowAddMenu()
    {
        var menu = new GenericMenu();
        foreach (ActionFlowKind kind in System.Enum.GetValues(typeof(ActionFlowKind)))
        { var captured = kind; menu.AddItem(new GUIContent(FlowKindLabel(kind)), false, () => AddFlowNode(captured)); }
        menu.ShowAsContext();
    }
    void AddFlowNode(ActionFlowKind kind)
    {
        PushUndo();
        flowNodes.Add(new ActionFlowNode { kind = kind, keyNumber = frameSelectIndex, endKeyNumber = frameSelectIndex + 10 });
        bool hasTrack = trackList.Exists(t => Ethan.ActionEditor.Editor.BuiltinTrackRegistry.Canonical(t.type) == Global.TrackType.Flow);
        if (!hasTrack) trackList.Add(new Global.SkillTrack { type = Global.TrackType.Flow, expanded = true });
        selType = SelType.Flow; selIdx = flowNodes.Count - 1;
    }
    float FlowTrackHeight()
    {
        flowItems.Clear();
        int end = configFile != null ? configFile.RuntimeEndFrame : 0;
        for (int i = 0; i < flowNodes.Count; i++) {
            var n = flowNodes[i]; if (n == null) continue;
            int last = n.kind == ActionFlowKind.Complete ? n.keyNumber : n.limitWindow ? n.endKeyNumber : Mathf.Max(n.keyNumber, end);
            flowItems.Add(new FlowItem { selection = SelType.Flow, index = i, start = n.keyNumber, end = last,
                dragEnd = n.limitWindow && n.kind != ActionFlowKind.Complete ? n.endKeyNumber : n.keyNumber,
                label = FlowKindLabel(n.kind) + (n.kind == ActionFlowKind.Branch ? " / " + (n.nextAction != null ? n.nextAction.name : "Choose action") :
                    n.kind == ActionFlowKind.AllowExit && !string.IsNullOrEmpty(n.category) ? " / " + n.category : "") });
        }
        for (int i = 0; i < jumpList.Count; i++) { var n = jumpList[i]; if(n == null) continue;
            flowItems.Add(new FlowItem { selection = SelType.Jump, index = i, start = n.beginKey, end = n.endKey - 1, dragEnd = n.endKey,
                label = "Branch / " + (n.nextSkill != null ? n.nextSkill.name : "Choose action") }); }
        for (int i = 0; i < cancelList.Count; i++) { var n = cancelList[i]; if(n == null) continue;
            flowItems.Add(new FlowItem { selection = SelType.Cancel, index = i, start = n.keyNumber,
                end = n.endKeyNumber > n.keyNumber ? n.endKeyNumber : Mathf.Max(n.keyNumber, end), dragEnd = n.endKeyNumber > n.keyNumber ? n.endKeyNumber : n.keyNumber,
                label = "Allow Exit / At least " + n.minCancelPriority }); }
        if (configFile != null && configFile.exitFrame > 0) flowItems.Add(new FlowItem { selection = SelType.FlowEnd, index = 0,
            start = configFile.exitFrame, end = configFile.exitFrame, dragEnd = configFile.exitFrame, label = "Complete / Exit frame" });
        flowItems.Sort((a,b)=> { int time=a.start.CompareTo(b.start); if(time!=0) return time; int type=a.selection.CompareTo(b.selection); return type!=0?type:a.index.CompareTo(b.index); });
        var ends = new List<int>();
        for (int i = 0; i < flowItems.Count; i++) {
            var item = flowItems[i]; int lane = 0;
            while (lane < ends.Count && ends[lane] >= item.start) lane++;
            int labelEnd = item.start + Mathf.CeilToInt(190 / Mathf.Max(1, pixelsPerFrame));
            if(lane == ends.Count) ends.Add(0);
            ends[lane] = Mathf.Max(item.end, labelEnd); item.lane = lane; flowItems[i] = item;
        }
        return Mathf.Max(1, ends.Count) * TRACK_HEIGHT;
    }
    void DrawFlowTrack(Rect rect)
    {
        FlowTrackHeight();
        foreach (var item in flowItems) {
            float x = rect.x + item.start * pixelsPerFrame, y = rect.y + item.lane * TRACK_HEIGHT + 3;
            float end = rect.x + (item.end + 1) * pixelsPerFrame;
            bool selected = selType == item.selection && selIdx == item.index;
            var color = selected ? new Color(.85f,.88f,1) : new Color(.60f,.68f,.90f);
            EditorGUI.DrawRect(new Rect(x,y,4,TRACK_HEIGHT-6),color);
            if(item.end > item.start) EditorGUI.DrawRect(new Rect(x+4,y+TRACK_HEIGHT-10,Mathf.Max(0,end-x-4),2),new Color(color.r,color.g,color.b,.45f));
            var hit = new Rect(x,y,Mathf.Max(170,Mathf.Min(350,end-x)),TRACK_HEIGHT-6);
            GUI.Label(new Rect(x+7,y,Mathf.Max(163,hit.width-7),18),item.label,EditorStyles.miniLabel);
            if(selected) DrawOutline(hit,color);
            var ev = Event.current;
            if(ev.type == EventType.MouseDown && ev.button == 0 && hit.Contains(ev.mousePosition)) {
                selType=item.selection; selIdx=item.index; frameSelectIndex=item.start;
                StartDrag(item.selection,item.index,item.start,item.dragEnd); ev.Use();
            }
            if(ev.type == EventType.ContextClick && hit.Contains(ev.mousePosition)) {
                selType=item.selection; selIdx=item.index;
                var menu=new GenericMenu();
                if(item.selection != SelType.FlowEnd) menu.AddItem(new GUIContent("Copy"),false,CopySelected);
                menu.AddItem(new GUIContent("Remove"),false,DelSel); menu.ShowAsContext(); ev.Use();
            }
        }
    }
    void DrawFlowInspector(int index)
    {
        if(_serializedConfig == null || index < 0 || index >= flowNodes.Count) return;
        _serializedConfig.Update();
        var list=_serializedConfig.FindProperty("flowNodes"); var item=list.GetArrayElementAtIndex(index);
        var kind=(ActionFlowKind)item.FindPropertyRelative("kind").enumValueIndex;
        EditorGUILayout.LabelField("Action Flow / " + FlowKindLabel(kind),EditorStyles.boldLabel);
        if(GUILayout.Button("Remove node")) { list.DeleteArrayElementAtIndex(index); ApplyInspectorProperties(); selType=SelType.None; selIdx=-1; return; }
        EditorGUILayout.PropertyField(item.FindPropertyRelative("keyNumber"),new GUIContent("Frame"));
        if(kind != ActionFlowKind.Complete) {
            EditorGUILayout.PropertyField(item.FindPropertyRelative("limitWindow"),new GUIContent("Limit window"));
            if(item.FindPropertyRelative("limitWindow").boolValue)
                EditorGUILayout.PropertyField(item.FindPropertyRelative("endKeyNumber"),new GUIContent("Until frame (inclusive)"));
            else EditorGUILayout.LabelField("Active from this frame until the action ends.",EditorStyles.wordWrappedMiniLabel);
        }
        if(kind == ActionFlowKind.AllowExit) {
            var category=item.FindPropertyRelative("category");
            string[] categories={"", "Attack", "Shoot", "Special", "Dodge", "Jump", "Movement"};
            int selected=System.Array.IndexOf(categories,category.stringValue); if(selected<0) selected=7;
            EditorGUI.BeginChangeCheck(); int chosen=EditorGUILayout.Popup("Allowed category",selected,new[]{"Any","Attack","Shoot","Special","Dodge","Jump","Movement","Custom"});
            if(EditorGUI.EndChangeCheck()) category.stringValue=chosen<7?categories[chosen]:"Custom";
            if(chosen==7) EditorGUILayout.PropertyField(category,new GUIContent("Category ID"));
            EditorGUILayout.PropertyField(item.FindPropertyRelative("priorityRule"),new GUIContent("Priority rule"));
            if(item.FindPropertyRelative("priorityRule").enumValueIndex == (int)ActionFlowPriority.AtLeast)
                EditorGUILayout.PropertyField(item.FindPropertyRelative("minimumPriority"));
            EditorGUILayout.HelpBox("Allows an eligible action request. Without a request, the current action continues.",MessageType.None);
        } else if(kind == ActionFlowKind.Branch) {
            EditorGUILayout.PropertyField(item.FindPropertyRelative("nextAction"),new GUIContent("Next action"));
            EditorGUILayout.PropertyField(item.FindPropertyRelative("automatic"),new GUIContent("Automatic"));
            if(!item.FindPropertyRelative("automatic").boolValue) EditorGUILayout.PropertyField(item.FindPropertyRelative("command"));
        } else EditorGUILayout.HelpBox("Completes the skill before this frame's gameplay events. Input or AI can select the next action. Animation transitions use the host's blending policy.",MessageType.None);
        if(kind != ActionFlowKind.Complete) {
            showAdvanced=EditorGUILayout.ToggleLeft("Show conditions",showAdvanced);
            if(showAdvanced) EditorGUILayout.PropertyField(item.FindPropertyRelative("conditions"),true);
        }
        ApplyInspectorProperties();
    }
    void DrawLegacyFlowInspector(string listName,int index)
    {
        _serializedConfig.Update(); var item=_serializedConfig.FindProperty(listName).GetArrayElementAtIndex(index);
        bool branch=listName=="jumpList";
        EditorGUILayout.LabelField("Action Flow / " + (branch?"Branch":"Allow Exit"),EditorStyles.boldLabel);
        if(branch) CollisionFields(item,"beginKey","endKey","nextSkill","autoTrigger","triggerCommandId");
        else CollisionFields(item,"keyNumber","endKeyNumber","minCancelPriority");
        showAdvanced=EditorGUILayout.ToggleLeft("Show advanced properties",showAdvanced);
        if(showAdvanced) {
            if(branch) CollisionFields(item,"triggerKey","requiredEnergy","energyCost","fadeDuration","requestSlowPromptOnJump","derivePromptVfx","derivePromptSound");
            else CollisionFields(item,"fadeDuration");
        }
        EditorGUILayout.HelpBox(branch?"Existing branch: the end frame is exclusive. Project resource and cue settings retain their original behavior.":"Existing exit rule: priority uses an inclusive minimum. End <= start remains open until completion.",MessageType.None);
        ApplyInspectorProperties();
        if(GUILayout.Button("Remove node")) DelSel();
    }
    void DrawLegacyFlowEnd()
    {
        EditorGUILayout.LabelField("Action Flow / Complete",EditorStyles.boldLabel);
        DrawSerializedFields("exitFrame");
        EditorGUILayout.HelpBox("Existing exit frame retains the host's original end-of-frame ordering. New Complete nodes stop before gameplay events.",MessageType.None);
        if(GUILayout.Button("Remove node")) DelSel();
    }
}
