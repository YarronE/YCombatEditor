using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ethan.ActionEditor.Editor
{
    [FilePath("UserSettings/ActionEditorUserSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class ActionEditorUserSettings : ScriptableSingleton<ActionEditorUserSettings>
    {
        [Serializable]
        sealed class PreviewBinding
        {
            public string configGuid;
            public string objectId;
        }

        [SerializeField] List<PreviewBinding> previewBindings = new List<PreviewBinding>();

        internal GameObject GetPreviewModel(SkillConfigSO config)
        {
            string guid = GetGuid(config);
            if (string.IsNullOrEmpty(guid)) return null;
            var binding = previewBindings.Find(x => x.configGuid == guid);
            if (binding == null || string.IsNullOrEmpty(binding.objectId)) return null;
            if (!GlobalObjectId.TryParse(binding.objectId, out var objectId)) return null;
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(objectId) as GameObject;
        }

        internal void SetPreviewModel(SkillConfigSO config, GameObject model)
        {
            string guid = GetGuid(config);
            if (string.IsNullOrEmpty(guid)) return;
            var binding = previewBindings.Find(x => x.configGuid == guid);
            if (binding == null)
            {
                binding = new PreviewBinding { configGuid = guid };
                previewBindings.Add(binding);
            }
            binding.objectId = model == null ? string.Empty : GlobalObjectId.GetGlobalObjectIdSlow(model).ToString();
            Save(true);
        }

        static string GetGuid(SkillConfigSO config)
        {
            if (config == null) return string.Empty;
            string path = AssetDatabase.GetAssetPath(config);
            return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }
    }
}
