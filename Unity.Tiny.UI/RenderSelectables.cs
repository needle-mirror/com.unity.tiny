using Unity.Entities;
using Unity.Tiny.Rendering;

namespace Unity.Tiny.UI
{
    [UpdateAfter(typeof(ProcessUIEvents))]
    [UpdateBefore(typeof(UpdateVisibility))]
    public class RenderSelectables : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities
                .WithAll<RectTransform>()
                .WithoutBurst()
                .ForEach((Entity e, in Selectable selectable, in RectTransform trans, in RectTransform rectTransform, in UIState sr) =>
                {

                    if (!EntityManager.HasComponent<SimpleMaterial>(selectable.Graphic)) return;

                    // TODO: breaks when multiple buttons have the same target graphic as one result overwrites the other
                    // fix by only running this code when the UI state has changed from the previous frame
                    var sm = EntityManager.GetComponentData<SimpleMaterial>(selectable.Graphic);
                    var graphicTransform = EntityManager.GetComponentData<RectTransform>(selectable.Graphic);

                    if (rectTransform.Hidden)
                    {
                        graphicTransform.Hidden = true;
                        EntityManager.SetComponentData(selectable.Graphic, graphicTransform);
                        return;
                    }

                    if (sr.IsPressed)
                    {
                        // down
                        sm.constAlbedo = selectable.PressedColor.AsFloat4().xyz;
                        sm.constOpacity = selectable.PressedColor.AsFloat4().w;
                    }
                    else if (sr.IsHighlight)
                    {
                        // up / hover
                        sm.constAlbedo.xyz = selectable.HighlightedColor.AsFloat4().xyz;
                        sm.constOpacity = selectable.HighlightedColor.AsFloat4().w;
                    }
                    else
                    {
                        // none
                        sm.constAlbedo = selectable.NormalColor.AsFloat4().xyz;
                        sm.constOpacity = selectable.NormalColor.AsFloat4().w;
                    }

                    EntityManager.SetComponentData(selectable.Graphic, sm);
                }).Run();
        }
    }
}
