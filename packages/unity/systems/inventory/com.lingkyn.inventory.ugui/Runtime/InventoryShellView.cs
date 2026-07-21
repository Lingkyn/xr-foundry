using Lingkyn.Inventory.Presentation;
using UnityEngine;

namespace Lingkyn.Inventory.UGUI
{
    public sealed class InventoryShellView : MonoBehaviour, IInventoryView
    {
        [SerializeField] private InventoryPanelView panel;
        [Tooltip("Optional visual skin. When null, a translucent spatial-glass default is applied at runtime.")]
        [SerializeField] private InventorySkin skin;

        private InventorySkin _defaultSkin;
        private bool _skinApplied;

        public InventoryPanelView Panel => panel;
        public InventorySkin Skin => skin;
        public InventoryViewModel LastModel { get; private set; }

        private void Awake() => EnsureSkinApplied();

        public void Render(InventoryViewModel model)
        {
            if (model == null) throw new System.ArgumentNullException(nameof(model));
            LastModel = model;
            EnsureSkinApplied();
            if (panel != null) panel.Render(model);
        }

        /// <summary>Inject a consumer skin. Pass null to revert to the built-in default.</summary>
        public void ApplySkin(InventorySkin value)
        {
            skin = value;
            _skinApplied = false;
            EnsureSkinApplied();
        }

        private void EnsureSkinApplied()
        {
            if (_skinApplied || panel == null) return;
            var resolved = skin != null ? skin : (_defaultSkin ??= InventorySkin.CreateDefault());
            panel.ApplySkin(resolved);
            _skinApplied = true;
        }
    }
}
