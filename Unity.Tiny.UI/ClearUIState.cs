using Unity.Entities;

namespace Unity.Tiny.UI
{
    public class ClearUIState : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities
                .ForEach((ref UIState state) =>
                {
                    state = default;
                }).Run();
        }
    }
}
