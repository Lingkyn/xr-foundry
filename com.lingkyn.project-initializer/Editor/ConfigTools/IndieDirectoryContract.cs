namespace Lingkyn.Unity.ProjectInitializer.Editor.ConfigTools
{
    /// <summary>
    /// Indie profile directory contract from unity-project-initializer / initializer-flow.md.
    /// XR Prefabs/XR is a project extension beyond the base spec.
    /// </summary>
    public static class IndieDirectoryContract
    {
        public const string ProjectRoot = "Assets/_Project";
        public const string SettingsRoot = ProjectRoot + "/Settings";
        public const string ActivationMarker = SettingsRoot + "/LingkynProjectInitializer.marker";
        public const string BootScene = ProjectRoot + "/Scenes/Boot.unity";
        public const string MainMenuScene = ProjectRoot + "/Scenes/MainMenu.unity";
        public const string Level01Scene = ProjectRoot + "/Scenes/Level_01.unity";
        public const string SandboxScene = ProjectRoot + "/Scenes/Sandbox.unity";

        public static readonly string[] BaselineScenes =
        {
            BootScene,
            MainMenuScene,
            Level01Scene,
            SandboxScene,
        };

        public static readonly string[] RequiredFolders =
        {
            $"{ProjectRoot}/Art/Animations",
            $"{ProjectRoot}/Art/Materials",
            $"{ProjectRoot}/Art/Models",
            $"{ProjectRoot}/Art/Shaders",
            $"{ProjectRoot}/Art/Sprites",
            $"{ProjectRoot}/Art/Textures",
            $"{ProjectRoot}/Art/VFX",
            $"{ProjectRoot}/Audio/BGM",
            $"{ProjectRoot}/Audio/SFX",
            $"{ProjectRoot}/Audio/UI",
            $"{ProjectRoot}/Data/Config",
            $"{ProjectRoot}/Data/Localization",
            $"{ProjectRoot}/Data/ScriptableObjects",
            $"{ProjectRoot}/Data/ScriptableObjects/Definitions",
            $"{ProjectRoot}/Data/ScriptableObjects/EventChannels",
            $"{ProjectRoot}/Data/ScriptableObjects/RuntimeSets",
            $"{ProjectRoot}/Prefabs/Characters",
            $"{ProjectRoot}/Prefabs/Props",
            $"{ProjectRoot}/Prefabs/Shared",
            $"{ProjectRoot}/Prefabs/Systems",
            $"{ProjectRoot}/Prefabs/UI",
            $"{ProjectRoot}/Prefabs/VFX",
            $"{ProjectRoot}/Prefabs/XR",
            $"{ProjectRoot}/Scenes",
            $"{ProjectRoot}/Settings",
            $"{ProjectRoot}/Scripts/App/Bootstrap",
            $"{ProjectRoot}/Scripts/App/Composition",
            $"{ProjectRoot}/Scripts/App/Lifecycle",
            $"{ProjectRoot}/Scripts/Presentation/UI",
            $"{ProjectRoot}/Scripts/Presentation/Camera",
            $"{ProjectRoot}/Scripts/Presentation/Views",
            $"{ProjectRoot}/Scripts/Presentation/ViewBinding",
            $"{ProjectRoot}/Scripts/Presentation/Animation",
            $"{ProjectRoot}/Scripts/Interaction/Input",
            $"{ProjectRoot}/Scripts/Interaction/Targeting",
            $"{ProjectRoot}/Scripts/Interaction/HitDetection",
            $"{ProjectRoot}/Scripts/Interaction/Interactables",
            $"{ProjectRoot}/Scripts/Interaction/XR",
            $"{ProjectRoot}/Scripts/Domain/Model",
            $"{ProjectRoot}/Scripts/Domain/Rules",
            $"{ProjectRoot}/Scripts/Domain/ValueObjects",
            $"{ProjectRoot}/Scripts/Domain/Interfaces",
            $"{ProjectRoot}/Scripts/Gameplay/_FeatureTemplate",
            $"{ProjectRoot}/Scripts/Flow/Contracts",
            $"{ProjectRoot}/Scripts/Flow/Game",
            $"{ProjectRoot}/Scripts/Flow/Scene",
            $"{ProjectRoot}/Scripts/Flow/UI",
            $"{ProjectRoot}/Scripts/Flow/Loading",
            $"{ProjectRoot}/Scripts/Services/Events",
            $"{ProjectRoot}/Scripts/Services/AssetLoading",
            $"{ProjectRoot}/Scripts/Services/Audio",
            $"{ProjectRoot}/Scripts/Services/Persistence",
            $"{ProjectRoot}/Scripts/Services/Localization",
            $"{ProjectRoot}/Scripts/Services/Logging",
            $"{ProjectRoot}/Scripts/Services/Pooling",
            $"{ProjectRoot}/Scripts/Services/Time",
            $"{ProjectRoot}/Scripts/Services/Loading",
            $"{ProjectRoot}/Scripts/Services/Diagnostics",
            $"{ProjectRoot}/Scripts/Shared/Constants",
            $"{ProjectRoot}/Scripts/Shared/Extensions",
            $"{ProjectRoot}/Scripts/Shared/Utilities",
            $"{ProjectRoot}/Scripts/Shared/Types",
            $"{ProjectRoot}/Scripts/Editor/Validation",
            $"{ProjectRoot}/Scripts/Editor/SceneSetup",
            $"{ProjectRoot}/Scripts/Editor/ConfigTools",
            $"{ProjectRoot}/Scripts/Editor/Build",
            $"{ProjectRoot}/Scripts/Editor/Menu",
            $"{ProjectRoot}/Tests/EditMode",
            $"{ProjectRoot}/Tests/PlayMode",
        };
    }
}
