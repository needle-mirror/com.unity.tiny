using Unity.Entities;

namespace Unity.Tiny
{
    /// <summary>
    ///  Configures DOTS Runtime core related parameters. You can access this component via
    ///  TinyEnvironment.Get/SetConfigData&lt;CoreConfig&gt;()
    /// </summary>
    //[HideInInspector]
    public struct CoreConfig : IComponentData
    {
        public static CoreConfig Default { get; } = new CoreConfig
        {
            editorGuid32 = 0,
            editorVersionMajor = 0,
            editorVersionMinor = 0,
            editorVersionRevision = 0,
            editorVersionReleaseType = 0,
            editorVersionInc = 0,
        };

        /// <summary>
        /// Specifies the editor's 32-bit "GUID". This identifies an editor instance as
        /// the one which built this player.
        /// </summary>
        public uint editorGuid32;

        /// <summary>
        /// Specifies the major version number of the Unity editor which built this player.
        /// </summary>
        public int editorVersionMajor;

        /// <summary>
        /// Specifies the minor version number of the Unity editor which built this player.
        /// </summary>
        public int editorVersionMinor;

        /// <summary>
        /// Specifies the revision version number of the Unity editor which built this player.
        /// </summary>
        public int editorVersionRevision;

        /// <summary>
        /// Specifies the release type of the Unity editor which built this player.
        ///   0 = 'a' = alpha
        ///   1 = 'b' = beta
        ///   2 = 'f' = public
        ///   3 = 'p' = patch
        ///   4 = 'x' = experimental
        /// </summary>
        public int editorVersionReleaseType;

        /// <summary>
        /// Specifies the incremental version number of the Unity editor which built this player.
        /// [major].[minor].[revision][type][incremental]
        /// </summary>
        public int editorVersionInc;
    }
}
