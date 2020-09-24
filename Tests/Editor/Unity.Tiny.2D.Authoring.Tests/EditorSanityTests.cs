using UnityEngine;
using NUnit.Framework;

[TestFixture]
public class EditorSanityTests : AuthoringTestFixture
{
    private SortingLayer[] m_StartingLayers;

    [SetUp]
    protected override void Setup()
    {
        base.Setup();
        m_StartingLayers = SortingLayer.layers;
    }

    [TearDown]
    protected override void TearDown()
    {
        base.TearDown();

        var finalLayers = SortingLayer.layers;
        for (var i = finalLayers.Length - 1; i >= 0; i--)
        {
            if (System.Array.FindIndex(m_StartingLayers, layer => layer.id == finalLayers[i].id) != -1)
                continue;

            EditorTestUtilities.RemoveSortingLayerFromTagManager(finalLayers[i].name);
        }
    }

    [Test]
    public void EditorSanityTests_NewGameObject_CreatedGameObject()
    {
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var startNoOfGOs = activeScene.GetRootGameObjects().Length;

        Root = new GameObject();

        var finalNoOfGOs = activeScene.GetRootGameObjects().Length;

        Assert.That(finalNoOfGOs - startNoOfGOs, Is.EqualTo(1));
    }

    [Test]
    public void EditorSanityTests_NewComponent_CreatedComponent()
    {
        var startNoOfSRs = Object.FindObjectsOfType<SpriteRenderer>().Length;

        Root = new GameObject();
        CreateClassicComponent<SpriteRenderer>(Root);

        var finalNoOfSRs = Object.FindObjectsOfType<SpriteRenderer>().Length;

        Assert.That(finalNoOfSRs - startNoOfSRs, Is.EqualTo(1));
    }

    [Test]
    public void EditorSanityTests_ConvertGameObject_SuccessfulConversion()
    {
        Root = new GameObject();

        var wasConversionSuccessful = RunConversion(Root);

        Assert.That(wasConversionSuccessful, Is.True);
    }

    [Test]
    public void EditorSanityTests_ConvertNull_UnsuccessfulConversion()
    {
        var wasConversionSuccessful = RunConversion(null);
        Assert.That(wasConversionSuccessful, Is.False);
    }

    [Test]
    public void EditorSanityTests_ConvertAndCleanup_N_Times_WorldGotZeroEntities(
        [Values(1, 2, 10)] int noOfConverts)
    {
        Root = new GameObject();

        for (var i = 0; i < noOfConverts; i++)
        {
            RunConversion(Root);
            CleanupWorld();
        }

        var allEntities = EntityManager.GetAllEntities();
        Assert.That(allEntities.Length, Is.EqualTo(0));
    }

    [Test]
    public void EditorSanityTests_AddSortingLayerToTagManagerMethod_NewSortingLayerAdded(
        [Values("NewSortingLayer", "UnlikelyExistingName_7rEWiosG2")] string newSortingLayerName)
    {
        EditorTestUtilities.AddSortingLayerToTagManager(newSortingLayerName);

        Assert.That(EditorTestUtilities.SortingLayerExists(newSortingLayerName), Is.True);

        EditorTestUtilities.RemoveSortingLayerFromTagManager(newSortingLayerName);
    }

    [Test]
    public void EditorSanityTests_RemoveSortingLayerFromTagManager_SortingLayerRemoved(
        [Values("SortingLayerToRemove", "UnlikelyExistingName_Wsx85qk")] string sortingLayerToRemove)
    {
        EditorTestUtilities.AddSortingLayerToTagManager(sortingLayerToRemove);
        Assert.That(EditorTestUtilities.SortingLayerExists(sortingLayerToRemove), Is.True);

        EditorTestUtilities.RemoveSortingLayerFromTagManager(sortingLayerToRemove);
        Assert.That(EditorTestUtilities.SortingLayerExists(sortingLayerToRemove), Is.False);
    }
}
