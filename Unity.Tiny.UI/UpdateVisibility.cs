using Unity.Collections;
using Unity.Entities;
using Unity.Tiny.Rendering;

namespace Unity.Tiny.UI
{
    [UpdateAfter(typeof(ProcessUIEvents))]
    public class UpdateVisibility : SystemBase
    {
        protected override void OnUpdate()
            {
                Entities
                    .WithAll<RectTransform>()
                    .WithoutBurst()
                    .WithStructuralChanges()
                    .ForEach((Entity e, in RectTransformResult rtr) =>
                    {
                        if (rtr.HiddenResult)
                        {
                            EntityManager.AddComponentData(e, new DisableRendering());
                        }
                        else if (!rtr.HiddenResult && EntityManager.HasComponent<DisableRendering>(e))
                        {
                            EntityManager.RemoveComponent<DisableRendering>(e);
                        }
                    }).Run();
            }
    }
}
