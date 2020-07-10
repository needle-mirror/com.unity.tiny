using System;
using Unity.Collections;
using Unity.Entities;
using System.Diagnostics;

namespace Unity.Tiny.GenericAssetLoading
{
    public enum LoadResult
    {
        stillWorking = 0,
        success = 1,
        failed = 2
    };

    public interface IGenericAssetLoader<T, TN, TS, L>
        where T : struct, IComponentData
        where TN : struct, IComponentData, ISystemStateComponentData
        where TS : struct, IComponentData
        where L : struct, IComponentData, ISystemStateComponentData
    {
        void StartLoad(EntityManager man, Entity e, ref T thing, ref TN native, ref TS source, ref L loading);
        LoadResult CheckLoading(IntPtr cppwrapper, EntityManager man, Entity e, ref T thing, ref TN native, ref TS source, ref L loading);
        void FreeNative(EntityManager man, Entity e, ref TN native);
        void FinishLoading(EntityManager man, Entity e, ref T thing, ref TN native, ref L loading);
    }

    // T = the thing to load
    // TN = native component of the thing to load
    // TS = source component for loading
    // L = extra loading data, component is added while loading is in flight
    public class GenericAssetLoader<T, TN, TS, L> : SystemBase
        where T : struct, IComponentData
        where TN : struct, IComponentData, ISystemStateComponentData
        where TS : struct, IComponentData
        where L : struct, IComponentData, ISystemStateComponentData
    {
        protected IGenericAssetLoader<T, TN, TS, L> c;
        protected IntPtr wrapper;
        // TODO: need to dispose groups?

        EntityQuery m_CleanupQuery;
        EntityQuery m_PrepareLoadQuery;
        EntityQuery m_StartLoadQuery;
        EntityQuery m_InProgressLoadQuery;
        int m_T_TypeIndex;
        int m_TN_TypeIndex;
        int m_TS_TypeIndex;
        int m_L_TypeIndex;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_CleanupQuery = GetEntityQuery(new EntityQueryDesc
            {
                None = new[] { ComponentType.ReadWrite<T>() },
                All = new[] { ComponentType.ReadWrite<TN>() }
            });

            m_PrepareLoadQuery = GetEntityQuery(new EntityQueryDesc
            {
                None = new[] { ComponentType.ReadWrite<TN>() },
                All = new[] { ComponentType.ReadWrite<T>(), ComponentType.ReadWrite<TS>(), }
            });

            m_StartLoadQuery = GetEntityQuery(new EntityQueryDesc
            {
                None = new[] { ComponentType.ReadWrite<L>() },
                All = new[] { ComponentType.ReadWrite<T>(), ComponentType.ReadWrite<TN>(), ComponentType.ReadWrite<TS>() }
            });

            m_InProgressLoadQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadWrite<T>(), ComponentType.ReadWrite<TN>(), ComponentType.ReadWrite<TS>(), ComponentType.ReadWrite<L>() }
            });

            m_T_TypeIndex = TypeManager.GetTypeIndex<T>();
            m_TN_TypeIndex = TypeManager.GetTypeIndex<TN>();
            m_TS_TypeIndex = TypeManager.GetTypeIndex<TS>();
            m_L_TypeIndex = TypeManager.GetTypeIndex<L>();
        }


        protected override void OnUpdate()
        {
            var mgr = EntityManager;

            // cleanup native and internal components in case the thing component was removed (this also covers entity deletion)
            // remove Native and Loading if Thing is gone
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
            using (var entities = m_CleanupQuery.ToEntityArray(Allocator.TempJob))
            {
                foreach(var e in entities)
                {
                    var n = TypeManager.IsZeroSized(m_TN_TypeIndex) ? default : EntityManager.GetComponentData<TN>(e);
                    c.FreeNative(mgr, e, ref n);
                    ecb.RemoveComponent<TN>(e);
                    if (mgr.HasComponent<L>(e))
                        ecb.RemoveComponent<L>(e);
                }
            }
            ecb.Playback(mgr);
            ecb.Dispose();

            // add the Native component for Things that want to load and do not have one yet
            ecb = new EntityCommandBuffer(Allocator.Temp);
            using (var entities = m_PrepareLoadQuery.ToEntityArray(Allocator.TempJob))
            {
                foreach (var e in entities)
                {
                    ecb.AddComponent(e, default(TN)); // +TN
                }
            }
            ecb.Playback(mgr);
            ecb.Dispose();

            // start loading!
            ecb = new EntityCommandBuffer(Allocator.Temp);
            using (var entities = m_StartLoadQuery.ToEntityArray(Allocator.TempJob))
            {
                foreach (var e in entities)
                {
                    var t = TypeManager.IsZeroSized(m_T_TypeIndex)  ? default : EntityManager.GetComponentData<T>(e);
                    var n = TypeManager.IsZeroSized(m_TN_TypeIndex) ? default : EntityManager.GetComponentData<TN>(e);
                    var s = TypeManager.IsZeroSized(m_TS_TypeIndex) ? default : EntityManager.GetComponentData<TS>(e);

                    L l = default;
                    c.StartLoad(mgr, e, ref t, ref n, ref s, ref l);
                    ecb.AddComponent(e, l);

                    ecb.SetComponent(e, t);
                    ecb.SetComponent(e, n);
                    ecb.SetComponent(e, s);
                }
            }
            ecb.Playback(mgr);
            ecb.Dispose();

            // check on all things that are in flight, and finish when done
            ecb = new EntityCommandBuffer(Allocator.Temp);
            using (var entities = m_InProgressLoadQuery.ToEntityArray(Allocator.TempJob))
            {
                foreach (var e in entities)
                {
                    var t = TypeManager.IsZeroSized(m_T_TypeIndex)  ? default : EntityManager.GetComponentData<T>(e);
                    var n = TypeManager.IsZeroSized(m_TN_TypeIndex) ? default : EntityManager.GetComponentData<TN>(e);
                    var s = TypeManager.IsZeroSized(m_TS_TypeIndex) ? default : EntityManager.GetComponentData<TS>(e);
                    var l = TypeManager.IsZeroSized(m_L_TypeIndex)  ? default : EntityManager.GetComponentData<L>(e);

                    LoadResult lr = c.CheckLoading(wrapper, mgr, e, ref t, ref n, ref s, ref l);
                    if (lr == LoadResult.stillWorking)
                    {
                        ecb.SetComponent(e, t);
                        ecb.SetComponent(e, n);
                        ecb.SetComponent(e, s);
                        ecb.SetComponent(e, l);
                        continue;
                    }

                    // remove load state
                    ecb.RemoveComponent<L>(e);
                    ecb.RemoveComponent<TS>(e);
                    if (lr == LoadResult.failed)
                    {
                        c.FreeNative(mgr, e, ref n);

                        // should we remove native here?
                        ecb.SetComponent(e, t);
                        ecb.SetComponent(e, n);
                        continue;
                    }
                    // success!
                    c.FinishLoading(mgr, e, ref t, ref n, ref l);

                    ecb.SetComponent(e, t);
                    ecb.SetComponent(e, n);
                }
            }
            ecb.Playback(mgr);
            ecb.Dispose();
        }
    }
}
