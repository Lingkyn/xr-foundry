using System;
using System.Reflection;
using UnityEngine;

namespace Lingkyn.Unity.XrBaseline.Interaction
{
    /// <summary>
    /// Greybox hover glow for grabbable props. Polls XRGrabInteractable.isHovered (no XRI assembly reference).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class GrabbableHoverVisual : MonoBehaviour
    {
        const string GrabInteractableTypeName =
            "UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable, Unity.XR.Interaction.Toolkit";

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] MeshRenderer _renderer;
        [SerializeField] Color _idleBaseColor = new(0.15f, 0.38f, 0.78f, 1f);
        [ColorUsage(true, true)]
        [SerializeField] Color _hoverEmission = new(3.5f, 1.2f, 0.15f, 1f);
        [SerializeField] Color _hoverBaseColor = new(0.45f, 0.72f, 1f, 1f);

        MaterialPropertyBlock _propertyBlock;
        Component _grabComponent;
        PropertyInfo _isHoveredProperty;
        bool _isHovered;
        bool _reportedUnresolved;

        void Reset()
        {
            _renderer = GetComponent<MeshRenderer>();
        }

        void Awake()
        {
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
            CacheGrabComponent();
            ApplyVisual(false);
        }

        void OnEnable()
        {
            CacheGrabComponent();
        }

        void LateUpdate()
        {
            if (_grabComponent == null || _isHoveredProperty == null)
            {
                CacheGrabComponent();
                if (_grabComponent == null || _isHoveredProperty == null) return;
            }

            var hovered = (bool)_isHoveredProperty.GetValue(_grabComponent);
            if (hovered == _isHovered) return;

            _isHovered = hovered;
            ApplyVisual(hovered);
        }

        void CacheGrabComponent()
        {
            _grabComponent = null;
            _isHoveredProperty = null;

            var grabType = Type.GetType(GrabInteractableTypeName);
            if (grabType == null)
            {
                ReportUnresolved("XRGrabInteractable type is not loaded; is XR Interaction Toolkit installed?");
                return;
            }

            _grabComponent = GetComponent(grabType);
            if (_grabComponent == null)
            {
                ReportUnresolved("no XRGrabInteractable on this GameObject; the hover visual will stay idle.");
                return;
            }

            _isHoveredProperty = grabType.GetProperty("isHovered", BindingFlags.Instance | BindingFlags.Public);
            if (_isHoveredProperty == null)
            {
                ReportUnresolved("XRGrabInteractable exposes no public isHovered property in this XRI revision.");
            }
        }

        /// <summary>
        /// A hover visual that silently never lights up hides a broken rig. Report once per
        /// component instead of falling back to success (consumer lessons register, LESSON-004).
        /// </summary>
        void ReportUnresolved(string reason)
        {
            if (_reportedUnresolved) return;
            _reportedUnresolved = true;
            Debug.LogWarning($"xr_baseline_hover_visual_unresolved: {reason}", this);
        }

        void ApplyVisual(bool hovered)
        {
            if (_renderer == null) return;

            _propertyBlock ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_propertyBlock);

            if (hovered)
            {
                _propertyBlock.SetColor(BaseColorId, _hoverBaseColor);
                _propertyBlock.SetColor(EmissionColorId, _hoverEmission);
            }
            else
            {
                _propertyBlock.SetColor(BaseColorId, _idleBaseColor);
                _propertyBlock.SetColor(EmissionColorId, Color.black);
            }

            _renderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
