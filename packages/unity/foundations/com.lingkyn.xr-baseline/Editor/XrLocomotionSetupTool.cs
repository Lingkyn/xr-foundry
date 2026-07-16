using System;
using UnityEngine;

namespace Lingkyn.Unity.XrBaseline.Editor.SceneSetup
{
    /// <summary>
    /// Optional stick locomotion for XR rigs. Default baseline keeps Move disabled (room-scale + teleport).
    /// </summary>
    public static class XrLocomotionSetupTool
    {
        public static void ApplyContinuousMovePreference(GameObject rigRoot, bool enabled)
        {
            if (rigRoot == null) return;

            foreach (var transform in rigRoot.GetComponentsInChildren<Transform>(true))
            {
                if (IsMoveNode(transform.gameObject))
                {
                    transform.gameObject.SetActive(enabled);
                }

                foreach (var behaviour in transform.GetComponents<Behaviour>())
                {
                    if (behaviour == null) continue;
                    if (!IsContinuousMoveBehaviour(behaviour)) continue;
                    behaviour.enabled = enabled;
                }
            }
        }

        static bool IsMoveNode(GameObject go) =>
            go.name.Equals("Move", StringComparison.OrdinalIgnoreCase);

        static bool IsContinuousMoveBehaviour(Behaviour behaviour)
        {
            var typeName = behaviour.GetType().Name;
            return typeName.Contains("ContinuousMove", StringComparison.Ordinal);
        }
    }
}
