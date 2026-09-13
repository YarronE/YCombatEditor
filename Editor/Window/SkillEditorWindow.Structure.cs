using System.Collections.Generic;
using Ethan.ActionEditor;
using Ethan.ActionEditor.Editor;
using UnityEditor;
using UnityEngine;
public partial class SkillEditorWindow
{
    readonly Dictionary<int,int> motionLanes=new Dictionary<int,int>();
    readonly Dictionary<int,int> facingLanes=new Dictionary<int,int>();
    bool showAdvanced,showValidation;
    static string SelectionTitle(string list) => list switch {
        "animSegments"=>"Animation", "fxList"=>"Effect", "cameraCues"=>"Camera", "moveSegmentList"=>"Motion / Displacement",
        "adjustMotionList"=>"Motion / Facing", "attackList"=>"Attack", "jumpList"=>"Transition", "interactionWindows"=>"Interaction",
        "warningCueList"=>"Telegraph", "cancelList"=>"Cancel", "hitFxList"=>"Hit Effect", "trailToggleList"=>"Trail", _=>ObjectNames.NicifyVariableName(list) };
    static string FieldLabel(string name,string fallback) => name switch {
        "keyNumber"=>"Start frame", "endKeyNumber"=>"End frame", "beginKey"=>"Start frame", "endKey"=>"End frame (exclusive)",
        "skillName"=>"Action name", "skillDescription"=>"Description", "ownerType"=>"Actor profile", "exitFrame"=>"Exit frame (0 = auto)",
        "skillCD"=>"Cooldown", "particleSystem"=>"Visual effect", "audioClip"=>"Audio clip", "followCharacter"=>"Follow actor",
        "allowTurning"=>"Allow target tracking", "targetKind"=>"Tracking target", "rotationSpeed"=>"Turn speed (deg/s)",
        "maxDistance"=>"Distance limit", "space"=>"Direction", "isReWrite"=>"Override hit shape", "parameter1"=>"Radius / length", "parameter2"=>"Width", _=>fallback };
    static bool IsEssentialField(string list,string field){
        string fields=list switch {
            "attackList" or "phase2AttackList"=>"keyNumber endKeyNumber damageMode tickInterval damageRatio impactLevel isReWrite shapeType parameter1 parameter2 offset unblockable unparryable interactionTag",
            "fxList" or "phase2FxList"=>"keyNumber particleSystem audioClip offset rotation scale followCharacter baseVolume customLifetime",
            "moveSegmentList"=>"keyNumber endKeyNumber driveMode rootMotionScale space target resolveDirectionPerFrame curve maxDistance alignFacing",
            "animSegments"=>"clip startFrame clipStartFrame clipEndFrame blendInFrames",
            "jumpList"=>"beginKey endKey nextSkill triggerCommandId triggerKey autoTrigger requiredEnergy energyCost fadeDuration",
            "projectileList"=>"keyNumber endKeyNumber prefab firePoint spawnOffset speed lifetime damageRatio autoAim shotCount spreadAngle",
            _=>null };
        return fields==null || (" "+fields+" ").Contains(" "+field+" ");
    }
    int LaneCount(Dictionary<int,int> lanes){int count=1;foreach(var lane in lanes.Values)count=Mathf.Max(count,lane+1);return count;}
    float MotionTrackHeight(){
        AssignLanes(motionLanes,moveSegmentList,m=>m.keyNumber,m=>SegEnd(m.keyNumber,m.endKeyNumber));
        AssignLanes(facingLanes,adjustMotionList,m=>m.keyNumber,m=>SegEnd(m.keyNumber,m.endKeyNumber));
        return 36+TRACK_HEIGHT*(LaneCount(motionLanes)+LaneCount(facingLanes));
    }
    void DrawUnifiedMotionTrack(Rect rect){
        float y=rect.y;
        GUI.Label(new Rect(rect.x+6,y+1,160,16),"Displacement",EditorStyles.miniLabel);y+=18;
        int movementCount=LaneCount(motionLanes),turnCount=LaneCount(facingLanes);
        GetDef(Global.TrackType.Move).drawLanes(new Rect(rect.x,y,rect.width,TRACK_HEIGHT*movementCount),motionLanes,movementCount);
        y+=TRACK_HEIGHT*movementCount;
        GUI.Label(new Rect(rect.x+6,y+1,260,16),"Facing / target tracking",EditorStyles.miniLabel);y+=18;
        GetDef(Global.TrackType.AdjustMotion).drawLanes(new Rect(rect.x,y,rect.width,TRACK_HEIGHT*turnCount),facingLanes,turnCount);
    }
    void ShowMotionAddMenu(){
        var menu=new GenericMenu();
        menu.AddItem(new GUIContent("Displacement"),false,AddMoveSegmentHere);
        menu.AddItem(new GUIContent("Allow target tracking"),false,()=>AddFacingWindow(true));
        menu.AddItem(new GUIContent("Lock facing / planted feet"),false,()=>AddFacingWindow(false));menu.ShowAsContext();
    }
    void AddFacingWindow(bool allowed){PushUndo();adjustMotionList.Add(new Global.AdjustMotionSegment {
        keyNumber=frameSelectIndex,endKeyNumber=frameSelectIndex+10,allowTurning=allowed,
        targetKind=ownerType==Global.SkillOwnerType.Enemy?Global.MoveTargetKind.Player:Global.MoveTargetKind.LockedTarget });
        selType=SelType.AdjustMotion;selIdx=adjustMotionList.Count-1;
    }
    void AddCameraHere(){PushUndo();cameraCues.Add(new ActionCameraCue {keyNumber=frameSelectIndex,endKeyNumber=frameSelectIndex+15});selType=SelType.Camera;selIdx=cameraCues.Count-1;}
    void ShowActionTools(){
        var menu=new GenericMenu();
        menu.AddItem(new GUIContent("Show advanced properties"),showAdvanced,()=>showAdvanced=!showAdvanced);
        menu.AddItem(new GUIContent("Default hit shape"),false,()=>{selType=SelType.Hitbox;selIdx=-1;});
        menu.AddItem(new GUIContent("Actor settings"),false,()=>{selType=SelType.Enemy;selIdx=-1;});
        menu.AddSeparator("");menu.AddItem(new GUIContent("Restore backup"),false,RestoreCurrentConfig);
        if(configFile!=null&&ActionConfigMigrationService.NeedsMigration(configFile))menu.AddItem(new GUIContent("Upgrade schema with backup"),false,MigrateCurrentConfig);
        if(animSegments.Count>=2)menu.AddItem(new GUIContent("Arrange animation clips"),false,ShowOptimizeMenu);
        menu.ShowAsContext();
    }
}
