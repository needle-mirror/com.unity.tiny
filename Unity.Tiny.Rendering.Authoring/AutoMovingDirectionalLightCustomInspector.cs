using UnityEditor;
using UnityEngine;

namespace Unity.Tiny.Authoring
{
    [CustomEditor(typeof(AutoMovingDirectionalLight))]
    public class AutoMovingDirectionalLightCustomInspector : Editor
    {
        private AutoMovingDirectionalLight comp;

        private void OnEnable()
        {
            comp = target as AutoMovingDirectionalLight;
        }

        public override void OnInspectorGUI()
        {
            base.DrawDefaultInspector();
            var light = comp.gameObject.GetComponent<Light>();
            if (light == null)
                EditorGUILayout.HelpBox($"{nameof(Tiny.Authoring.AutoMovingDirectionalLight)} is only working with Directional shadow mapped lights. Please add a Directional Light component with shadows enabled", MessageType.Warning);
            else if (light.type != LightType.Directional)
                EditorGUILayout.HelpBox($"{nameof(Tiny.Authoring.AutoMovingDirectionalLight)} is only working with Directional shadow mapped lights. Please use it on a Directional Light instead", MessageType.Warning);
            else if (light.shadows == LightShadows.None)
                EditorGUILayout.HelpBox($"{nameof(Tiny.Authoring.AutoMovingDirectionalLight)} is working with a Directional Light using shadows. Please use a shadow type in the Light Component", MessageType.Warning);
        }
    }
}
