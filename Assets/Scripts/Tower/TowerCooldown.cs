using UnityEngine;
using Unity.Entities;
using Unity.Collections;


public struct AttackCooldown : IComponentData
{
    public float Duration;
    public float Elapsed;
}


public partial struct AttackCooldownSystem : ISystem
{
    public NativeArray<Entity> o_ReadyToFire;
    public int                 o_ReadyToFireCount;

    public void OnUpdate(ref SystemState state)
    {
        //
        // TODO:
        // x) Reset Loop.
        // x) Probably rework this, I don't really know if everything should just be components?
        //

        float deltaTime = Time.deltaTime;
        foreach(var (cooldown, entity) in SystemAPI.Query<RefRW<AttackCooldown>>().WithEntityAccess())
        {
            cooldown.ValueRW.Elapsed += deltaTime;

            if(cooldown.ValueRW.Elapsed >= cooldown.ValueRW.Duration)
            {
                o_ReadyToFire[o_ReadyToFireCount++] = entity;
            }
        }
    }
}
