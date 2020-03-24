using System.Globalization;
using UnityEditor;

namespace Unity.Tiny.Animation.Editor
{
    [CustomEditor(typeof(TinyAnimationMecanimSupport))]
    class MecanimSupportEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (targets.Length > 1) // Show the default "Multi-object editing not supported." message
                return;

            EditorGUILayout.Space();
            var legacyAnimationClipCount = ((TinyAnimationMecanimSupport)target).GetComponent<UnityEngine.Animation>().GetClipCount();
            EditorGUILayout.HelpBox($"TinyAnimation considers the first Mecanim clip from this list to be at index {legacyAnimationClipCount.ToString(NumberFormatInfo.InvariantInfo)} for the purposes of its runtime APIs.", MessageType.Info, true);
        }
    }
}
