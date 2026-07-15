using System;
using System.Linq;
using Lingkyn.Inventory.UIToolkit;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Lingkyn.Inventory.XR.UIToolkit.Tests
{
    public sealed class InventoryUIToolkitWorldSpaceTests
    {
        private const string ProfilePath =
            "Packages/com.lingkyn.inventory.xr.uitoolkit/Runtime/Profiles/InventoryUIToolkitWorldSpaceDefault.asset";
        private const string PanelPath =
            "Packages/com.lingkyn.inventory.xr.uitoolkit/Runtime/Profiles/InventoryUIToolkitWorldSpacePanel.asset";
        private const string DocumentPath =
            "Packages/com.lingkyn.inventory.uitoolkit/Runtime/UI/InventoryDocument.uxml";

        [Test]
        public void ShippedAssetsUseWorldSpaceKeepExistingColliderAndValidatedPhysicalScale()
        {
            var profile = AssetDatabase.LoadAssetAtPath<InventoryUIToolkitWorldSpaceProfile>(ProfilePath);
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            Assert.That(profile, Is.Not.Null);
            Assert.That(panel, Is.Not.Null);
            Assert.That(profile.PanelSettings, Is.SameAs(panel));
            Assert.That(panel.renderMode, Is.EqualTo(PanelRenderMode.WorldSpace));
            Assert.That(() => profile.Validate(), Throws.Nothing);
            Assert.That(profile.PhysicalSizeMeters.x, Is.EqualTo(0.64f).Within(0.0001f));
            Assert.That(profile.PhysicalSizeMeters.y, Is.EqualTo(0.44f).Within(0.0001f));
            Assert.That(profile.MaxInteractionDistanceMeters, Is.GreaterThanOrEqualTo(profile.DefaultDistanceMeters));

            var serializedPanel = new SerializedObject(panel);
            Assert.That(serializedPanel.FindProperty("m_ColliderUpdateMode").intValue, Is.EqualTo(1),
                "Shipped PanelSettings must keep the explicit collider rather than generating a duplicate.");
            Assert.That(serializedPanel.FindProperty("m_ColliderIsTrigger").boolValue, Is.True);
            Assert.That(serializedPanel.FindProperty("m_PixelsPerUnit").floatValue, Is.EqualTo(100f));
        }

        [Test]
        public void SurfaceStartsClosedAndOpensOnlyAfterSceneContractPasses()
        {
            var surface = CreateSurface(out var surfaceObject);
            GameObject managerObject = null;
            GameObject interactionManagerObject = null;
            GameObject rayObject = null;
            GameObject eventSystemObject = null;
            GameObject panelInputObject = null;
            try
            {
                surfaceObject.SetActive(true);
                surface.InventoryView.Bind(surface.Document);
                Assert.That(surface.InteractionEnabled, Is.False);
                Assert.That(surface.Revalidate().Has(InventoryUIToolkitIssueCode.MissingXrUiToolkitManager), Is.True);
                Assert.That(surface.InteractionEnabled, Is.False);

                managerObject = new GameObject("XR UI Toolkit Manager", typeof(XRUIToolkitManager));
                Assert.That(surface.Revalidate().Has(InventoryUIToolkitIssueCode.MissingUiInteractor), Is.True);
                Assert.That(surface.InteractionEnabled, Is.False);

                interactionManagerObject = new GameObject("XR Interaction Manager", typeof(XRInteractionManager));
                rayObject = new GameObject("XR Ray Interactor");
                rayObject.transform.position = new Vector3(0f, 0f, -2f);
                var ray = rayObject.AddComponent<XRRayInteractor>();
                ray.enableUIInteraction = false;
                Assert.That(surface.Revalidate().Has(InventoryUIToolkitIssueCode.UiInteractionDisabled), Is.True);
                ray.enableUIInteraction = true;
                var validReport = surface.Revalidate();
                Assert.That(
                    validReport.IsValid,
                    Is.True,
                    string.Join(" | ", validReport.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
                Assert.That(surface.InteractionEnabled, Is.True);

                eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
                Assert.That(surface.Revalidate().Has(InventoryUIToolkitIssueCode.MissingPanelInputConfiguration), Is.True);
                Assert.That(surface.InteractionEnabled, Is.False);

                panelInputObject = new GameObject("Panel Input Configuration", typeof(PanelInputConfiguration));
                var configuration = panelInputObject.GetComponent<PanelInputConfiguration>();
                configuration.panelInputRedirection = PanelInputConfiguration.PanelInputRedirection.Never;
                configuration.processWorldSpaceInput = true;
                configuration.interactionLayers = 0;
                configuration.maxInteractionDistance = surface.Profile.MaxInteractionDistanceMeters;
                Assert.That(surface.Revalidate().Has(InventoryUIToolkitIssueCode.InteractionLayerMismatch), Is.True);

                configuration.interactionLayers = 1 << surface.gameObject.layer;
                configuration.maxInteractionDistance = float.NaN;
                Assert.That(surface.Revalidate().Has(InventoryUIToolkitIssueCode.InvalidMaxInteractionDistance), Is.True);

                configuration.maxInteractionDistance = 0.25f;
                Assert.That(surface.Revalidate().Has(InventoryUIToolkitIssueCode.PanelInputCannotReachSurface), Is.True);

                configuration.maxInteractionDistance = surface.Profile.MaxInteractionDistanceMeters;
                Assert.That(surface.Revalidate().IsValid, Is.True);

                surface.transform.SetParent(new GameObject("Head", typeof(Camera)).transform, true);
                Assert.That(surface.Revalidate().Has(InventoryUIToolkitIssueCode.HeadLockedSurface), Is.True);
                Assert.That(surface.InteractionEnabled, Is.False);
                UnityEngine.Object.DestroyImmediate(surface.transform.parent.gameObject);
            }
            finally
            {
                DestroyImmediate(
                    panelInputObject,
                    eventSystemObject,
                    rayObject,
                    interactionManagerObject,
                    managerObject,
                    surfaceObject);
            }
        }

        [Test]
        public void SurfaceRejectsCrossObjectBindingsAndColliderDrift()
        {
            var surface = CreateSurface(out var surfaceObject);
            var externalObject = new GameObject("External UI Toolkit Parts");
            try
            {
                var externalDocument = externalObject.AddComponent<UIDocument>();
                externalDocument.visualTreeAsset = surface.Document.visualTreeAsset;
                var externalView = externalObject.AddComponent<InventoryDocumentView>();
                var externalCollider = externalObject.AddComponent<BoxCollider>();
                surface.Configure(surface.Profile, externalDocument, externalView, externalCollider);
                surface.ApplyProfile();

                var report = InventoryUIToolkitSceneValidator.ValidateSurface(surface);
                Assert.That(report.Has(InventoryUIToolkitIssueCode.DocumentOutsideSurface), Is.True);
                Assert.That(report.Has(InventoryUIToolkitIssueCode.InventoryViewOutsideSurface), Is.True);
                Assert.That(report.Has(InventoryUIToolkitIssueCode.ColliderOutsideSurface), Is.True);

                surface.Configure(surface.Profile);
                surface.ApplyProfile();
                surface.WorldSpaceCollider.isTrigger = false;
                Assert.That(InventoryUIToolkitSceneValidator.ValidateSurface(surface)
                    .Has(InventoryUIToolkitIssueCode.ColliderNotTrigger), Is.True);

                surface.ApplyProfile();
                surface.WorldSpaceCollider.center = new Vector3(0.01f, 0f, 0f);
                Assert.That(InventoryUIToolkitSceneValidator.ValidateSurface(surface)
                    .Has(InventoryUIToolkitIssueCode.ColliderCenterMismatch), Is.True);

                surface.ApplyProfile();
                surface.WorldSpaceCollider.size += new Vector3(0.01f, 0f, 0f);
                Assert.That(InventoryUIToolkitSceneValidator.ValidateSurface(surface)
                    .Has(InventoryUIToolkitIssueCode.ColliderSizeMismatch), Is.True);

                surface.ApplyProfile();
                surface.WorldSpaceCollider.size = new Vector3(0f, 1f, 1f);
                Assert.That(InventoryUIToolkitSceneValidator.ValidateSurface(surface)
                    .Has(InventoryUIToolkitIssueCode.InvalidColliderGeometry), Is.True);
            }
            finally
            {
                DestroyImmediate(externalObject, surfaceObject);
            }
        }

        [Test]
        public void ProfileRejectsPanelSettingsThatCanRewriteOrSolidifyCollider()
        {
            var profile = AssetDatabase.LoadAssetAtPath<InventoryUIToolkitWorldSpaceProfile>(ProfilePath);
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            var profileCopy = UnityEngine.Object.Instantiate(profile);
            var panelCopy = UnityEngine.Object.Instantiate(panel);
            try
            {
                var serializedProfile = new SerializedObject(profileCopy);
                serializedProfile.FindProperty("panelSettings").objectReferenceValue = panelCopy;
                serializedProfile.ApplyModifiedPropertiesWithoutUndo();

                var serializedPanel = new SerializedObject(panelCopy);
                serializedPanel.FindProperty("m_ColliderUpdateMode").intValue = 0;
                serializedPanel.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(() => profileCopy.Validate(), Throws.InvalidOperationException);

                serializedPanel.FindProperty("m_ColliderUpdateMode").intValue = 1;
                serializedPanel.FindProperty("m_ColliderIsTrigger").boolValue = false;
                serializedPanel.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(() => profileCopy.Validate(), Throws.InvalidOperationException);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(panelCopy);
                UnityEngine.Object.DestroyImmediate(profileCopy);
            }
        }

        [Test]
        public void ProfileAppliesFixedDocumentSizeAndMatchingExplicitCollider()
        {
            var surface = CreateSurface(out var surfaceObject);
            try
            {
                surface.ApplyProfile();
                Assert.That(surface.Document.panelSettings.renderMode, Is.EqualTo(PanelRenderMode.WorldSpace));
                Assert.That(surface.Document.worldSpaceSizeMode, Is.EqualTo(UIDocument.WorldSpaceSizeMode.Fixed));
                Assert.That(surface.Document.worldSpaceSize, Is.EqualTo(surface.Profile.ReferenceResolution));
                Assert.That(surface.transform.localScale, Is.EqualTo(Vector3.one * surface.Profile.LocalScale));
                Assert.That(surface.WorldSpaceCollider.isTrigger, Is.True);
                Assert.That(surface.WorldSpaceCollider.size.x * surface.Profile.LocalScale,
                    Is.EqualTo(surface.Profile.PhysicalSizeMeters.x).Within(0.0001f));
                Assert.That(surface.WorldSpaceCollider.size.y * surface.Profile.LocalScale,
                    Is.EqualTo(surface.Profile.PhysicalSizeMeters.y).Within(0.0001f));
                Assert.That(InventoryUIToolkitSceneValidator.IsSupportedUnityVersion("6000.3.8f1"), Is.True);
                Assert.That(InventoryUIToolkitSceneValidator.IsSupportedUnityVersion("6000.3.7f1"), Is.False);
            }
            finally
            {
                DestroyImmediate(surfaceObject);
            }
        }

        private static InventoryUIToolkitWorldSpaceSurface CreateSurface(out GameObject gameObject)
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(DocumentPath);
            var profile = AssetDatabase.LoadAssetAtPath<InventoryUIToolkitWorldSpaceProfile>(ProfilePath);
            Assert.That(tree, Is.Not.Null);
            Assert.That(profile, Is.Not.Null);

            gameObject = new GameObject("Inventory UI Toolkit World Space");
            gameObject.SetActive(false);
            var document = gameObject.AddComponent<UIDocument>();
            document.visualTreeAsset = tree;
            var view = gameObject.AddComponent<InventoryDocumentView>();
            var collider = gameObject.AddComponent<BoxCollider>();
            var surface = gameObject.AddComponent<InventoryUIToolkitWorldSpaceSurface>();
            surface.Configure(profile, document, view, collider);
            surface.ApplyProfile();
            return surface;
        }

        private static void DestroyImmediate(params GameObject[] gameObjects)
        {
            foreach (var gameObject in gameObjects)
            {
                if (gameObject != null) UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }
}
