using UnityEditor;
using UnityEngine;

namespace Ethan.ActionEditor.Editor
{
    public static class ActionAssetCreation
    {
        [MenuItem("Assets/Create/Combat/Skill Config")]
        public static void Create()
        {
            var config=ScriptableObject.CreateInstance<SkillConfigSO>();
            config.InitializeExplicitTiming();
            config.SetActionEditorSchemaVersion(ActionConfigMigrationService.CurrentSchemaVersion);
            ProjectWindowUtil.CreateAsset(config,"NewSkillConfig.asset");
        }
    }
}
