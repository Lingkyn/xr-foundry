using UnityEditor;
using UnityEngine;

namespace Lingkyn.Unity.ProjectInitializer.Editor.Settings
{
    public static class ProjectInputSystemSetup
    {
        const string ActiveInputHandlerProperty = "activeInputHandler";

        public static void ApplyWhenEnabled(bool useInputSystem)
        {
            if (!useInputSystem) return;

            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (asset == null || asset.Length == 0) return;

            var settings = new SerializedObject(asset[0]);
            var handler = settings.FindProperty(ActiveInputHandlerProperty);
            if (handler == null) return;

            // 1 = Input System Package (New)
            if (handler.intValue == 1) return;

            handler.intValue = 1;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("project_initializer: activeInputHandler set to Input System Package.");
        }
    }
}
