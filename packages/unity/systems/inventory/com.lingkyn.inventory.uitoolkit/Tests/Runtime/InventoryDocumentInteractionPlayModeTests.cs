using System.Collections;
using System.Linq;
using Lingkyn.Inventory.Core;
using Lingkyn.Inventory.Presentation;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Lingkyn.Inventory.UIToolkit.Tests
{
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class InventoryDocumentInteractionPlayModeTests
    {
        [UnityTest]
        public IEnumerator InteractionGateSuppressesIntentsWithoutHidingRenderedState()
        {
#if UNITY_EDITOR
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.lingkyn.inventory.uitoolkit/Runtime/UI/InventoryDocument.uxml");
            Assert.That(asset, Is.Not.Null);
            var root = asset.CloneTree();
            var gameObject = new GameObject("InventoryDocumentView", typeof(UIDocument), typeof(InventoryDocumentView));
            try
            {
                var view = gameObject.GetComponent<InventoryDocumentView>();
                view.Bind(root);
                var slots = Enumerable.Range(0, 2).Select(index => new InventorySlotViewModel(
                    new SlotAddress(new ContainerId("pack"), index),
                    new ItemDefinitionId($"item-{index}"),
                    index + 1,
                    false,
                    true));
                view.Render(new InventoryViewModel(2, InventoryUiState.Partial, slots, "Ready"));
                var activations = 0;
                view.ActivationRequested += _ => activations++;

                view.SetInteractionEnabled(false);
                Assert.That(view.TryActivate(0), Is.False);
                Assert.That(view.SlotButtons.All(button => !button.enabledSelf), Is.True);
                Assert.That(root.Q<Label>(InventoryDocumentContract.State).text, Is.EqualTo("Partial"));

                view.SetInteractionEnabled(true);
                Assert.That(view.TryActivate(0), Is.True);
                Assert.That(activations, Is.EqualTo(1));
                Assert.That(view.SlotButtons.All(button => button.enabledSelf), Is.True);
                yield return null;
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
#else
            yield break;
#endif
        }
    }
}
