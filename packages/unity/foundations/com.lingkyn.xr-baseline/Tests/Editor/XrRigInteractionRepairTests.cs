using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using Lingkyn.Unity.XrBaseline.Config;
using Lingkyn.Unity.XrBaseline.Editor.SceneSetup;
using Object = UnityEngine.Object;

namespace Lingkyn.Unity.XrBaseline.Tests
{
    /// <summary>
    /// Covers Issue #21: the repair must configure the CurveInteractionCaster that an XRI 3.x
    /// NearFarInteractor actually references, stay idempotent, never lower a longer value, and
    /// report a missing caster instead of claiming success.
    /// </summary>
    public sealed class XrRigInteractionRepairTests
    {
        [Test]
        public void ShortCurveCasterIsRaisedToConfiguredDistanceAndNeverLowered()
        {
            var rig = CreateInactiveRig();
            var config = ScriptableObject.CreateInstance<VrBaselineConfig>();
            try
            {
                config.farRayCastDistance = 30f;
                var interactor = rig.AddComponent<NearFarInteractor>();
                var caster = rig.AddComponent<CurveInteractionCaster>();
                caster.castDistance = 5f;
                AssignFarCaster(interactor, caster);

                var result = XrRigInteractionRepair.Repair(rig, config);

                Assert.That(result.Diagnostics, Is.Empty, string.Join("\n", result.Diagnostics));
                Assert.That(result.ConfiguredFarCasters, Has.Count.EqualTo(1));
                Assert.That(caster.castDistance, Is.EqualTo(30f).Within(0.001f));

                caster.castDistance = 45f;
                var second = XrRigInteractionRepair.Repair(rig, config);

                Assert.That(second.Diagnostics, Is.Empty);
                Assert.That(caster.castDistance, Is.EqualTo(45f).Within(0.001f), "A longer authored value must be preserved.");
            }
            finally
            {
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(rig);
            }
        }

        [Test]
        public void MissingFarCasterIsReportedInsteadOfSilentSuccess()
        {
            var rig = CreateInactiveRig();
            var config = ScriptableObject.CreateInstance<VrBaselineConfig>();
            try
            {
                var interactor = rig.AddComponent<NearFarInteractor>();
                AssignFarCaster(interactor, null);

                var result = XrRigInteractionRepair.Repair(rig, config);

                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.ConfiguredFarCasters, Is.Empty);
                Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
                Assert.That(result.Diagnostics[0], Does.Contain("NearFarInteractor"));
            }
            finally
            {
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(rig);
            }
        }

        [Test]
        public void RigWithoutInteractorsReportsNothingAndClaimsNothing()
        {
            var rig = CreateInactiveRig();
            try
            {
                var result = XrRigInteractionRepair.Repair(rig);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(result.ConfiguredFarCasters, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }
        }

        static GameObject CreateInactiveRig()
        {
            var rig = new GameObject("XROriginRig_Test");
            rig.SetActive(false);
            return rig;
        }

        /// <summary>
        /// Sets the interactor's serialized far caster reference without naming the field, so the
        /// test tracks the same contract the repair relies on: the reference whose name contains
        /// "FarCaster". Fails the test if this XRI revision exposes no such reference.
        /// </summary>
        static void AssignFarCaster(NearFarInteractor interactor, Object caster)
        {
            var serialized = new SerializedObject(interactor);
            var iterator = serialized.GetIterator();
            SerializedProperty target = null;
            while (iterator.Next(true))
            {
                if (iterator.propertyType == SerializedPropertyType.ObjectReference &&
                    iterator.name.IndexOf("FarCaster", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    target = iterator.Copy();
                    break;
                }
            }

            Assert.That(target, Is.Not.Null, "NearFarInteractor exposes no serialized far caster reference in this XRI revision.");
            target.objectReferenceValue = caster;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
