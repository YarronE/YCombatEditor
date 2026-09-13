using AtkJudge;
using System;
using Ethan.ActionEditor;
using Ethan.ActionEditor.Editor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

// The original script GUID stays on SkillEditorWindow.cs.
public partial class SkillEditorWindow
{
    #region 保存加载
    void CreateNewConfig(){
        string directory=ActionEditorSettings.instance.DefaultAssetDirectory;
        ActionConfigMigrationService.EnsureAssetDirectory(directory);
        string path=EditorUtility.SaveFilePanelInProject("Create action","NewAction","asset","Choose where to save the action asset.",directory);
        if(string.IsNullOrEmpty(path)) return;
        var asset=ScriptableObject.CreateInstance<SkillConfigSO>();
        asset.InitializeExplicitTiming();
        asset.SetActionEditorSchemaVersion(ActionConfigMigrationService.CurrentSchemaVersion);
        AssetDatabase.CreateAsset(asset,path);
        AssetDatabase.SaveAssetIfDirty(asset);
        configFile=asset;
        LoadConfig();
        Selection.activeObject=asset;
    }
    void SelectConfig(SkillConfigSO selected){
        StopPreviewSession();
        _editGesture.Complete();
        CommitDeferredChanges();
        configFile=selected;
        inspScrollPos=Vector2.zero;
        timelineScrollPos=Vector2.zero;
        selType=SelType.None;selIdx=-1;
        isDraggingItem=isDraggingSegment=isDraggingSegEdge=false;
        if(configFile!=null)LoadConfig();
        else { _serializedConfig=null;RefreshValidation(); }
        Repaint();
    }
    void LoadConfig(){
        StopPreviewSession();
        _editGesture.Complete();
        CommitDeferredChanges();
        if(configFile==null) return;
        _serializedConfig=new SerializedObject(configFile);
        RefreshConfigBindings();
        previewModel=ActionEditorUserSettings.instance.GetPreviewModel(configFile);
        if(previewModel==null&&!string.IsNullOrEmpty(configFile.previewModelName)){
            var f=GameObject.Find(configFile.previewModelName);
            if(f!=null){ previewModel=f; previewAnim=previewModel.GetComponent<Animator>(); } }
        selType=SelType.None; selIdx=-1;
        RefreshValidation();
    }

    void RefreshConfigBindings(){
        if(configFile==null) return;
        flowNodes=configFile.flowNodes??new List<ActionFlowNode>();
        cameraCues=configFile.cameraCues??new List<ActionCameraCue>();
        warningCueList=configFile.warningCueList??new List<Global.WarningCue>();
        jumpList=configFile.jumpList??new List<Global.Jump>();
        atkList=configFile.attackList??new List<Global.Attack>();
        fxAndSoundList=configFile.fxList??new List<Global.FxAndSound>();
        cancelList=configFile.cancelList??new List<Global.CancelPoint>();
        projectileList=configFile.projectileList??new List<Global.Projectile>();
        hitFxList=configFile.hitFxList??new List<Global.HitFx>();
        superArmorList=configFile.superArmorList??new List<Global.SuperArmorSegment>();
        adjustMotionList=configFile.adjustMotionList??new List<Global.AdjustMotionSegment>();
        trailToggleList=configFile.trailToggleList??new List<Global.TrailToggle>();
        moveSegmentList=configFile.moveSegmentList??new List<Global.MoveSegment>();
        interactionWindows=configFile.interactionWindows??new List<InteractionWindow>();
        // 多段动画
        animSegments=configFile.animSegments??new List<Global.AnimClipSegment>();
        // 向后兼容：如果 animSegments 为空但 skillClip 有值，自动创建一段
        if(animSegments.Count==0&&configFile.skillClip!=null)
            animSegments=new List<Global.AnimClipSegment>{new Global.AnimClipSegment{clip=configFile.skillClip,startFrame=0}};
        // 动态轨道
        trackList=configFile.tracks??new List<Global.SkillTrack>();
        // 向后兼容：如果轨道列表为空，创建默认轨道
        if(trackList.Count==0){
            trackList=new List<Global.SkillTrack>();
            trackList.Add(new Global.SkillTrack{type=Global.TrackType.Animation,expanded=true});
            trackList.Add(new Global.SkillTrack{type=Global.TrackType.Move,expanded=true});
            trackList.Add(new Global.SkillTrack{type=Global.TrackType.Attack,expanded=true});
            trackList.Add(new Global.SkillTrack{type=Global.TrackType.Fx,expanded=true});
            trackList.Add(new Global.SkillTrack{type=Global.TrackType.Camera,expanded=true});
            trackList.Add(new Global.SkillTrack{type=Global.TrackType.Jump,expanded=true});
            trackList.Add(new Global.SkillTrack{type=Global.TrackType.Interaction,expanded=true});
        }
    }
    void SaveConfig(){
        if(configFile==null) return;
        CommitDeferredChanges();
        ActionEditorTransaction.SaveIfDirty(configFile);
        RefreshValidation();
        Debug.Log($"Action saved: {configFile.name}");
    }

