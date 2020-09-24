using Unity.Assertions;
using UnityEngine;
using UnityEditor;

using Hash128 = Unity.Entities.Hash128;

namespace Unity.Tiny.Authoring
{
    internal static class Tiny2DAuthoringUtils
    {
        private const string k_BuiltInGuid = "0000000000000000f000000000000000";

        public static string GetFullAssetPath(Object asset)
        {
            Assert.IsNotNull(asset, "Asset cannot be null");

            if (!AssetDatabase.Contains(asset))
                return string.Empty;

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long localId);
            if (guid == k_BuiltInGuid)
                return string.Empty;

            var spritePath = AssetDatabase.GetAssetPath(asset);
            spritePath = spritePath.Remove(0, 6);

            return Application.dataPath + spritePath;
        }

        public static string GetFullAssetMetaPath(Object asset)
        {
            var assetPath = GetFullAssetPath(asset);

            if (string.IsNullOrEmpty(assetPath))
                return string.Empty;

            return assetPath + ".meta";
        }

        public static Hash128 GetFileLastModifiedHash(string pathToFile)
        {
            Assert.IsTrue(System.IO.File.Exists(pathToFile), $"File does not exist at path: {pathToFile}");
            var lastModifiedTime = System.IO.File.GetLastWriteTime(pathToFile);
            return new Hash128((uint)lastModifiedTime.GetHashCode(), 0, 0, 0);
        }

        public static Hash128 GetObjectGuidHash(Object asset)
        {
            Assert.IsNotNull(asset, "Asset cannot be null");
            if (!AssetDatabase.Contains(asset))
                return new Hash128((uint)asset.GetInstanceID(), 0, 0, 0);

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long fileId);
            var guidHash = new Hash128(guid);
            var fileIdHash = new Hash128((uint)fileId, 0, 0, 0);

            guidHash.Value += fileIdHash.Value;
            return guidHash;
        }
    }
}
