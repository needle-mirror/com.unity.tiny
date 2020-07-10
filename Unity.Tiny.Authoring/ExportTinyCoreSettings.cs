using Unity.Entities;
using Unity.Entities.Runtime.Build;
using UnityEngine.Assertions;
using Unity.Tiny;
using System;
using System.Reflection;

namespace Unity.Tiny.Authoring
{
    [UpdateAfter(typeof(ConfigurationSystem))]
    [DisableAutoCreation]
    public class ExportTinyCoreSettings : ConfigurationSystemBase
    {
        protected override void OnUpdate()
        {
            using (var query = EntityManager.CreateEntityQuery(typeof(ConfigurationTag)))
            {
                int num = query.CalculateEntityCount();
                Assert.IsTrue(num != 0);
                var singletonEntity = query.GetSingletonEntity();
                
                CoreConfig config = CoreConfig.Default;

                var editorConnectionType = Type.GetType($"UnityEditor.EditorConnectionInternal,UnityEditor");
                var methodGetLocalGuid = editorConnectionType?.GetMethod("GetLocalGuid");
                if (methodGetLocalGuid != null)
                {
                    config.editorGuid32 = (uint)methodGetLocalGuid.Invoke(null, null);
                }
                
                var unityVersionParts = UnityEngine.Application.unityVersion.Split('.');
                config.editorVersionMajor = int.Parse(unityVersionParts[0]);
                config.editorVersionMinor = int.Parse(unityVersionParts[1]);
                
                int typeIndex = unityVersionParts[2].IndexOfAny(new char[] {'a','b','f','p','x'});
                if (unityVersionParts[2][typeIndex] == 'a')
                    config.editorVersionReleaseType = 0;  // alpha pre-release
                else if (unityVersionParts[2][typeIndex] == 'b')
                    config.editorVersionReleaseType = 1;  // beta pre-release
                else if (unityVersionParts[2][typeIndex] == 'f')
                    config.editorVersionReleaseType = 2;  // public release
                else if (unityVersionParts[2][typeIndex] == 'p')
                    config.editorVersionReleaseType = 3;  // patch release
                else /*if (unityVersionParts[2][typeIndex] == 'x')*/
                    config.editorVersionReleaseType = 4;  // experimental release

                config.editorVersionRevision = int.Parse(unityVersionParts[2].Substring(0, typeIndex));
                config.editorVersionInc = int.Parse(unityVersionParts[2].Substring(typeIndex + 1));
                
                EntityManager.AddComponentData(singletonEntity, config);
            }
        }
    }
}
