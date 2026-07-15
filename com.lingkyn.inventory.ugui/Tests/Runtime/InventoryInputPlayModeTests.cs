using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Lingkyn.Inventory.UGUI.Tests
{
    public sealed class InventoryInputPlayModeTests
    {
        [UnityTest]
        public IEnumerator PointerAndKeyboardSubmitActivateTheSameSlotWithoutXr()
        {
            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            var slotObject = new GameObject("Slot", typeof(RectTransform), typeof(InventorySlotView));
            var slot = slotObject.GetComponent<InventorySlotView>();
            var activations = 0;
            slot.Activated += _ => activations++;

            ExecuteEvents.Execute(slotObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            ExecuteEvents.Execute(slotObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            yield return null;

            Assert.That(activations, Is.EqualTo(2));
            Assert.That(typeof(InventorySlotView).Assembly.GetReferencedAssemblies()
                .Any(reference => reference.Name.StartsWith("Unity.XR", System.StringComparison.Ordinal)), Is.False);
            Object.Destroy(slotObject);
            Object.Destroy(eventSystemObject);
        }
    }
}
