using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Lingkyn.Inventory.UGUI.Editor
{
    public static class InventoryPrefabFactory
    {
        private const string Root = "Packages/com.lingkyn.inventory.ugui/Runtime/Prefabs";

        private static readonly Color PanelColor = new Color(0.035f, 0.075f, 0.09f, 0.96f);
        private static readonly Color SectionColor = new Color(0.055f, 0.12f, 0.14f, 0.96f);
        private static readonly Color AccentColor = new Color(0.08f, 0.50f, 0.56f, 1f);
        private static readonly Color TextColor = new Color(0.88f, 0.98f, 1f, 1f);

        [MenuItem("Tools/XR Foundry/Inventory/Rebuild UGUI Prefabs")]
        public static void Rebuild()
        {
            Directory.CreateDirectory(Root);

            var item = CreateItemView();
            var slot = CreateSlot(item);
            var grid = CreateGrid(slot);
            var details = CreateDetails();
            var actions = CreateActionMenu();
            var panel = CreatePanel(grid, details, actions);
            var shell = CreateShell(panel);

            CreateVariant(slot, "InventorySlotCompact");
            CreateGallery(shell);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static GameObject CreateItemView()
        {
            var root = CreateRectObject("ItemView", new Vector2(160f, 56f));
            var view = root.AddComponent<InventoryItemView>();
            ConfigureLayout(root, 120f, 48f, 160f, 56f);

            var label = CreateText("ItemLabel", root.transform, "Empty", 22, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, 8f);
            SetObjectReference(view, "label", label);

            return SaveAndDestroy(root, "ItemView");
        }

        private static GameObject CreateSlot(GameObject itemPrefab)
        {
            var root = CreateRectObject("InventorySlot", new Vector2(180f, 72f));
            var image = root.AddComponent<Image>();
            image.color = new Color(0.10f, 0.14f, 0.18f, 1f);
            image.raycastTarget = true;

            var selectable = root.AddComponent<Button>();
            selectable.targetGraphic = image;
            selectable.transition = Selectable.Transition.None;

            ConfigureLayout(root, 140f, 64f, 180f, 72f);
            var view = root.AddComponent<InventorySlotView>();

            var itemInstance = InstantiateNested(itemPrefab, root.transform);
            Stretch((RectTransform)itemInstance.transform, 8f);
            var itemView = itemInstance.GetComponent<InventoryItemView>();

            SetObjectReference(view, "itemView", itemView);
            SetObjectReference(view, "selectionControl", selectable);
            SetObjectReference(view, "background", image);

            return SaveAndDestroy(root, "InventorySlot");
        }

        private static GameObject CreateGrid(GameObject slotPrefab)
        {
            var root = CreateRectObject("InventoryGrid", new Vector2(588f, 180f));
            var background = root.AddComponent<Image>();
            background.color = SectionColor;
            background.raycastTarget = false;
            var scroll = root.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.scrollSensitivity = 32f;
            ConfigureLayout(root, 588f, 164f, 588f, 180f);

            var viewportObject = CreateRectObject("Viewport", Vector2.zero);
            viewportObject.transform.SetParent(root.transform, false);
            var viewportRect = (RectTransform)viewportObject.transform;
            Stretch(viewportRect, 0f);
            var viewportGraphic = viewportObject.AddComponent<Image>();
            viewportGraphic.color = new Color(0f, 0f, 0f, 0.001f);
            viewportGraphic.raycastTarget = true;
            viewportObject.AddComponent<RectMask2D>();

            var contentObject = CreateRectObject("Content", new Vector2(0f, 0f));
            contentObject.transform.SetParent(viewportObject.transform, false);
            var contentRect = (RectTransform)contentObject.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            var layout = contentObject.AddComponent<GridLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.cellSize = new Vector2(180f, 72f);
            layout.spacing = new Vector2(12f, 12f);
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            var fitter = contentObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRect;
            scroll.content = contentRect;

            var view = root.AddComponent<InventoryGridView>();
            var slotInstance = InstantiateNested(slotPrefab, contentObject.transform);
            var slot = slotInstance.GetComponent<InventorySlotView>();
            SetObjectReference(view, "contentRoot", contentObject.transform);
            SetObjectReference(view, "slotTemplate", slot);

            return SaveAndDestroy(root, "InventoryGrid");
        }

        private static GameObject CreateDetails()
        {
            var root = CreateRectObject("ItemDetails", new Vector2(588f, 84f));
            var image = root.AddComponent<Image>();
            image.color = SectionColor;
            image.raycastTarget = false;
            ConfigureLayout(root, 588f, 72f, 588f, 84f);

            var view = root.AddComponent<InventoryDetailsView>();
            var label = CreateText("DetailsLabel", root.transform, "Empty", 20, TextAnchor.MiddleLeft);
            Stretch(label.rectTransform, 16f);
            SetObjectReference(view, "label", label);

            return SaveAndDestroy(root, "ItemDetails");
        }

        private static GameObject CreateActionMenu()
        {
            var root = CreateRectObject("ActionMenu", new Vector2(588f, 80f));
            var image = root.AddComponent<Image>();
            image.color = SectionColor;
            image.raycastTarget = false;
            var group = root.AddComponent<CanvasGroup>();
            var layout = root.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlHeight = true;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;
            ConfigureLayout(root, 588f, 68f, 588f, 80f);

            var buttonObject = CreateRectObject("PrimaryAction", new Vector2(220f, 56f));
            buttonObject.transform.SetParent(root.transform, false);
            var buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.color = AccentColor;
            buttonImage.raycastTarget = true;
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            ConfigureLayout(buttonObject, 180f, 48f, 220f, 56f);

            var label = CreateText("Label", buttonObject.transform, "Primary action", 20, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, 8f);

            var view = root.AddComponent<InventoryActionMenuView>();
            SetObjectReference(view, "group", group);
            SetObjectReference(view, "primaryAction", button);
            SetObjectReference(view, "label", label);

            return SaveAndDestroy(root, "ActionMenu");
        }

        private static GameObject CreatePanel(GameObject gridPrefab, GameObject detailsPrefab, GameObject actionsPrefab)
        {
            var root = CreateRectObject("InventoryPanel", new Vector2(640f, 440f));
            var image = root.AddComponent<Image>();
            image.color = PanelColor;
            image.raycastTarget = false;
            var layout = root.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(26, 26, 24, 24);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            ConfigureLayout(root, 640f, 440f, 640f, 440f);

            var gridInstance = InstantiateNested(gridPrefab, root.transform);
            var detailsInstance = InstantiateNested(detailsPrefab, root.transform);
            var actionsInstance = InstantiateNested(actionsPrefab, root.transform);

            var view = root.AddComponent<InventoryPanelView>();
            SetObjectReference(view, "grid", gridInstance.GetComponent<InventoryGridView>());
            SetObjectReference(view, "details", detailsInstance.GetComponent<InventoryDetailsView>());
            SetObjectReference(view, "actionMenu", actionsInstance.GetComponent<InventoryActionMenuView>());

            return SaveAndDestroy(root, "InventoryPanel");
        }

        private static GameObject CreateShell(GameObject panelPrefab)
        {
            var root = CreateRectObject("InventoryShell", new Vector2(640f, 440f));
            ConfigureLayout(root, 640f, 440f, 640f, 440f);
            var panelInstance = InstantiateNested(panelPrefab, root.transform);
            Stretch((RectTransform)panelInstance.transform, 0f);

            var view = root.AddComponent<InventoryShellView>();
            SetObjectReference(view, "panel", panelInstance.GetComponent<InventoryPanelView>());

            return SaveAndDestroy(root, "InventoryShell");
        }

        private static void CreateGallery(GameObject shellPrefab)
        {
            var root = CreateRectObject("InventoryStateGallery", new Vector2(640f, 440f));
            var shellInstance = InstantiateNested(shellPrefab, root.transform);
            Stretch((RectTransform)shellInstance.transform, 0f);
            var gallery = root.AddComponent<InventoryStateGallery>();
            SetObjectReference(gallery, "shell", shellInstance.GetComponent<InventoryShellView>());
            SaveAndDestroy(root, "InventoryStateGallery");
        }

        private static void CreateVariant(GameObject source, string name)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.name = name;
            var layout = instance.GetComponent<LayoutElement>();
            layout.minWidth = 112f;
            layout.minHeight = 52f;
            layout.preferredWidth = 144f;
            layout.preferredHeight = 60f;
            ((RectTransform)instance.transform).sizeDelta = new Vector2(144f, 60f);
            PrefabUtility.SaveAsPrefabAsset(instance, $"{Root}/{name}.prefab");
            UnityEngine.Object.DestroyImmediate(instance);
        }

        private static GameObject CreateRectObject(string name, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform));
            ((RectTransform)root.transform).sizeDelta = size;
            return root;
        }

        private static Text CreateText(string name, Transform parent, string value, int fontSize, TextAnchor alignment)
        {
            var labelObject = CreateRectObject(name, Vector2.zero);
            labelObject.transform.SetParent(parent, false);
            var label = labelObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = value;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = TextColor;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private static void ConfigureLayout(GameObject target, float minWidth, float minHeight, float preferredWidth, float preferredHeight)
        {
            var element = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            element.minWidth = minWidth;
            element.minHeight = minHeight;
            element.preferredWidth = preferredWidth;
            element.preferredHeight = preferredHeight;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static GameObject InstantiateNested(GameObject prefab, Transform parent)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent, false);
            return instance;
        }

        private static GameObject SaveAndDestroy(GameObject root, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/{name}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (value == null) throw new ArgumentNullException(nameof(value), $"{target.name}.{propertyName} cannot be null.");

            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException($"Serialized property '{propertyName}' was not found on {target.GetType().Name}.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
