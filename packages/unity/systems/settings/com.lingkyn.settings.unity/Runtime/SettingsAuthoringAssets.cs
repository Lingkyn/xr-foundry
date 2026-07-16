using System;
using UnityEngine;

namespace Lingkyn.Settings.Unity
{
    [Serializable]
    public sealed class AccessibilityMetadataRecord
    {
        public string category;
        public string featureId;
        public string titleKey;
        public string descriptionKey;
        public bool previewSupported;
        public bool availableBeforeGameplay;
        public string documentationKey;
    }

    [Serializable]
    public sealed class NumericConstraintRecord
    {
        public bool enabled;
        public double minInclusive;
        public double maxInclusive;
        public double step;
        public bool hasStep;
    }

    [Serializable]
    public sealed class StringConstraintRecord
    {
        public bool enabled;
        public int maxLength;
    }

    [Serializable]
    public sealed class OptionEntryRecord
    {
        public string optionId;
    }

    [Serializable]
    public sealed class OptionConstraintRecord
    {
        public OptionEntryRecord[] options = Array.Empty<OptionEntryRecord>();
    }

    [CreateAssetMenu(fileName = "SettingDefinition", menuName = "Lingkyn/Settings/Setting Definition")]
    public sealed class SettingDefinitionAsset : ScriptableObject
    {
        public string key;
        public SettingValueKindRecord kind;
        public bool defaultBoolean;
        public long defaultInteger;
        public double defaultFloat;
        public string defaultString;
        public string defaultOptionId;
        public SettingScopeRecord defaultScope = SettingScopeRecord.User;
        public int applicationOrder;
        public bool requiresRestart;
        public NumericConstraintRecord numericConstraint = new NumericConstraintRecord();
        public StringConstraintRecord stringConstraint = new StringConstraintRecord();
        public OptionConstraintRecord optionConstraint = new OptionConstraintRecord();
        public AccessibilityMetadataRecord accessibility = new AccessibilityMetadataRecord();
    }

    public enum SettingValueKindRecord
    {
        Boolean = 0,
        Integer = 1,
        Float = 2,
        String = 3,
        Option = 4,
    }

    public enum SettingScopeRecord
    {
        Global = 0,
        User = 1,
        Profile = 2,
        Session = 3,
    }

    [CreateAssetMenu(fileName = "SettingsCatalog", menuName = "Lingkyn/Settings/Settings Catalog")]
    public sealed class SettingsCatalogAsset : ScriptableObject
    {
        public SettingDefinitionAsset[] definitions = Array.Empty<SettingDefinitionAsset>();
    }

    [Serializable]
    public sealed class ProfileOverrideRecord
    {
        public SettingDefinitionAsset definition;
        public SettingValueKindRecord kind;
        public bool booleanValue;
        public long integerValue;
        public double floatValue;
        public string stringValue;
        public string optionId;
    }

    [Serializable]
    public sealed class ProfileLayerRecord
    {
        public string layerId;
        public ProfileOverrideRecord[] overrides = Array.Empty<ProfileOverrideRecord>();
    }

    [CreateAssetMenu(fileName = "SettingsProfile", menuName = "Lingkyn/Settings/Settings Profile")]
    public sealed class SettingsProfileAsset : ScriptableObject
    {
        public string profileId;
        public ProfileLayerRecord[] layers = Array.Empty<ProfileLayerRecord>();
    }
}
