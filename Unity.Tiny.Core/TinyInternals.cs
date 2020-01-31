using System;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;


namespace Unity.Tiny
{
    public static class TinyInternals
    {
        public static void SetSimFixedRate(World world, float rate)
        {
            SimulationSystemGroup sim = world.GetExistingSystem<SimulationSystemGroup>();
            sim.SetFixedTimeStep(rate);
        }
    }

}
