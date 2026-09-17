using System;
using Lingkyn.Inventory.Core;
using Lingkyn.Inventory.Presentation;
using Lingkyn.Inventory.UGUI;
using Lingkyn.Inventory.XR.UGUI;

namespace XRFoundry.ReferenceSystem.Bindings
{
    /// <summary>
    /// Consumer-owned composition seam between Inventory presentation and the
    /// selected UGUI world-space surface. Domain state remains owned by the
    /// supplied InventoryAggregate; this adapter owns only presentation wiring
    /// and its subscription lifetime.
    /// </summary>
    public sealed class InventoryUguiXrSurfaceAdapter : IDisposable
    {
        private readonly InventoryGridView _grid;
        private InventoryPresenter _presenter;

        public InventoryUguiXrSurfaceAdapter(
            InventoryAggregate inventory,
            InventoryWorldSpaceSurface surface)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            Surface = surface ?? throw new ArgumentNullException(nameof(surface));

            InventoryXrSceneValidator.ValidateSurface(surface).ThrowIfInvalid();

            Shell = surface.Shell;
            if (Shell.Panel == null)
            {
                throw new InvalidOperationException(
                    "InventoryUguiXrSurfaceAdapter requires the surface InventoryShell panel binding.");
            }
            RequireSurfaceOwnership(Shell.Panel.transform, surface.transform, "panel");

            _grid = Shell.Panel.Grid;
            if (_grid == null)
            {
                throw new InvalidOperationException(
                    "InventoryUguiXrSurfaceAdapter requires the surface InventoryGrid binding.");
            }
            RequireSurfaceOwnership(_grid.transform, surface.transform, "grid");

            var contentRoot = _grid.ContentRoot;
            if (contentRoot == null)
            {
                throw new InvalidOperationException(
                    "InventoryUguiXrSurfaceAdapter requires the InventoryGrid content root binding.");
            }
            RequireSurfaceOwnership(contentRoot, surface.transform, "content root");

            var slotTemplate = _grid.SlotTemplate;
            if (slotTemplate == null)
            {
                throw new InvalidOperationException(
                    "InventoryUguiXrSurfaceAdapter requires the InventoryGrid slot template binding.");
            }
            RequireSurfaceOwnership(slotTemplate.transform, surface.transform, "slot template");

            if (!slotTemplate.transform.IsChildOf(contentRoot))
            {
                throw new InvalidOperationException(
                    "InventoryUguiXrSurfaceAdapter requires the InventoryGrid slot template to be a child of its content root.");
            }

            _presenter = new InventoryPresenter(inventory, Shell);
            _grid.ActivationRequested += OnActivationRequested;
            _grid.SelectionRequested += OnSelectionRequested;
        }

        public event Action<InventorySlotIntent> ActivationRequested;
        public event Action<InventorySlotIntent> SelectionRequested;

        public InventoryWorldSpaceSurface Surface { get; }
        public InventoryShellView Shell { get; }
        public bool IsDisposed => _presenter == null;

        public InventoryPresenter Presenter => _presenter ?? throw new ObjectDisposedException(
            nameof(InventoryUguiXrSurfaceAdapter));

        public InventoryViewModel Current => Presenter.Current;

        public void Refresh() => Presenter.Refresh();

        public void Dispose()
        {
            var presenter = _presenter;
            if (presenter == null) return;

            _grid.ActivationRequested -= OnActivationRequested;
            _grid.SelectionRequested -= OnSelectionRequested;
            _presenter = null;
            presenter.Dispose();
        }

        private void OnActivationRequested(InventorySlotIntent intent)
        {
            if (!Surface.InteractionEnabled) return;

            // Activation selects presentation state but deliberately leaves any
            // domain mutation to a consumer-owned typed intent handler.
            Presenter.Select(intent.Address);
            ActivationRequested?.Invoke(intent);
        }

        private void OnSelectionRequested(InventorySlotIntent intent)
        {
            if (!Surface.InteractionEnabled) return;

            Presenter.Select(intent.Address);
            SelectionRequested?.Invoke(intent);
        }

        private static void RequireSurfaceOwnership(
            UnityEngine.Transform binding,
            UnityEngine.Transform surfaceRoot,
            string bindingName)
        {
            if (!binding.IsChildOf(surfaceRoot))
            {
                throw new InvalidOperationException(
                    $"InventoryUguiXrSurfaceAdapter requires the {bindingName} binding to remain inside the surface hierarchy.");
            }
        }
    }
}
