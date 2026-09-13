using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ethan.ActionEditor.Samples
{
    public static class SamplePlayerBuild
    {
        public static void Run()
        {
            if(!Application.isBatchMode) throw new InvalidOperationException("Batch-only sample build entrypoint.");
            try
            {
                BasicSampleBuilder.Create();
                string basic=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
                InteractionSampleBuilder.Create();
                string interaction=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
                Directory.CreateDirectory("Builds");
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{basic,interaction},locationPathName="Builds/ACTSample.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development });
                File.WriteAllText("Builds/result.json",JsonUtility.ToJson(new Result { success=report.summary.result==BuildResult.Succeeded,result=report.summary.result.ToString(),errors=(int)report.summary.totalErrors,bytes=(long)report.summary.totalSize },true));
                EditorApplication.Exit(report.summary.result==BuildResult.Succeeded ? 0 : 2);
            }
            catch(Exception exception) { Debug.LogException(exception); EditorApplication.Exit(2); }
        }
        [Serializable] sealed class Result { public bool success; public string result; public int errors; public long bytes; }
    }
}
