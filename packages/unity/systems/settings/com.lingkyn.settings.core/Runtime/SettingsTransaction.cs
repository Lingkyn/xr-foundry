using System;
using System.Collections.Generic;

namespace Lingkyn.Settings.Core
{
    public enum SettingsTransactionCommandKind
    {
        Set = 0,
        ResetScope = 1,
        ApplyProfile = 2,
    }

    public readonly struct SettingsTransactionCommand
    {
        public SettingsTransactionCommand(SettingsTransactionCommandKind kind, ScopedSettingKey scopedKey, SettingValue value, SettingScope resetScope, SettingsProfile profile)
        {
            Kind = kind;
            ScopedKey = scopedKey;
            Value = value;
            ResetScope = resetScope;
            Profile = profile;
        }

        public SettingsTransactionCommandKind Kind { get; }
        public ScopedSettingKey ScopedKey { get; }
        public SettingValue Value { get; }
        public SettingScope ResetScope { get; }
        public SettingsProfile Profile { get; }

        public static SettingsTransactionCommand Set(ScopedSettingKey scopedKey, SettingValue value)
            => new SettingsTransactionCommand(SettingsTransactionCommandKind.Set, scopedKey, value, default, null);

        public static SettingsTransactionCommand ResetScope(SettingScope scope)
            => new SettingsTransactionCommand(SettingsTransactionCommandKind.ResetScope, default, default, scope, null);

        public static SettingsTransactionCommand ApplyProfile(SettingsProfile profile)
            => new SettingsTransactionCommand(SettingsTransactionCommandKind.ApplyProfile, default, default, default, profile);
    }

    public sealed class SettingsTransaction
    {
        private readonly List<SettingsTransactionCommand> _commands = new List<SettingsTransactionCommand>();

        internal SettingsTransaction(long baseRevision) => BaseRevision = baseRevision;

        public long BaseRevision { get; }
        public IReadOnlyList<SettingsTransactionCommand> Commands => _commands;

        public void StageSet(ScopedSettingKey scopedKey, SettingValue value)
            => _commands.Add(SettingsTransactionCommand.Set(scopedKey, value));

        public void StageReset(SettingScope scope)
            => _commands.Add(SettingsTransactionCommand.ResetScope(scope));

        public void StageProfile(SettingsProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            _commands.Add(SettingsTransactionCommand.ApplyProfile(profile));
        }
    }
}
