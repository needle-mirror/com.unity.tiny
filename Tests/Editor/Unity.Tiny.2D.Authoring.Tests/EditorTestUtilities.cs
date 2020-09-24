using System.Reflection;
using UnityEditor;
using UnityEngine;
using Unity.Collections;
using NUnit.Framework;

public static class EditorTestUtilities
{
    public const float Epsilon = 0.001f;

    private const string k_TagManagerPath = "ProjectSettings/TagManager.asset";
    private const string k_DefaultLayerName = "Default";
    private const string k_SortingLayersPropertyName = "m_SortingLayers";
    private const string k_NamePropertyName = "name";

    private static Assembly s_EditorAssembly;
    private static MethodInfo s_AddSortingLayerMethod;

    public static bool SortingLayerExists(string sortingLayerName)
    {
        var sortingLayers = SortingLayer.layers;
        foreach (var sortingLayer in sortingLayers)
        {
            if (sortingLayer.name == sortingLayerName)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Add a new sorting layer to the TagManager if it's not already there
    /// </summary>
    /// <param name="sortingLayerName">The name of the Sorting Layer to be added</param>
    /// <returns>If the Sorting Layer to add is already in the TagManager, false is returned, true otherwise</returns>
    public static bool AddSortingLayerToTagManager(string sortingLayerName)
    {
        if (sortingLayerName == k_DefaultLayerName)
            return false;

        var tagManager = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(k_TagManagerPath));

        var sortingLayers = tagManager.FindProperty(k_SortingLayersPropertyName);
        for (var i = 0; i < sortingLayers.arraySize; i++)
        {
            var sortingLayerProperty = sortingLayers.GetArrayElementAtIndex(i);
            var sortingLayerNameProperty = sortingLayerProperty.FindPropertyRelative(k_NamePropertyName);

            if (sortingLayerNameProperty.stringValue.Equals(sortingLayerName))
                return false;
        }

        AddSortingLayer();
        tagManager.Update();

        var newLayer = sortingLayers.GetArrayElementAtIndex(sortingLayers.arraySize - 1);
        newLayer.FindPropertyRelative(k_NamePropertyName).stringValue = sortingLayerName;
        tagManager.ApplyModifiedProperties();

        return true;
    }

    public static void RemoveSortingLayerFromTagManager(string sortingLayerName)
    {
        if (sortingLayerName == k_DefaultLayerName)
            return;

        var tagManager = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(k_TagManagerPath));

        var sortingLayers = tagManager.FindProperty(k_SortingLayersPropertyName);
        for (var i = 0; i < sortingLayers.arraySize; i++)
        {
            var sortingLayerProperty = sortingLayers.GetArrayElementAtIndex(i);
            var sortingLayerNameProperty = sortingLayerProperty.FindPropertyRelative(k_NamePropertyName);

            if (!sortingLayerNameProperty.stringValue.Equals(sortingLayerName))
                continue;

            sortingLayers.DeleteArrayElementAtIndex(i);
            tagManager.ApplyModifiedProperties();
            break;
        }
    }

    /// <summary>
    /// Add a new sorting layer with the "Default" name to the Unity's TagManager
    /// </summary>
    private static void AddSortingLayer()
    {
        if (s_AddSortingLayerMethod == null)
        {
            if (s_EditorAssembly == null)
                s_EditorAssembly = Assembly.GetAssembly(typeof(Editor));

            var type = s_EditorAssembly.GetType("UnityEditorInternal.InternalEditorUtility");
            s_AddSortingLayerMethod =
                type.GetMethod("AddSortingLayer", BindingFlags.Static | BindingFlags.NonPublic);
        }

        s_AddSortingLayerMethod?.Invoke(null, null);
    }
}

[SetUpFixture]
internal class NUnitAssemblyWideSetupEntitiesTests
{
    private NativeLeakDetectionMode OldMode;

    [OneTimeSetUp]
    public void Setup()
    {
        OldMode = NativeLeakDetection.Mode;

        // Should have stack trace with tests
        NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        NativeLeakDetection.Mode = OldMode;
    }
}
