using Ethan.ActionEditor;
using Ethan.ActionEditor.Editor;
using UnityEditor;
using UnityEngine;
public partial class SkillEditorWindow
{
    void DrawToolbar(){
        EditorGUI.DrawRect(new Rect(0,0,position.width,TOOLBAR_HEIGHT),new Color(.16f,.17f,.19f));
        GUILayout.BeginArea(new Rect(10,5,position.width-20,TOOLBAR_HEIGHT-10));
        EditorGUILayout.BeginHorizontal(GUILayout.Height(22));
        GUILayout.Label("ACT",EditorStyles.boldLabel,GUILayout.Width(34));
        if(GUILayout.Button("New",EditorStyles.miniButton,GUILayout.Width(44)))CreateNewConfig();
        using(new EditorGUI.DisabledScope(configFile==null))if(GUILayout.Button("Save",EditorStyles.miniButton,GUILayout.Width(44)))SaveConfig();
        if(GUILayout.Button("Undo",EditorStyles.miniButton,GUILayout.Width(44)))UndoLatest();
        if(GUILayout.Button("Redo",EditorStyles.miniButton,GUILayout.Width(44)))RedoLatest();
        GUILayout.Space(10);EditorGUIUtility.labelWidth=44;EditorGUI.BeginChangeCheck();
        var selected=(SkillConfigSO)EditorGUILayout.ObjectField("Action",configFile,typeof(SkillConfigSO),false,GUILayout.MinWidth(180));
        if(EditorGUI.EndChangeCheck()){StopPreviewSession();CommitDeferredChanges();configFile=selected;if(configFile!=null)LoadConfig();}
        using(new EditorGUI.DisabledScope(configFile==null)){
            if(GUILayout.Button("Action properties",EditorStyles.miniButton,GUILayout.Width(110))){selType=SelType.Basic;selIdx=-1;}
            if(GUILayout.Button("Tools",EditorStyles.miniButton,GUILayout.Width(48)))ShowActionTools();
        }
        if(GUILayout.Button("Help",EditorStyles.miniButton,GUILayout.Width(42)))ActionEditorHelp.OpenUserGuide();
        EditorGUILayout.EndHorizontal();EditorGUILayout.BeginHorizontal(GUILayout.Height(22));
        EditorGUIUtility.labelWidth=82;EditorGUI.BeginChangeCheck();
        previewModel=(GameObject)EditorGUILayout.ObjectField("Preview actor",previewModel,typeof(GameObject),true,GUILayout.MinWidth(220));
        if(EditorGUI.EndChangeCheck()){StopPreviewSession();EnsurePreviewInstance();if(configFile!=null)ActionEditorUserSettings.instance.SetPreviewModel(configFile,previewModel);}
        GUILayout.Space(12);EditorGUIUtility.labelWidth=76;EditorGUI.BeginChangeCheck();
        animSourceModel=(GameObject)EditorGUILayout.ObjectField("Clip source",animSourceModel,typeof(GameObject),false,GUILayout.MinWidth(200));
        if(EditorGUI.EndChangeCheck())LoadClipsFromAsset();
        GUILayout.Label("Shared authoring for players and enemies",EditorStyles.miniLabel,GUILayout.Width(230));
        EditorGUILayout.EndHorizontal();EditorGUILayout.BeginHorizontal(GUILayout.Height(22));
        using(new EditorGUI.DisabledScope(configFile==null||!IsPreviewAllowed))
            if(GUILayout.Button(playFrame?"Stop":"Play",EditorStyles.miniButton,GUILayout.Width(48))){if(playFrame)StopPreviewSession();else{playTimer=0;playFrame=true;}}
        if(GUILayout.Button("Previous",EditorStyles.miniButton,GUILayout.Width(62)))frameSelectIndex=Mathf.Max(0,frameSelectIndex-1);
        if(GUILayout.Button("Next",EditorStyles.miniButton,GUILayout.Width(44)))frameSelectIndex=Mathf.Min(GetTotalFrames(),frameSelectIndex+1);
        EditorGUIUtility.labelWidth=40;
        frameSelectIndex=Mathf.Clamp(EditorGUILayout.IntField("Frame",frameSelectIndex,GUILayout.Width(92)),0,GetTotalFrames());
        float fps=configFile!=null?ActionTiming.FrameRate(configFile):30;
        GUILayout.Label($"/ {GetTotalFrames()}   {frameSelectIndex/fps:F2}s   {fps:0.##} FPS",EditorStyles.miniLabel,GUILayout.Width(165));
        GUILayout.FlexibleSpace();
        if(configFile!=null)GUILayout.Label(EditorUtility.IsDirty(configFile)?"Unsaved":"Saved",EditorStyles.miniLabel,GUILayout.Width(52));
        if(GUILayout.Button($"Issues {_validationErrorCount+_validationWarningCount}",EditorStyles.miniButton,GUILayout.Width(64)))showValidation=!showValidation;
        GUILayout.Label("Zoom",EditorStyles.miniLabel,GUILayout.Width(32));
        pixelsPerFrame=GUILayout.HorizontalSlider(pixelsPerFrame,MIN_PPF,MAX_PPF,GUILayout.Width(65));
        if(GUILayout.Button("Fit",EditorStyles.miniButton,GUILayout.Width(34)))FitTimeline();
        EditorGUILayout.EndHorizontal();GUILayout.EndArea();EditorGUIUtility.labelWidth=0;
    }
}
