using Unity.Entities;
using Unity.Entities.Runtime.Build;
using UnityEngine.Assertions;
using Unity.Tiny;
using System;
using System.Reflection;
using System.Text.RegularExpressions;

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
                
                var unityVersionParts = Regex.Split(UnityEngine.Application.unityVersion, @"\D+");
                config.editorVersionMajor = int.Parse(unityVersionParts[0]);
                config.editorVersionMinor = int.Parse(unityVersionParts[1]);
                
                int typeIndex = UnityEngine.Application.unityVersion.IndexOfAny(new char[] {'a','b','f','p','x'});
                if (UnityEngine.Application.unityVersion[typeIndex] == 'a')
                    config.editorVersionReleaseType = 0;  // alpha pre-release
                else if (UnityEngine.Application.unityVersion[typeIndex] == 'b')
                    config.editorVersionReleaseType = 1;  // beta pre-release
                else if (UnityEngine.Application.unityVersion[typeIndex] == 'f')
                    config.editorVersionReleaseType = 2;  // public release
                else if (UnityEngine.Application.unityVersion[typeIndex] == 'p')
                    config.editorVersionReleaseType = 3;  // patch release
                else /*if (unityVersionParts[2][typeIndex] == 'x')*/
                    config.editorVersionReleaseType = 4;  // experimental release

                config.editorVersionRevision = int.Parse(unityVersionParts[2]);
                config.editorVersionInc = int.Parse(unityVersionParts[3]);

                // There might be an additional version for special builds for customers (such as c1 for China users)
                // but we won't currently track that...
                
                EntityManager.AddComponentData(singletonEntity, config);
            }
        }
    }
}
