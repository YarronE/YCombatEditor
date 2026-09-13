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
    #region 动画采样与更新
    void SampleAnim(){
        if(!IsPreviewAllowed) return;
        if(previewModel==null) return;
        EnsurePreviewInstance();
        var eff=GetEffectivePreviewModel();
        if(eff==null) return;
        if(configFile!=null && configFile.UsesExplicitTiming){
            try { SampleExplicitPreview(eff,frameSelectIndex); }
            catch(Exception exception) { StopPreviewSession(); Debug.LogException(exception); return; }
            if(last_frameSelectIndex!=frameSelectIndex) PlayFxAndSound(last_frameSelectIndex,frameSelectIndex);
            last_frameSelectIndex=frameSelectIndex;
            return;
        }
        // 确保处于 AnimationMode（防止 InvalidOperationException）
        if(!AnimationMode.InAnimationMode()){
            try{ AnimationMode.StartAnimationMode(); }catch{ return; }
        }
        // 找当前帧对应的 segment 并采样
        foreach(var seg in animSegments){
            if(seg.clip==null) continue;
            if(frameSelectIndex>=seg.startFrame&&frameSelectIndex<seg.EndFrame){
                int localFrame=frameSelectIndex-seg.startFrame+seg.clipStartFrame;
                float fps=seg.clip.frameRate>0?seg.clip.frameRate:30;
                AnimationMode.SampleAnimationClip(eff,seg.clip,localFrame/fps);
                break;
            }
        }
        if(last_frameSelectIndex!=frameSelectIndex) PlayFxAndSound(last_frameSelectIndex,frameSelectIndex);
        last_frameSelectIndex=frameSelectIndex;
    }
    void OnEditorUpdate(){
        double ct=EditorApplication.timeSinceStartup; float dt=(float)(ct-m_LastEditorTime); m_LastEditorTime=ct;
        AnimPlayUpdate(dt); FxUpdate(dt); ProjUpdate(dt);
    }
    void AnimPlayUpdate(float dt){
        if(!IsPreviewAllowed){ playFrame=false; return; }
        if(!playFrame) return;
        float fps=30f;
        foreach(var s in animSegments) if(s.clip!=null&&s.clip.frameRate>0){ fps=s.clip.frameRate; break; }
        if(configFile!=null && configFile.UsesExplicitTiming) fps=ActionTiming.FrameRate(configFile);
        frameRate=1f/fps;
        int mx=configFile!=null && configFile.UsesExplicitTiming ? configFile.RuntimeEndFrame+1 : GetTotalFrames(); if(mx<=0) return;
        playTimer+=dt; while(playTimer>frameRate){ playTimer-=frameRate; frameSelectIndex++; if(frameSelectIndex>=mx){ frameSelectIndex=0; CleanPreviewFx(); } }
        Repaint();
    }
    void FxUpdate(float dt){
        if(Application.isPlaying||_previewFxInstances.Count==0) return;
        bool anyRemoved=false;
        for(int i=_previewFxInstances.Count-1;i>=0;i--){
            var entry=_previewFxInstances[i];
            if(entry.go==null){ _previewFxInstances.RemoveAt(i); anyRemoved=true; continue; }
            float step=dt*entry.speed;
            entry.duration+=step;
            _previewFxInstances[i]=entry;
            if(entry.duration>entry.maxLifetime+0.5f){
                DestroyImmediate(entry.go);
                _previewFxInstances.RemoveAt(i);
                anyRemoved=true;
                continue;
            }
            foreach(var p in entry.go.GetComponentsInChildren<ParticleSystem>())
                p.Simulate(step,false,false,false);
        }
        if(anyRemoved&&_previewFxInstances.Count==0) _spawnedFxIndices.Clear();
        if(_previewFxInstances.Count>0) SceneView.RepaintAll();
    }
    /// <summary>驱动投掷物预览实例每帧移动</summary>
    void ProjUpdate(float dt){
        if(Application.isPlaying||_previewProjInstances.Count==0) return;
        bool anyRemoved=false;
        for(int i=_previewProjInstances.Count-1;i>=0;i--){
            var entry=_previewProjInstances[i];
            if(entry.go==null){ _previewProjInstances.RemoveAt(i); anyRemoved=true; continue; }
            entry.elapsed+=dt;
            if(entry.elapsed>entry.lifetime+0.5f){
                DestroyImmediate(entry.go);
                _previewProjInstances.RemoveAt(i);
                anyRemoved=true;
                continue;
            }
            entry.go.transform.position+=entry.velocity*dt;
            _previewProjInstances[i]=entry;
        }
        if(anyRemoved) { /* 清理完毕 */ }
        if(_previewProjInstances.Count>0) SceneView.RepaintAll();
    }

    /// <summary>计算投掷物发射点世界坐标</summary>
    Vector3 GetProjectileFirePosition(Global.Projectile p,GameObject eff){
        if(!string.IsNullOrEmpty(p.firePointPath)){
            Transform fp=eff.transform.Find(p.firePointPath);
            if(fp!=null) return fp.position;
        }
        return eff.transform.TransformPoint(p.spawnOffset);
    }

    /// <summary>在编辑器预览中生成投掷物：Instantiate 预制体并模拟直线飞行</summary>
    void SpawnPreviewProjectile(Global.Projectile p){
        if(p.prefab==null) return;
        var projGo=Instantiate(p.prefab);
        projGo.hideFlags=HideFlags.DontSave|HideFlags.HideInHierarchy;
        projGo.AddComponent<EditorPreviewFxTag>().hideFlags=HideFlags.DontSave|HideFlags.HideInInspector;

        // ── 禁用运行时脚本（ProjectileBehaviour等），避免编辑器报错 ──
        foreach(var mb in projGo.GetComponentsInChildren<MonoBehaviour>()){
            if(mb is EditorPreviewFxTag) continue;
            mb.enabled=false;
        }
        // ── 禁用碰撞体 ──
        foreach(var col in projGo.GetComponentsInChildren<Collider>()) col.enabled=false;

        // ── 计算发射点和方向 ──
        EnsurePreviewInstance(); var eff=GetEffectivePreviewModel();
        Vector3 firePos=Vector3.zero;
        Quaternion fireRot=Quaternion.identity;
        if(eff!=null){
            firePos=GetProjectileFirePosition(p,eff);
            fireRot=eff.transform.rotation;
        }
        Vector3 fireDir=fireRot*Vector3.forward;

        // ── 应用旋转 ──
        Quaternion finalRot=Quaternion.LookRotation(fireDir)*Quaternion.Euler(p.spawnRotation);
        projGo.transform.position=firePos;
        projGo.transform.rotation=finalRot;

        // ── 应用缩放 ──
        if(p.scale!=Vector3.zero) projGo.transform.localScale=p.scale;

        // ── 计算速度 ──
        float speed=Mathf.Max(p.speed,0.1f);
        Vector3 velocity=fireDir*speed;

        // ── 散射多发 ──
        if(p.shotCount>1){
            float halfSpread=p.spreadAngle*0.5f;
            for(int s=0;s<p.shotCount;s++){
                float angle=Mathf.Lerp(-halfSpread,halfSpread,(float)s/(p.shotCount-1));
                Vector3 spreadDir=Quaternion.AngleAxis(angle,Vector3.up)*fireDir;
                Quaternion spreadRot=Quaternion.LookRotation(spreadDir)*Quaternion.Euler(p.spawnRotation);
                if(s==0){
                    // 第一发用已创建的实例
                    projGo.transform.rotation=spreadRot;
                    velocity=spreadDir*speed;
                }else{
                    // 额外发射创建副本
                    var extraGo=Instantiate(p.prefab);
                    extraGo.hideFlags=HideFlags.DontSave|HideFlags.HideInHierarchy;
                    extraGo.AddComponent<EditorPreviewFxTag>().hideFlags=HideFlags.DontSave|HideFlags.HideInInspector;
                    foreach(var mb in extraGo.GetComponentsInChildren<MonoBehaviour>()){
                        if(mb is EditorPreviewFxTag) continue;
                        mb.enabled=false;
                    }
                    foreach(var col in extraGo.GetComponentsInChildren<Collider>()) col.enabled=false;
                    extraGo.transform.position=firePos;
                    extraGo.transform.rotation=spreadRot;
                    if(p.scale!=Vector3.zero) extraGo.transform.localScale=p.scale;
                    _previewProjInstances.Add(new PreviewProjEntry{go=extraGo,velocity=spreadDir*speed,elapsed=0f,lifetime=p.lifetime});
                }
            }
        }

        _previewProjInstances.Add(new PreviewProjEntry{go=projGo,velocity=velocity,elapsed=0f,lifetime=p.lifetime});
    }

    /// <summary>清理所有投掷物预览实例</summary>
    void CleanPreviewProj(){ foreach(var e in _previewProjInstances) if(e.go!=null) DestroyImmediate(e.go); _previewProjInstances.Clear(); }
    #endregion

    #region SceneGUI
    void OnSceneGUICallback(SceneView sv){ if(!IsPreviewAllowed) return; OnSceneGUI_Draw(sv); }
    void OnSceneGUI_Draw(SceneView sv){
        if(previewModel==null) return;
        EnsurePreviewInstance(); var eff=GetEffectivePreviewModel(); if(eff==null) return;
        // 保存矩阵，确保任何异常路径都能恢复
        var savedHandlesMatrix=Handles.matrix;
        try{
        var l2w=eff.transform.localToWorldMatrix;

        // ── 收集当前帧范围内所有活跃的攻击判定 ──
        var activeAtks=FindActiveAttacks(frameSelectIndex);

        // ── 特效位置 Gizmo + Handle（在 Scene 中直接调整特效偏移） ──
        DrawFxSceneHandles(eff,l2w);

        // ── 位移段可视化（方向/目标点/落点/范围圈/偏移 Handle） ──
        DrawMoveSegmentSceneHandles(eff,l2w);

        // ── 选中的攻击 (可交互操控 Handle) ──
        int selAtkIdx=(selType==SelType.Attack&&selIdx>=0&&selIdx<atkList.Count)?selIdx:-1;


        if(activeAtks.Count==0) return;

        // ── 绘制所有活跃攻击的判定框 ──
        for(int ai=0;ai<activeAtks.Count;ai++){
            int aidx=activeAtks[ai]; var a=atkList[aidx];
            bool isSel=aidx==selAtkIdx;
            bool rw=a.isReWrite;

            // 计算判定框参数 — 支持骨骼跟踪模式
            IJudgeArea area;
            Vector3 curOffset;
            if(!rw){ curOffset=new Vector3(offsetX,offsetY,offsetZ);
                switch(skillShapeSelectIndex){
                case 1: area=boxItem; area.SetValue(range1,1,range2,curOffset.x,curOffset.y,curOffset.z); break;
                default: area=sphereItem; area.SetValue(range1,0,0,curOffset.x,curOffset.y,curOffset.z); break; }
            }else{
                // 骨骼跟踪模式：根据当前帧从boneOffsets取偏移
                curOffset=a.GetOffsetAtFrame(frameSelectIndex);
                switch(a.shapeType){
                case 1: area=boxItem; area.SetValue(a.parameter1,1,a.parameter2,curOffset.x,curOffset.y,curOffset.z); break;
                default: area=sphereItem; area.SetValue(a.parameter1,0,0,curOffset.x,curOffset.y,curOffset.z); break; } }

            Handles.matrix=Matrix4x4.TRS(l2w.MultiplyPoint3x4(Vector3.zero),l2w.rotation,l2w.lossyScale);

            // 选中的用亮红色，非选中的用半透明橙色
            Color fillCol=isSel?new Color(1,0,0,.25f):new Color(1,.5f,0,.12f);
            Color wireCol=isSel?new Color(1,0,0,.8f):new Color(1,.5f,0,.4f);
            HandlesDrawTool.H.PushColor(fillCol); HandlesDrawTool.H.isFill=true;
            switch(area){ case BoxItem v: HandlesDrawTool.H.DrawBox(v.size,Matrix4x4.Translate(v.offset)); break;
                case SphereItem v: HandlesDrawTool.H.DrawSphere(v.radius,Matrix4x4.Translate(v.offset)); break; }
            HandlesDrawTool.H.isFill=false; HandlesDrawTool.H.PopColor();
            // 线框
            HandlesDrawTool.H.PushColor(wireCol);
            switch(area){ case BoxItem v: HandlesDrawTool.H.DrawBox(v.size,Matrix4x4.Translate(v.offset)); break;
                case SphereItem v: HandlesDrawTool.H.DrawSphere(v.radius,Matrix4x4.Translate(v.offset)); break; }
            HandlesDrawTool.H.PopColor();

            // 帧标签
            Vector3 labelWorldPos;
            switch(area){ case BoxItem v: labelWorldPos=l2w.MultiplyPoint3x4(v.offset+Vector3.up*v.size.y*0.5f); break;
                case SphereItem v: labelWorldPos=l2w.MultiplyPoint3x4(v.offset+Vector3.up*v.radius); break;
                default: labelWorldPos=l2w.MultiplyPoint3x4(Vector3.zero); break; }
            string fLabel=a.endKeyNumber>a.keyNumber?$"Atk[{aidx}] F{a.keyNumber}-{a.endKeyNumber}":$"Atk[{aidx}] F{a.keyNumber}";
            EnsureSceneLabelStyles();
            if(_sceneLabelStyle!=null&&_sceneSelLabelStyle!=null)
                Handles.Label(labelWorldPos,fLabel,isSel?_sceneSelLabelStyle:_sceneLabelStyle);

            // 仅选中的攻击可以操控 Handle
            if(isSel){
                _judgment.value=area;
                DrawHandle(area);
            }
        }

        // ── 重置 Handles.matrix，防止污染后续 Unity 渲染 ──
        Handles.matrix=Matrix4x4.identity;

        // ── 聚焦功能 — 按F键聚焦到选中攻击判定框 ──
        if(selAtkIdx>=0&&Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.F){
            var a=atkList[selAtkIdx];
            Vector3 center; float size;
            if(a.isReWrite){
                center=l2w.MultiplyPoint3x4(a.GetOffsetAtFrame(frameSelectIndex));
                size=a.shapeType==0?a.parameter1*2.5f:Mathf.Max(a.parameter1,a.parameter2)*2.5f;
            }else{
                center=l2w.MultiplyPoint3x4(new Vector3(offsetX,offsetY,offsetZ));
                size=skillShapeSelectIndex==0?range1*2.5f:Mathf.Max(range1,range2)*2.5f;
            }
            size=Mathf.Max(size,1f);
            sv.LookAt(center,sv.rotation,size);
            Event.current.Use();
        }
        }finally{ Handles.matrix=savedHandlesMatrix; }
    }


    // Scene标签样式（延迟初始化）
    static GUIStyle _sceneLabelStyle, _sceneSelLabelStyle;
    static GUIStyle _sceneFxLabelStyle, _sceneSelFxLabelStyle;

    /// <summary>在 Scene 视图中绘制所有当前帧活跃特效的位置标记和拖拽 Handle</summary>
    void DrawFxSceneHandles(GameObject eff,Matrix4x4 l2w){
        if(fxAndSoundList==null||fxAndSoundList.Count==0) return;
        Handles.matrix=Matrix4x4.identity;
        EnsureSceneLabelStyles();

        int selFxIdx=selType==SelType.Fx&&selIdx>=0&&selIdx<fxAndSoundList.Count?selIdx:-1;

        for(int i=0;i<fxAndSoundList.Count;i++){
            var fx=fxAndSoundList[i];
            if(fx.particleSystem==null) continue;

            // 判断当前帧是否在特效活跃范围内
            int begin=fx.keyNumber;
            int end=fx.endKeyNumber>fx.keyNumber?fx.endKeyNumber:fx.keyNumber;
            bool isActive=frameSelectIndex>=begin&&frameSelectIndex<=end;

            // 计算世界位置
            Vector3 worldPos;
            if(fx.useWorldSpace)
                worldPos=fx.offset;
            else
                worldPos=eff.transform.TransformPoint(fx.offset);

            bool isSel=i==selFxIdx;

            // 活跃的特效 或 选中的特效 才显示
            if(isActive||isSel){
                float handleSize=HandleUtility.GetHandleSize(worldPos)*0.05f;
                Color dotColor=isSel?new Color(0.3f,1f,0.5f,1f):new Color(0.3f,0.85f,0.45f,0.6f);
                Handles.color=dotColor;
                Handles.SphereHandleCap(0,worldPos,Quaternion.identity,handleSize*2f,EventType.Repaint);

                // 标签
                string label=fx.particleSystem!=null?fx.particleSystem.name:$"FX[{i}]";
                if(isActive) label=$" {label} F{begin}";
                if(_sceneLabelStyle!=null){
                    var fxLabelStyle=isSel?_sceneSelFxLabelStyle:_sceneFxLabelStyle;
                    if(fxLabelStyle!=null) Handles.Label(worldPos+Vector3.up*handleSize*4f,label,fxLabelStyle);
                }

                // 选中的特效：完整的 Position + Rotation Handle
                if(isSel){
                    // ── Position Handle ──
                    Quaternion handleRot=fx.followCharacter&&!fx.useWorldSpace?eff.transform.rotation:Quaternion.identity;
                    EditorGUI.BeginChangeCheck();
                    Vector3 newWorldPos=Handles.DoPositionHandle(worldPos,handleRot);
                    if(EditorGUI.EndChangeCheck()){
                        Undo.RecordObject(configFile,"Move effect");
                        if(fx.useWorldSpace)
                            fx.offset=newWorldPos;
                        else
                            fx.offset=eff.transform.InverseTransformPoint(newWorldPos);
                    }

                    // ── Rotation Handle ──
                    Quaternion curRot;
                    if(fx.useWorldSpace)
                        curRot=Quaternion.Euler(fx.rotation);
                    else if(fx.followCharacter)
                        curRot=eff.transform.rotation*Quaternion.Euler(fx.rotation);
                    else
                        curRot=Quaternion.Euler(fx.rotation);
                    EditorGUI.BeginChangeCheck();
                    Quaternion newRot=Handles.DoRotationHandle(curRot,worldPos);
                    if(EditorGUI.EndChangeCheck()){
                        Undo.RecordObject(configFile,"Rotate effect");
                        if(fx.useWorldSpace)
                            fx.rotation=newRot.eulerAngles;
                        else if(fx.followCharacter)
                            fx.rotation=(Quaternion.Inverse(eff.transform.rotation)*newRot).eulerAngles;
                        else
                            fx.rotation=newRot.eulerAngles;
                    }

                    // ── Scale 指示（非交互，仅可视化） ──
                    if(fx.scale!=Vector3.one){
                        float avgScale=(fx.scale.x+fx.scale.y+fx.scale.z)/3f;
                        Handles.color=new Color(0.3f,1f,0.5f,0.15f);
                        Handles.DrawWireDisc(worldPos,Vector3.up,handleSize*8f*avgScale);
                    }
                }
                else if(fx.rotation!=Vector3.zero){
                    // 非选中但有旋转：小箭头指示朝向
                    Quaternion rot=fx.followCharacter?
                        eff.transform.rotation*Quaternion.Euler(fx.rotation):
                        Quaternion.Euler(fx.rotation);
                    Handles.color=new Color(0.3f,1f,0.5f,0.4f);
                    Handles.ArrowHandleCap(0,worldPos,rot,handleSize*8f,EventType.Repaint);
                }
            }
        }

        // ── F键聚焦到选中特效 ──
        if(selFxIdx>=0&&Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.F){
            var fx=fxAndSoundList[selFxIdx];
            Vector3 focusPos=fx.useWorldSpace?fx.offset:eff.transform.TransformPoint(fx.offset);
            SceneView sv=SceneView.lastActiveSceneView;
            if(sv!=null) sv.LookAt(focusPos,sv.rotation,2f);
            Event.current.Use();
        }
    }

    /// <summary>SceneView 位移段可视化：方向箭头、目标点标记、落点、范围圈、偏移 Handle。</summary>
    void DrawMoveSegmentSceneHandles(GameObject eff,Matrix4x4 l2w){
        if(moveSegmentList==null||moveSegmentList.Count==0) return;
        EnsureSceneLabelStyles();

        Vector3 selfPos=eff.transform.position;
        Quaternion selfRot=eff.transform.rotation;
        float hs=HandleUtility.GetHandleSize(selfPos);

        // 目标：优先锁定目标，其次最近敌人；编辑器内无运行时索敌，退化为「无目标」提示。
        Transform target=null;
        IActionTargetProvider targetProvider=null;
        foreach(var c in eff.GetComponents<MonoBehaviour>()) if(c is IActionTargetProvider provider){ targetProvider=provider; break; }
        if(targetProvider!=null) target=targetProvider.ResolveTarget(Global.MoveTargetKind.LockedTarget);
        if(target==null){
            // 尝试从场景找最近的通用动作 Actor。
            target=FindNearestSceneTarget(eff);
        }

        int selMoveIdx=selType==SelType.Move&&selIdx>=0&&selIdx<moveSegmentList.Count?selIdx:-1;

        for(int i=0;i<moveSegmentList.Count;i++){
            var m=moveSegmentList[i];
            int begin=m.keyNumber,end=SegEnd(m.keyNumber,m.endKeyNumber);
            bool isActive=frameSelectIndex>=begin&&frameSelectIndex<=end;
            bool isSel=i==selMoveIdx;

            if(m.driveMode==Global.MoveDriveMode.Animation){
                if(isSel||isActive){
                    Handles.color=new Color(0.5f,0.8f,1f,isSel?1f:0.5f);
                    Handles.ArrowHandleCap(0,selfPos,selfRot,hs*1.5f,EventType.Repaint);
                    if(_sceneLabelStyle!=null) Handles.Label(selfPos+Vector3.up*hs*0.8f,$"Root motion x{m.rootMotionScale:F1} F{begin}-{end}",
                        isSel?_sceneSelLabelStyle:_sceneLabelStyle);
                }
                continue;
            }

            // ── 方向解算（复用 MoveSolver） ──
            var ctx=new MoveSolveContext{
                selfPosition=selfPos, selfRotation=selfRot, inputDirection=Vector3.zero,
                hasTarget=target!=null,
                targetPosition=target!=null?target.position:Vector3.zero,
                targetRotation=target!=null?target.rotation:Quaternion.identity };
            Vector3 dir=MoveSolver.ResolveDirection(m,ctx);
            Vector3 targetPoint=MoveSolver.ResolveTargetPoint(m,ctx);

            if(!(isSel||isActive)) continue;

            // ── 方向箭头 ──
            Handles.color=isSel?new Color(0.3f,1f,0.5f,1f):new Color(0.3f,1f,0.5f,0.4f);
            Handles.ArrowHandleCap(0,selfPos,Quaternion.LookRotation(dir,Vector3.up),hs*1.2f,EventType.Repaint);

            // ── 目标点标记 + 连线（仅中心为目标时） ──
            if(m.origin==Global.MoveOrigin.Target){
                if(ctx.hasTarget){
                    Handles.color=new Color(1f,0.6f,0.2f,isSel?1f:0.5f);
                    Handles.DrawDottedLine(ctx.targetPosition,targetPoint,4f);
                    Handles.SphereHandleCap(0,targetPoint,Quaternion.identity,hs*0.08f,EventType.Repaint);
                    if(_sceneLabelStyle!=null) Handles.Label(targetPoint+Vector3.up*hs*0.3f,"Target point",isSel?_sceneSelLabelStyle:_sceneLabelStyle);
                }else if(isSel){
                    if(_sceneLabelStyle!=null) Handles.Label(selfPos+Vector3.up*hs*0.9f,"No preview target assigned.",_sceneLabelStyle);
                }
            }

            // ── 预期落点（按曲线终值与最大距离裁剪推算） ──
            if(m.maxDistance>0f){
                float endVal=m.curve!=null?m.curve.Evaluate(1f):1f;
                float finalDist=Mathf.Min(endVal*m.maxDistance,m.maxDistance);
                Vector3 landing=selfPos+dir*finalDist;
                Handles.color=new Color(0.3f,0.7f,1f,isSel?0.9f:0.4f);
                Handles.DrawWireDisc(landing,Vector3.up,hs*0.12f);
                if(_sceneLabelStyle!=null&&isSel) Handles.Label(landing+Vector3.up*hs*0.2f,$"Destination {finalDist:F1}m",_sceneLabelStyle);
            }

            // ── maxDistance 范围圈（以角色为心） ──
            if(m.maxDistance>0f&&isSel){
                Handles.color=new Color(0.3f,0.7f,1f,0.25f);
                Handles.DrawWireDisc(selfPos,Vector3.up,m.maxDistance);
            }

            // ── minKeepDistance 范围圈（以目标为心） ──
            if(m.minKeepDistance>0f&&ctx.hasTarget&&isSel){
                Handles.color=new Color(1f,0.3f,0.3f,0.3f);
                Handles.DrawWireDisc(ctx.targetPosition,Vector3.up,m.minKeepDistance);
            }

            // ── targetOffset 的 PositionHandle（仅中心为目标且有目标时） ──
            if(isSel&&m.origin==Global.MoveOrigin.Target&&ctx.hasTarget){
                EditorGUI.BeginChangeCheck();
                Vector3 newPoint=Handles.DoPositionHandle(targetPoint,Quaternion.identity);
                if(EditorGUI.EndChangeCheck()){
                    Undo.RecordObject(configFile,"Move target offset");
                    Vector3 newOffset=m.offsetSpace==Global.OffsetSpace.TargetLocal
                        ? Quaternion.Inverse(ctx.targetRotation)*(newPoint-ctx.targetPosition)
                        : newPoint-ctx.targetPosition;
                    m.targetOffset=newOffset;
                }
            }

            // ── 标签 ──
            if(_sceneLabelStyle!=null)
                Handles.Label(selfPos+Vector3.up*hs*1.2f,$" {MoveSegLabel(m,begin,end)}",isSel?_sceneSelLabelStyle:_sceneLabelStyle);
        }
    }

    Transform FindNearestSceneTarget(GameObject self){
        float closest=float.MaxValue; Transform best=null;
        var cols=Physics.OverlapSphere(self.transform.position,20f);
        foreach(var col in cols){
            if(col.transform.root==self.transform.root) continue;
            var d=col.GetComponent<IActionActor>(); if(d==null) d=col.GetComponentInParent<IActionActor>();
            if(d==null) continue;
            float dist=Vector3.Distance(self.transform.position,col.transform.position);
            if(dist<closest){ closest=dist; best=col.transform; }
        }
        return best;
    }
    static void EnsureSceneLabelStyles(){
        // EditorStyles 在某些时序下（如 OnEnable 首次调用）可能还未初始化，此时 boldLabel 为 null
        try{
            if(EditorStyles.boldLabel==null) return;
        }catch{ return; }
        if(_sceneLabelStyle==null){
            _sceneLabelStyle=new GUIStyle(EditorStyles.boldLabel);
            _sceneLabelStyle.normal.textColor=new Color(1f,.6f,.2f,.8f);
            _sceneLabelStyle.fontSize=10;
        }
        if(_sceneSelLabelStyle==null){
            _sceneSelLabelStyle=new GUIStyle(EditorStyles.boldLabel);
            _sceneSelLabelStyle.normal.textColor=new Color(1f,.2f,.2f,1f);
            _sceneSelLabelStyle.fontSize=11;
        }
        if(_sceneFxLabelStyle==null){
            _sceneFxLabelStyle=new GUIStyle(EditorStyles.boldLabel);
            _sceneFxLabelStyle.normal.textColor=new Color(0.3f,0.85f,0.45f,0.8f);
            _sceneFxLabelStyle.fontSize=10;
        }
        if(_sceneSelFxLabelStyle==null){
            _sceneSelFxLabelStyle=new GUIStyle(EditorStyles.boldLabel);
            _sceneSelFxLabelStyle.normal.textColor=new Color(0.3f,1f,0.5f,1f);
            _sceneSelFxLabelStyle.fontSize=11;
        }
    }
    void DrawHandle(IJudgeArea config){
        // 保存当前矩阵，Handle 操控在自定义矩阵下进行（使偏移值为模型局部坐标）
        var savedMatrix=Handles.matrix;
        Vector3 off=Vector3.zero,sz=Vector3.one;
        switch(config){ case BoxItem v: off=v.offset; sz=v.size; break; case SphereItem v: off=v.offset; sz=new Vector2(v.radius,0); break; }
        float hs=HandleUtility.GetHandleSize(off);
        switch(Tools.current){
            case Tool.Move: off=Handles.DoPositionHandle(off,Quaternion.identity); break;
            case Tool.Scale: sz=Handles.DoScaleHandle(sz,off,Quaternion.identity,hs); break;
            case Tool.Rect: switch(config){
                case BoxItem v: boxHandle.axes=PrimitiveBoundsHandle.Axes.X|PrimitiveBoundsHandle.Axes.Z; boxHandle.center=off; boxHandle.size=sz;
                    boxHandle.DrawHandle(); off=boxHandle.center; sz=boxHandle.size; break;
                case SphereItem v: sphereHandle.axes=PrimitiveBoundsHandle.Axes.X|PrimitiveBoundsHandle.Axes.Y|PrimitiveBoundsHandle.Axes.Z;
                    sphereHandle.center=off; sphereHandle.radius=sz.x; sphereHandle.DrawHandle(); off=sphereHandle.center; sz.x=sphereHandle.radius; break;
            } break;
        }
        // 恢复矩阵
        Handles.matrix=savedMatrix;
        // 判断当前选中的攻击是否独立编辑
        bool isRW=selType==SelType.Attack&&selIdx>=0&&selIdx<atkList.Count&&atkList[selIdx].isReWrite;
        switch(config){
            case BoxItem v: v.offset=off; v.size=sz;
                if(!isRW){ offsetX=v.offset.x; offsetY=v.offset.y; offsetZ=v.offset.z; range1=v.size.x; range2=v.size.z; }
                else UpdateAtkScene(v.size.x,v.size.z,v.offset); break;
            case SphereItem v: v.offset=off; v.radius=sz.x;
                if(!isRW){ offsetX=v.offset.x; offsetY=v.offset.y; offsetZ=v.offset.z; range1=v.radius; }
                else UpdateAtkScene(v.radius,0,v.offset); break;
        }
    }
    void UpdateAtkScene(float p1,float p2,Vector3 o){
        // 优先使用选中的攻击，否则用范围查找
        int i=(selType==SelType.Attack&&selIdx>=0&&selIdx<atkList.Count)?selIdx:FindKeyIndex(frameSelectIndex);
        if(i>=0){ atkList[i].offset=o; atkList[i].parameter1=p1; atkList[i].parameter2=p2; }
    }
    #endregion

    void StopPreviewSession(){
        playFrame=false; playTimer=0; last_frameSelectIndex=-1;
        CleanPreviewFx(); DestroyPreviewInstance();
    }
    void SampleExplicitPreview(GameObject target,int frame){
        var animator=target.GetComponentInChildren<Animator>();
        if(animator==null) throw new InvalidOperationException("Preview requires an Animator.");
        var output=animator.GetComponent<ActionClipAnimator>();
        if(output==null) output=animator.gameObject.AddComponent<ActionClipAnimator>();
        output.enabled=true;
        output.ResetOutput();
        float fps=ActionTiming.FrameRate(configFile);
        foreach(var segment in animSegments.Where(s=>s?.clip!=null).OrderBy(s=>s.startFrame)){
            if(segment.startFrame>frame) break;
            output.Sample(segment.startFrame);
            output.Play(segment,fps);
        }
        output.Sample(frame);
    }
    #region 预览实例
    void EnsurePreviewInstance(){
        if(previewModel==null){ DestroyPreviewInstance(); return; }
        if(!PrefabUtility.IsPartOfPrefabAsset(previewModel) && (configFile==null || !configFile.UsesExplicitTiming)){ DestroyPreviewInstance(); previewAnim=previewModel.GetComponent<Animator>(); return; }
        if(_previewInstance!=null){ previewAnim=_previewInstance.GetComponent<Animator>(); return; }
        if(_previewInstanceRoot==null){ _previewInstanceRoot=new GameObject("SkillEditorPreviewRoot"); _previewInstanceRoot.hideFlags=HideFlags.HideAndDontSave; }
        _previewInstanceRoot.SetActive(false);
        _previewInstance=Instantiate(previewModel,_previewInstanceRoot.transform);
        foreach(var behaviour in _previewInstance.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled=false;
        _previewInstanceRoot.SetActive(true);
        _previewInstance.SetActive(true);
        _previewInstance.transform.SetParent(_previewInstanceRoot.transform); _previewInstance.hideFlags=HideFlags.HideAndDontSave;
        previewAnim=_previewInstance.GetComponent<Animator>();
    }
    void DestroyPreviewInstance(){
        if(_previewInstance!=null){ DestroyImmediate(_previewInstance); _previewInstance=null; previewAnim=null; }
        if(_previewInstanceRoot!=null){ DestroyImmediate(_previewInstanceRoot); _previewInstanceRoot=null; }
        if(previewModel==null) previewAnim=null;
    }
    GameObject GetEffectivePreviewModel()=> _previewInstance!=null?_previewInstance:previewModel;
    #endregion

    #region 骨骼采样生成连续判定框
    /// <summary>
    /// 在 Attack 的 keyNumber~endKeyNumber 范围内逐帧采样骨骼位置，
    /// 将结果存入 Attack.boneOffsets 列表。一个 Attack 即可覆盖整段帧范围。
    /// </summary>
    Transform ResolvePreviewBone(GameObject target){
        if(_trackBoneRef==null || target==null) return null;
        if(_trackBoneRef==target.transform || _trackBoneRef.IsChildOf(target.transform)) return _trackBoneRef;
        if(previewModel!=null && (_trackBoneRef==previewModel.transform || _trackBoneRef.IsChildOf(previewModel.transform))){
            string path=AnimationUtility.CalculateTransformPath(_trackBoneRef,previewModel.transform);
            return string.IsNullOrEmpty(path) ? target.transform : target.transform.Find(path);
        }
        return null;
    }
    void SampleBoneTrackingOffsets(Global.Attack a){
        if(_trackBoneRef==null||previewModel==null){ Debug.LogWarning("Assign a tracking bone and preview actor first."); return; }
        EnsurePreviewInstance(); var eff=GetEffectivePreviewModel(); if(eff==null) return;

        var sampledBone=ResolvePreviewBone(eff); if(sampledBone==null){ Debug.LogWarning("The tracking bone must belong to the preview actor."); return; }
        int startF=a.keyNumber;
        int endF=a.endKeyNumber>a.keyNumber?a.endKeyNumber:a.keyNumber;
        int count=endF-startF+1;

        if(a.boneOffsets==null) a.boneOffsets=new List<Vector3>();
        a.boneOffsets.Clear();

        for(int f=startF;f<=endF;f++){
            // 采样动画到指定帧
            SampleAnimAtFrame(eff,f);
            // 获取骨骼在角色局部空间的位置
            Vector3 localPos=eff.transform.InverseTransformPoint(sampledBone.position);
            a.boneOffsets.Add(localPos);
        }

        a.useBoneTracking=true;
        a.isReWrite=true;

        // 将首帧的偏移也写入静态offset（作为回退值）
        if(a.boneOffsets.Count>0) a.offset=a.boneOffsets[0];

        // 恢复当前帧动画
        SampleAnim();
        Debug.Log($"Bone sampling completed: {count} offsets, frames {startF}-{endF}.");
    }

    /// <summary>更新单帧的骨骼偏移</summary>
    void UpdateSingleFrameBoneOffset(Global.Attack a,int frame){
        if(_trackBoneRef==null||previewModel==null) return;
        EnsurePreviewInstance(); var eff=GetEffectivePreviewModel(); if(eff==null) return;
        var sampledBone=ResolvePreviewBone(eff); if(sampledBone==null) return;
        Vector3 localPos=eff.transform.InverseTransformPoint(sampledBone.position);
        int idx=frame-a.keyNumber;
        if(a.boneOffsets==null) a.boneOffsets=new List<Vector3>();
        // 确保列表足够长
        while(a.boneOffsets.Count<=idx) a.boneOffsets.Add(a.offset);
        if(idx>=0&&idx<a.boneOffsets.Count) a.boneOffsets[idx]=localPos;
    }

    /// <summary>采样动画到指定全局帧</summary>
    void SampleAnimAtFrame(GameObject target,int frame){
        if(configFile!=null && configFile.UsesExplicitTiming){ SampleExplicitPreview(target,frame); return; }
        foreach(var seg in animSegments){
            if(seg.clip==null) continue;
            if(frame>=seg.startFrame&&frame<seg.EndFrame){
                int localFrame=frame-seg.startFrame+seg.clipStartFrame;
                float fps=seg.clip.frameRate>0?seg.clip.frameRate:30;
                AnimationMode.SampleAnimationClip(target,seg.clip,localFrame/fps);
                return;
            }
        }
    }

    /// <summary>聚焦SceneView到指定世界坐标位置</summary>
    void FocusSceneViewOn(Vector3 worldPos,float size=2f){
        var sv=SceneView.lastActiveSceneView;
        if(sv!=null) sv.LookAt(worldPos,sv.rotation,Mathf.Max(size,1f));
    }
    #endregion
}
