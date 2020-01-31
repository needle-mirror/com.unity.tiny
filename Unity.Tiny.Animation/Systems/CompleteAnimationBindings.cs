using Unity.Entities;

namespace Unity.Tiny.Animation
{
    // TODO: jobify once the bogus IL generation is fixed in DOTS Player
    
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    class CompleteAnimationBindings : ComponentSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((Entity entity, DynamicBuffer<AnimationBindingRetarget> bindingRetargetBuffer, DynamicBuffer<AnimationBinding> bindings) =>
            {
                for (int i = 0; i < bindingRetargetBuffer.Length; ++i)
                {
                    var binding = bindings[i];
                    binding.targetComponentTypeIndex = TypeManager.GetTypeIndexFromStableTypeHash(bindingRetargetBuffer[i].stableTypeHash);
                    bindings[i] = binding;
                }

                PostUpdateCommands.RemoveComponent<AnimationBindingRetarget>(entity);
            });
        }
    }
}
