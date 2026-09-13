using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ethan.ActionEditor.Editor
{
    /// <summary>Small, explicit Editor API for project agents. Execute on Unity's main thread.</summary>
    public static partial class ActionEditorAgent
    {
        [Serializable]
        public sealed class Request
        {
            public int schemaVersion = 1;
            public string operation;
            public string[] assetPaths;
            public string outputAssetPath;
            public string animationAssetPath;
            public string animationClipName;
            public string displayName;
            public int exitFrame = -1;
            public string expectedDigest;
            public bool dryRun;
            public Edit[] edits;
        }

        [Serializable]
        public sealed class Issue
        {
            public string assetPath;
            public string code;
            public string severity;
            public string track;
            public int eventIndex;
            public string message;
        }

        [Serializable]
        public sealed class Result
        {
            public int schemaVersion = 1;
            public bool success;
            public string operation;
            public string unityVersion;
            public string packageVersion;
            public string createdAssetPath;
            public string[] actionAssets = Array.Empty<string>();
            public string[] animationAssets = Array.Empty<string>();
            public List<Issue> issues = new List<Issue>();
            public string contentDigest;
            public List<Field> fields = new List<Field>();
            public List<Change> changes = new List<Change>();
        }

        public static Result Execute(Request request)
        {
            var result = NewResult(request?.operation);
            try
            {
                if (request == null || (request.schemaVersion != 1 && request.schemaVersion != 2))
                    throw new ArgumentException("Request schemaVersion must be 1 or 2.");
                result.schemaVersion = request.schemaVersion;
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Exit Play Mode before running authoring operations.");
                switch (request.operation)
                {
                    case "inventory":
                        result.actionAssets = FindAssets("t:SkillConfigSO");
                        result.animationAssets = FindAssets("t:AnimationClip");
                        break;
                    case "validate":
                        if (request.assetPaths == null || request.assetPaths.Length == 0)
                            throw new ArgumentException("validate requires explicit assetPaths; zero checked assets is not a pass.");
                        foreach (string path in request.assetPaths)
                        {
                            var config = AssetDatabase.LoadAssetAtPath<SkillConfigSO>(path);
                            if (config == null) AddError(result, "AGENT_ASSET_MISSING", "Action asset not found.", path);
                            else AddIssues(result, path, ActionConfigValidator.Validate(config));
                        }
                        break;
                    case "create_action":
                        CreateAction(request, result);
                        break;
                    case "read_action": case "clone_action": case "edit_action":
                        if (request.schemaVersion != 2) throw new ArgumentException("These operations require schemaVersion 2.");
                        EditAction(request, result);
                        break;
                    default:
                        throw new ArgumentException("Supported operations: inventory, validate, create_action.");
                }
                result.success = result.issues.All(i => i.severity != "Error");
            }
            catch (Exception exception)
            {
                AddError(result, "AGENT_REQUEST_FAILED", exception.Message);
            }
            return result;
        }

        /// <summary>Read-only preflight. Does not run gameplay or prove custom handler correctness.</summary>
        public static Result InspectActor(GameObject actor, SkillConfigSO config)
        {
            var result = NewResult("inspect_actor");
            if (actor == null || config == null)
            {
                AddError(result, "AGENT_ACTOR_INPUT", "Provide an actor and an action config.");
                return result;
            }
            var player = actor.GetComponent<ActionPlayer>();
            if (player == null) AddError(result, "AGENT_PLAYER_MISSING", "Add ActionPlayer on the actor.");
            var behaviours = actor.GetComponents<MonoBehaviour>().Where(x => x != null && x.isActiveAndEnabled).ToArray();
            var animator = behaviours.OfType<IActionAnimator>().FirstOrDefault();
            var types = new HashSet<Type>();
            foreach (var component in behaviours)
                foreach (var contract in component.GetType().GetInterfaces())
                    if (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IActionEventHandler<>))
                        types.Add(contract.GetGenericArguments()[0]);
            var bridge = actor.GetComponent<ActionCharacterControllerDisplacement>();
            var controller = actor.GetComponent<CharacterController>();
            bool builtInMove = bridge != null && bridge.isActiveAndEnabled && controller != null && controller.enabled;
            AddIssues(result, AssetDatabase.GetAssetPath(config), ActionConfigValidator.Validate(config,
                new ActionValidationCapabilities {
                    HasAnimator = animator != null,
                    RequireEventHandlers = true,
                    CanHandleEvent = type => types.Contains(type) ||
                        (type == typeof(Global.MoveSegment) && builtInMove)
                }));
            if (player != null && !player.isActiveAndEnabled)
                AddError(result, "AGENT_PLAYER_DISABLED", "Enable the actor and ActionPlayer before playback.");
            if (animator != null)
                foreach (var segment in config.GetEffectiveSegments())
                    if (segment != null && !animator.CanPlay(segment))
                        AddError(result, "AGENT_ANIMATION_UNSUPPORTED", "The attached animation adapter cannot play a segment.");
            result.success = result.issues.All(i => i.severity != "Error");
            return result;
        }

        static void CreateAction(Request request, Result result)
        {
            string path = request.outputAssetPath;
            if (!ActionEditorSettings.TryNormalizeAssetDirectory(path, out var normalized) ||
                !normalized.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("outputAssetPath must be a new .asset file below Assets without path traversal.");
            if (File.Exists(normalized) || File.Exists(normalized + ".meta") || Directory.Exists(normalized) ||
                AssetDatabase.LoadMainAssetAtPath(normalized) != null)
                throw new IOException("Destination exists. Choose a new action path; existing assets are never overwritten.");
            var clips = AssetDatabase.LoadAllAssetsAtPath(request.animationAssetPath ?? string.Empty)
                .OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
            var clip = string.IsNullOrEmpty(request.animationClipName)
                ? (clips.Length == 1 ? clips[0] : null)
                : clips.SingleOrDefault(c => c.name == request.animationClipName);
            if (clip == null || clip.legacy || clip.length <= 0)
                throw new ArgumentException("Provide a non-Legacy, non-empty clip. For a model containing multiple clips, specify animationClipName.");
            if (request.exitFrame < -1) throw new ArgumentException("exitFrame must be -1 (derive), 0, or positive.");
            var config = ScriptableObject.CreateInstance<SkillConfigSO>();
            bool persisted = false;
            try
            {
                config.SetActionEditorSchemaVersion(ActionConfigMigrationService.CurrentSchemaVersion);
                config.InitializeExplicitTiming();
                config.skillName = string.IsNullOrWhiteSpace(request.displayName) ? Path.GetFileNameWithoutExtension(normalized) : request.displayName;
                config.animSegments.Add(new Global.AnimClipSegment { clip = clip, startFrame = 0 });
                config.exitFrame = request.exitFrame == -1 ? Math.Max(1, ActionTiming.Duration(config, config.animSegments[0])) : request.exitFrame;
                AddIssues(result, normalized, ActionConfigValidator.Validate(config));
                if (result.issues.Any(i => i.severity == "Error")) return;
                ActionConfigMigrationService.EnsureAssetDirectory(Path.GetDirectoryName(normalized).Replace('\\', '/'));
                AssetDatabase.CreateAsset(config, normalized);
                persisted = true;
                AssetDatabase.SaveAssetIfDirty(config);
                result.createdAssetPath = normalized;
            }
            finally { if (!persisted) UnityEngine.Object.DestroyImmediate(config); }
        }

        static string[] FindAssets(string filter) => AssetDatabase.FindAssets(filter, new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToArray();

        static Result NewResult(string operation)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ActionEditorAgent).Assembly);
            return new Result { operation = operation, unityVersion = Application.unityVersion,
                packageVersion = package == null ? "unknown" : package.version };
        }

        static void AddIssues(Result result, string path, IEnumerable<ActionValidationIssue> issues)
        {
            foreach (var issue in issues)
                result.issues.Add(new Issue { assetPath = path, code = issue.Code, severity = issue.Severity.ToString(),
                    track = issue.Track, eventIndex = issue.EventIndex, message = issue.Message });
        }

        static void AddError(Result result, string code, string message, string path = null)
        {
            result.success = false;
            result.issues.Add(new Issue { code = code, severity = "Error", message = message, assetPath = path, eventIndex = -1 });
        }

        /// <summary>Batch entrypoint. Results are new files under the consumer project's Logs/ActionEditorAgent.</summary>
        public static void RunFromCommandLine()
        {
            Result result;
            string output = null;
            try
            {
                var args = Environment.GetCommandLineArgs();
                string requestPath = Argument(args, "-actionEditorRequest");
                output = Path.GetFullPath(Argument(args, "-actionEditorResult"));
                string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/ActionEditorAgent")) + Path.DirectorySeparatorChar;
                if (!output.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) || File.Exists(output))
                    throw new ArgumentException("Result must be a NEW file under this project's Logs/ActionEditorAgent/.");
                // Reserve the result before any asset mutation; accidental replay cannot create an unreported action.
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                using (var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write))
                using (var writer = new StreamWriter(stream))
                {
                    try { result = Execute(JsonUtility.FromJson<Request>(File.ReadAllText(requestPath))); }
                    catch (Exception exception) { result = NewResult(null); AddError(result, "AGENT_REQUEST_FAILED", exception.Message); }
                    writer.Write(JsonUtility.ToJson(result, true));
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("[ACT Agent] " + exception.Message);
                if (Application.isBatchMode) EditorApplication.Exit(2);
                return;
            }
            if (Application.isBatchMode) EditorApplication.Exit(result.success ? 0 : 2);
        }

        static string Argument(string[] args, string key)
        {
            int index = Array.IndexOf(args, key);
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Missing argument " + key);
            return args[index + 1];
        }
    }
}
