using Unity.Entities;

namespace Unity.Tiny.UI
{
    [UpdateAfter(typeof(ProcessUIEvents))]
    [UpdateBefore(typeof(UpdateVisibility))]
    public class RenderToggle : SystemBase
    {
        protected override void OnUpdate()
        {
            // The toggled on sprite is made visible or invisible using the existing Hidden system.
            // Likewise, this system hides both the base image and toggleable image component when the user
            // sets a toggle box UI entity to hidden.
            Entities
                .WithAll<RectTransform >()
                .WithoutBurst()
                .ForEach((Entity e, in RectTransformResult result, in Toggleable toggle, in Selectable button, in UIState state) =>
                {
                    var toggledGraphicTransform = EntityManager.GetComponentData<RectTransform>(toggle.ToggledGraphic);

                    // handle hiding image entities when toggle is set to hidden
                    if (result.HiddenResult)
                    {
                        var baseGraphicTransform = EntityManager.GetComponentData<RectTransform>(button.Graphic);

                        toggledGraphicTransform.Hidden = true;
                        baseGraphicTransform.Hidden = true;
                        EntityManager.SetComponentData(button.Graphic, toggledGraphicTransform);
                    }
                    else
                    {
                        // toggle the toggle graphic when toggled
                        toggledGraphicTransform.Hidden = !toggle.IsToggled;
                        EntityManager.SetComponentData(toggle.ToggledGraphic, toggledGraphicTransform);
                    }
                }).Run();
        }
    }
}
