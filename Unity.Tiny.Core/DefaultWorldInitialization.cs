using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine.Assertions;

// We use the Entities namespace for parity with Entities.Hybrid's DefaultWorldInitialization
namespace Unity.Entities
{
    public static class DefaultWorldInitialization
    {
        /// <summary>
        /// Initializes the default world or runs ICustomBootstrap if one is available.
        /// ComponentSystems will be created and sorted into the high level ComponentSystemGroups.
        /// </summary>
        /// <param name="defaultWorldName">The name of the world that will be created unless there is a custom bootstrap.</param>
        /// <seealso cref="InitializeWorld"/>
        /// <seealso cref="AddSystemsToRootLevelSystemGroups"/>
        public static World Initialize(string defaultWorldName, bool editor = false)
        {
            if (!editor)
            {
                var bootstrap = GetCustomBootstrap();
                if (bootstrap != null && bootstrap.Initialize(defaultWorldName))
                {
                    Assert.IsTrue(World.DefaultGameObjectInjectionWorld != null,
                        $"ICustomBootstrap.Initialize() implementation failed to set " +
                        $"World.DefaultGameObjectInjectionWorld, despite returning true " +
                        $"(indicating the World has been properly initialized)");
                    return World.DefaultGameObjectInjectionWorld;
                }
            }

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
                world = InitializeWorld(defaultWorldName);

            AddSystemsToRootLevelSystemGroups(world);

            // Note that System sorting is done by the individual ComponentSystemGroups, as needed.
            return world;
        }

        /// <summary>
        /// Adds the list of systems to the world by injecting them into the root level system groups
        /// (InitializationSystemGroup, SimulationSystemGroup and PresentationSystemGroup). If a null system
        /// list is passed, all systems are automatically added.
        /// </summary>
        public static void AddSystemsToRootLevelSystemGroups(World world, List<Type> systems = null)
        {
            // Initialize the root level systems in case they haven't been created yet
            world.GetOrCreateSystem<InitializationSystemGroup>();
            world.GetOrCreateSystem<SimulationSystemGroup>();
            world.GetOrCreateSystem<PresentationSystemGroup>();

            if (systems == null)
                systems = GetAllSystems(WorldSystemFilterFlags.Default);

            // Create the working set of systems.
            // The full set of Systems must be created (and initialized with the World) before
            // they can be placed into SystemGroup. Else you get the problem that a System may
            // be put into a SystemGroup that hasn't been created.
            IterateAllAutoSystems(world, systems, (World w, Type systemType) =>
            {
                // Need the if check because game/test code may have auto-constructed a System already.
                if (world.GetExistingSystem(systemType) == null)
                {
                    AddSystem(world, TypeManager.ConstructSystem(systemType), false);
                }
            });

            IterateAllAutoSystems(world, systems, (World w, Type systemType) =>
            {
                AddSystemToGroup(world, world.GetExistingSystem(systemType));
            });
        }

        /// <summary>
        /// Calculates a list of all systems filtered with WorldSystemFilterFlags, [DisableAutoCreation] etc.
        /// </summary>
        /// <param name="filterFlags"></param>
        /// <param name="requireExecuteAlways">Optionally require that [ExecuteAlways] is present on the system. This is used when creating edit mode worlds.</param>
        /// <returns>The list of filtered systems</returns>
        public static List<Type> GetAllSystems(WorldSystemFilterFlags filterFlags, bool requireExecuteAlways = false)
        {
            // None of the other FilterFlags make sense for DOTS Runtime
            if (filterFlags != WorldSystemFilterFlags.Default)
                throw new ArgumentException("DOTS Runtime only supports filtering systems by WorldSystemFilterFlags.Default");

            var filteredSystemTypes = new List<Type>();

            var allSystemTypes = TypeManager.GetSystems();
            if (allSystemTypes.Length == 0)
                throw new InvalidOperationException("DefaultTinyWorldInitialization: No Systems found.");

            foreach (var s in allSystemTypes)
                filteredSystemTypes.Add(s);

            return filteredSystemTypes;
        }

        /// <summary>
        /// Initialize the World object. See <see cref="Initialize"/> for use.
        /// </summary>
        public static World InitializeWorld(string worldName)
        {
            var world = new World(worldName);
            World.DefaultGameObjectInjectionWorld = world;
            return world;
        }

        /// <summary>
        /// Call this to add a System that was manually constructed; normally these
        /// Systems are marked with [DisableAutoCreation].
        /// </summary>
        /// <param name="addSystemToGroup"></param> If true, the System will also be added to the correct
        /// SystemGroup (and the SystemGroup must already exist.) Otherwise, AddSystemToGroup() needs
        /// to be called separately, if needed.
        public static void AddSystem(World world, ComponentSystemBase system, bool addSystemToGroup)
        {
            if (world.GetExistingSystem(system.GetType()) != null)
                throw new ArgumentException("AddSystem: Error to add a duplicate system.");

            world.AddSystem(system);
            if (addSystemToGroup)
                AddSystemToGroup(world, system);
        }

        static void AddSystemToGroup(World world, ComponentSystemBase system)
        {
            var groups = TypeManager.GetSystemAttributes(system.GetType(), typeof(UpdateInGroupAttribute));
            if (groups.Length == 0)
            {
                var simulationSystemGroup = world.GetExistingSystem<SimulationSystemGroup>();
                simulationSystemGroup.AddSystemToUpdateList(system);
            }

            for (int g = 0; g < groups.Length; ++g)
            {
                var groupType = groups[g] as UpdateInGroupAttribute;
                var groupSystem = world.GetExistingSystem(groupType.GroupType) as ComponentSystemGroup;
                if (groupSystem == null)
                    throw new Exception("AddSystem failed to find existing SystemGroup.");

                groupSystem.AddSystemToUpdateList(system);
            }
        }

        static void IterateAllAutoSystems(World world, List<Type> systems, Action<World, Type> Action)
        {
            foreach (var systemType in systems)
            {
                if (TypeManager.GetSystemAttributes(systemType, typeof(DisableAutoCreationAttribute)).Length > 0)
                    continue;
                if (systemType == typeof(InitializationSystemGroup) ||
                    systemType == typeof(SimulationSystemGroup) ||
                    systemType == typeof(PresentationSystemGroup))
                {
                    continue;
                }

                Action(world, systemType);
            }
        }

        static ICustomBootstrap GetCustomBootstrap()
        {
            throw new Exception("This method should have been replaced by code-gen.");
        }
    }
}
