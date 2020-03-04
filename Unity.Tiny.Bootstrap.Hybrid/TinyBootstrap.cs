using System;
using System.Linq;
using Unity.Entities;
using UnityEngine;

namespace Unity.Tiny.Bootstrap.Hybrid
{
    public class TinyHybridWorldBootstrap : ICustomBootstrap
    {
        public bool Initialize(string defaultWorldName)
        {
            Debug.Log("Tiny Hybrid Bootstrap executing");
            var world = new World("Default World");
            World.DefaultGameObjectInjectionWorld = world;

            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);

            // filter out the tiny systems
            systems = systems.Where(s => {
                var asmName = s.Assembly.FullName;
                
                //Filter out all configuration systems converting bgfx shader, tiny render settings and tiny SceneStartups
                string typeName = s?.BaseType?.Name;
                if (typeName != null && (typeName.Contains("ConfigurationSystemBase") || typeName.Contains("ShaderExportSystem")))
                {
                    return false;
                }

                // White list `Unity.Tiny.Rendering`, but not `Unity.Tiny.Rendering.Native`
                // We need the camera and the world bounds for in editor play mode
                if(asmName.Contains("Unity.Tiny.Rendering") && !asmName.Contains("Native"))
                {
                    return true;
                }

                if (asmName.Contains("Unity.Tiny") && !asmName.Contains("Hybrid"))
                {
                    return false;
                }

                return true;
            }).ToList();

            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            ScriptBehaviourUpdateOrder.UpdatePlayerLoop(world);
            return true;
        }
    }
}
