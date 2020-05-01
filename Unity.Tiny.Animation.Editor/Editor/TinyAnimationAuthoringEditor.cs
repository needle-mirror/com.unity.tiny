using System.Globalization;
using TinyInternal.Bridge;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Unity.Tiny.Animation.Editor
{
    [CustomEditor(typeof(TinyAnimationAuthoring))]
    class TinyAnimationAuthoringEditor : UnityEditor.Editor
    {
        SerializedProperty m_AnimationClipsProp;
        SerializedProperty m_PlayAutomaticallyProp;
        SerializedProperty m_PatchScaleProp;
        SerializedProperty m_AdditionalAnimatorClipsProp;

        TinyAnimationAuthoring m_TinyAnimationAuthoring;
        int m_NonNullUserClipsCount;
        int m_TargetDirtyCount;

        AnimatorController m_CurrentController;
        int m_AnimatorDirtyCount;
        bool m_AnimatorControllerIsDirty;
        Animator m_Animator;

        void OnEnable()
        {
            m_TinyAnimationAuthoring = (TinyAnimationAuthoring)target;
            m_Animator = m_TinyAnimationAuthoring.GetComponent<Animator>();
            m_CurrentController = TinyAnimationEditorBridge.GetEffectiveAnimatorController(m_Animator);

            // Ensures an update during first pass
            m_TargetDirtyCount = -1;
            m_AnimatorDirtyCount = -1;

            m_AnimationClipsProp = serializedObject.FindProperty(nameof(TinyAnimationAuthoring.animationClips));
            m_PlayAutomaticallyProp = serializedObject.FindProperty(nameof(TinyAnimationAuthoring.playAutomatically));
            m_PatchScaleProp = serializedObject.FindProperty(nameof(TinyAnimationAuthoring.patchMissingScaleIfNeeded));
            m_AdditionalAnimatorClipsProp = serializedObject.FindProperty(nameof(TinyAnimationAuthoring.additionalAnimatorClips));
        }

        void OnDisable()
        {
            if (m_CurrentController != null)
            {
                TinyAnimationEditorBridge.UnregisterDirtyCallbackFromAnimatorController(m_CurrentController, OnControllerDirty);
                m_CurrentController = null;
            }

            m_TinyAnimationAuthoring = null;
            m_Animator = null;

            m_AnimationClipsProp = null;
            m_PlayAutomaticallyProp = null;
            m_PatchScaleProp = null;
            m_AdditionalAnimatorClipsProp = null;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();
            EditorGUILayout.PropertyField(m_AnimationClipsProp, true, null);
            EditorGUILayout.PropertyField(m_PlayAutomaticallyProp, false, null);
            EditorGUILayout.PropertyField(m_PatchScaleProp, false, null);
            serializedObject.ApplyModifiedProperties();

            var newTargetDirtyCount = EditorUtility.GetDirtyCount(m_TinyAnimationAuthoring);
            var newAnimatorDirtyCount = EditorUtility.GetDirtyCount(m_Animator);

            var targetIsDirty = m_TargetDirtyCount != newTargetDirtyCount;
            var animatorComponentIsDirty = m_AnimatorDirtyCount != newAnimatorDirtyCount;
            var animatorControllerAssetIsDirty = m_AnimatorControllerIsDirty;

            if (targetIsDirty)
                m_TargetDirtyCount = newTargetDirtyCount;

            if (animatorComponentIsDirty)
            {
                m_AnimatorDirtyCount = newAnimatorDirtyCount;

                if (m_CurrentController != null)
                    TinyAnimationEditorBridge.UnregisterDirtyCallbackFromAnimatorController(m_CurrentController, OnControllerDirty);

                m_CurrentController = TinyAnimationEditorBridge.GetEffectiveAnimatorController(m_Animator);

                if (m_CurrentController != null)
                    TinyAnimationEditorBridge.RegisterDirtyCallbackForAnimatorController(m_CurrentController, OnControllerDirty);
            }

            if (animatorControllerAssetIsDirty)
                m_AnimatorControllerIsDirty = false;

            if (targetIsDirty || animatorComponentIsDirty || animatorControllerAssetIsDirty)
                m_TinyAnimationAuthoring.UpdateAdditionalAnimatorClips();

            if (m_TinyAnimationAuthoring.additionalAnimatorClips.Count == 0)
                return;

            if (targetIsDirty)
                UpdateNonNullUserClipsCount();

            EditorGUILayout.Space();

            EditorGUILayout.HelpBox($"The following clips will be converted (in that order, starting at index {m_NonNullUserClipsCount.ToString(CultureInfo.InvariantCulture)}) from the Animator.\n" +
                "Note that the Animator's state machine will not get converted; only the clips.", MessageType.Info, true);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(m_AdditionalAnimatorClipsProp, true, null);
            }
        }

        void OnControllerDirty()
        {
            m_AnimatorControllerIsDirty = true;
        }

        void UpdateNonNullUserClipsCount()
        {
            m_NonNullUserClipsCount = 0;
            if (m_TinyAnimationAuthoring.animationClips == null)
                return;

            foreach (var clip in m_TinyAnimationAuthoring.animationClips)
            {
                if (clip != null)
                    m_NonNullUserClipsCount++;
            }
        }
    }
}