    void BindListsToConfig(){
        if(configFile==null) return;
        if(!ReferenceEquals(configFile.flowNodes,flowNodes)) configFile.flowNodes=flowNodes;
        if(!ReferenceEquals(configFile.cameraCues,cameraCues)) configFile.cameraCues=cameraCues;
        if(!ReferenceEquals(configFile.warningCueList,warningCueList)) configFile.warningCueList=warningCueList;
        if(!ReferenceEquals(configFile.jumpList,jumpList)) configFile.jumpList=jumpList;
        if(!ReferenceEquals(configFile.attackList,atkList)) configFile.attackList=atkList;
        if(!ReferenceEquals(configFile.fxList,fxAndSoundList)) configFile.fxList=fxAndSoundList;
        if(!ReferenceEquals(configFile.cancelList,cancelList)) configFile.cancelList=cancelList;
        if(!ReferenceEquals(configFile.projectileList,projectileList)) configFile.projectileList=projectileList;
        if(!ReferenceEquals(configFile.hitFxList,hitFxList)) configFile.hitFxList=hitFxList;
        if(!ReferenceEquals(configFile.superArmorList,superArmorList)) configFile.superArmorList=superArmorList;
        if(!ReferenceEquals(configFile.adjustMotionList,adjustMotionList)) configFile.adjustMotionList=adjustMotionList;
        if(!ReferenceEquals(configFile.trailToggleList,trailToggleList)) configFile.trailToggleList=trailToggleList;
        if(!ReferenceEquals(configFile.moveSegmentList,moveSegmentList)) configFile.moveSegmentList=moveSegmentList;
        if(!ReferenceEquals(configFile.interactionWindows,interactionWindows)) configFile.interactionWindows=interactionWindows;
        if(!ReferenceEquals(configFile.animSegments,animSegments)) configFile.animSegments=animSegments;
        if(!ReferenceEquals(configFile.tracks,trackList)) configFile.tracks=trackList;
        if(configFile.animSegments.Count>0) configFile.skillClip=configFile.animSegments[0].clip;
    }

    readonly ActionEditorGesture _editGesture=new ActionEditorGesture();
    SkillConfigSO _pendingEditConfig;
    void PushUndo(){
        if(configFile==null) return;
        ActionEditorTransaction.Record(configFile,"Edit Action");
        BindListsToConfig();
        // Menu callbacks mutate the selected asset synchronously. Capture its identity;
        // a later selection change must never commit cached fields to the new asset.
        _pendingEditConfig=configFile;
        EditorApplication.delayCall-=CommitDeferredChanges;
        EditorApplication.delayCall+=CommitDeferredChanges;
    }
    void UndoLatest(){
        _editGesture.Complete();
        CommitDeferredChanges();
        Undo.PerformUndo();
    }
    void RedoLatest(){
        _editGesture.Complete();
        Undo.PerformRedo();
    }

    void PrepareUndoForCurrentEvent(){
        if(configFile==null) return;
        var e=Event.current;
        if(e.type==EventType.MouseDown||e.type==EventType.KeyDown||e.type==EventType.ExecuteCommand||e.type==EventType.ContextClick||e.type==EventType.DragPerform)
            ActionEditorTransaction.Record(configFile,"Edit Action");
    }

    void CommitGuiChanges(){
        if(configFile==null) return;
        BindListsToConfig();
        ActionEditorTransaction.MarkChanged(configFile);
        RefreshValidation();
    }

    void CommitDeferredChanges(){
        EditorApplication.delayCall-=CommitDeferredChanges;
        if(_pendingEditConfig==null) return;
        ActionEditorTransaction.MarkChanged(_pendingEditConfig);
        _pendingEditConfig=null;
        RefreshValidation();
        Repaint();
    }

    void RefreshValidation(){
        _validationIssues.Clear();
        _validationErrorCount=0;
        _validationWarningCount=0;
        if(configFile==null) return;
        _validationIssues.AddRange(ActionConfigValidator.Validate(configFile));
        for(int i=0;i<_validationIssues.Count;i++){
            if(_validationIssues[i].Severity==ActionValidationSeverity.Error) _validationErrorCount++;
            else if(_validationIssues[i].Severity==ActionValidationSeverity.Warning) _validationWarningCount++;
        }
        if(_validationErrorCount>0) playFrame=false;
    }

    void MigrateCurrentConfig(){
        if(configFile==null) return;
        if(!EditorUtility.DisplayDialog("Upgrade action",$"Back up {configFile.name}, then upgrade to schema {ActionConfigMigrationService.CurrentSchemaVersion}.","Back up and upgrade","Cancel")) return;
        if(ActionConfigMigrationService.MigrateWithBackup(configFile,out var backup,out var error)){
            Debug.Log($"[ActionEditor] Upgraded {configFile.name}. Backup: {backup}");
            LoadConfig();
        }else EditorUtility.DisplayDialog("Migration failed",error,"OK");
    }

    void RestoreCurrentConfig(){
        if(configFile==null) return;
        string file=EditorUtility.OpenFilePanel("Select action backup", "Assets/ActionEditorBackups", "asset");
        if(string.IsNullOrEmpty(file)) return;
        string path=FileUtil.GetProjectRelativePath(file);
        var backup=AssetDatabase.LoadAssetAtPath<SkillConfigSO>(path);
        if(backup==null){ EditorUtility.DisplayDialog("Cannot restore","Select an action backup asset inside this project.","OK"); return; }
        if(!EditorUtility.DisplayDialog("Restore action",$"Restore all data in {configFile.name} from {path}.\nCurrent unsaved data will be backed up. Asset identity and scene references are preserved.","Back up and restore","Cancel")) return;
        if(ActionConfigMigrationService.RestoreWithBackup(configFile,backup,out var recovery,out var error)){
            LoadConfig(); Debug.Log($"[ActionEditor] Restored. Recovery backup: {recovery}");
        }else EditorUtility.DisplayDialog("Restore failed",error,"OK");
    }

    void FitTimeline(){
        int total=Mathf.Max(1,GetTotalFrames());
        float available=Mathf.Max(100f,position.width-TRACK_HEADER_WIDTH-40f-(position.width>=WIDE_LAYOUT_THRESHOLD?inspectorWidth:0f));
        pixelsPerFrame=Mathf.Clamp(available/total,MIN_PPF,MAX_PPF);
        timelineScrollPos.x=0f;
        Repaint();
    }
    #endregion
}
