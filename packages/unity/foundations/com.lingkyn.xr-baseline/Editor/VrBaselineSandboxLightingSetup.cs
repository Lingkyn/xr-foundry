using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Lingkyn.Unity.XrBaseline.Config;

namespace Lingkyn.Unity.XrBaseline.Editor.SceneSetup
{
    /// <summary>
    /// Tunes Sandbox greybox contrast: single directional key light, softer ambient, no duplicate suns.
    /// </summary>
    public static class VrBaselineSandboxLightingSetup
    {
        public static void Apply(Scene scene, Transform sceneRoot, VrBaselineConfig config = null)
        {
            if (sceneRoot == null) return;

            DisableExtraDirectionalLights(scene, sceneRoot);

            var lighting = sceneRoot.Find("_Lighting");
            if (lighting == null) return;

            Light directional = null;
            foreach (var light in lighting.GetComponentsInChildren<Light>(true))
            {
                if (light.type != LightType.Directional) continue;
                directional = light;
                break;
            }

            if (directional == null)
            {
                var lightGo = new GameObject("Directional Light");
                lightGo.transform.SetParent(lighting, false);
                directional = lightGo.AddComponent<Light>();
                directional.type = LightType.Directional;
            }

            var intensity = config != null ? config.directionalLightIntensity : 0.9f;
            var lightColor = config != null ? config.directionalLightColor : new Color(1f, 0.96f, 0.9f);

            directional.enabled = true;
            directional.intensity = intensity;
            directional.color = lightColor;
            directional.shadows = LightShadows.Soft;
            directional.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = config?.ambientSkyColor ?? new Color(0.42f, 0.45f, 0.5f);
            RenderSettings.ambientEquatorColor = config?.ambientEquatorColor ?? new Color(0.36f, 0.38f, 0.42f);
            RenderSettings.ambientGroundColor = config?.ambientGroundColor ?? new Color(0.28f, 0.28f, 0.3f);
            RenderSettings.ambientIntensity = config?.ambientIntensity ?? 0.88f;
        }

        static void DisableExtraDirectionalLights(Scene scene, Transform sceneRoot)
        {
            if (!scene.IsValid()) return;

            var canonicalLighting = sceneRoot.Find("_Lighting");
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                {
                    if (light.type != LightType.Directional) continue;
                    if (canonicalLighting != null && light.transform.IsChildOf(canonicalLighting)) continue;
                    light.enabled = false;
                }
            }
        }
    }
}
