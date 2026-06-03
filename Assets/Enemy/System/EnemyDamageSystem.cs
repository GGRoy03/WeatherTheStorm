using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

using WeatherTheStorm.Tower;

namespace WeatherTheStorm.Enemy
{
    public struct Status : IComponentData
    {
        public float Health;
        public float MaxHealth;
    }

    public struct EnemyDamageRequest : IComponentData
    {
        public Entity Target;
        public float  Damage;
    }

    [UpdateAfter(typeof(ProjectileSystem))]
    public partial struct EnemyDamageSystem : ISystem
    {
        private ComponentLookup<Status> m_StatusTable;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_StatusTable = state.GetComponentLookup<Status>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton<TowerFrameOutput>(out var towerOutput))
            {
                m_StatusTable.Update(ref state);

                var commandBuffer = SystemAPI
                    .GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged);

                JobHandle processDamageJob = new ProcessDamageEventJob
                {
                    HealthTable         = m_StatusTable,
                    CommandBuffer       = commandBuffer,
                    DamageRequestStream = towerOutput.DamageRequestStream,

                }.Schedule(state.Dependency);

                //
                // NOTE:
                // See OnCreate for ProjectileSystem for why we do this.
                //

                state.Dependency = towerOutput.DamageRequestStream.Dispose(processDamageJob);
            }
        }


        [BurstCompile]
        partial struct ProcessDamageEventJob : IJobEntity
        {
                       public ComponentLookup<Status> HealthTable;
                       public EntityCommandBuffer     CommandBuffer;
            [ReadOnly] public NativeStream            DamageRequestStream;

            //
            // TODO:
            // x) This health stuff is just a stub implementation, I'd be a bit more complex
            //    and have better naming.
            //

            public void Execute()
            {
                var streamReader = DamageRequestStream.AsReader();
                for (int pocketIdx = 0; pocketIdx < streamReader.ForEachCount; ++pocketIdx)
                {
                    streamReader.BeginForEachIndex(pocketIdx);
                    {
                        while (streamReader.RemainingItemCount > 0)
                        {
                            var request = streamReader.Read<EnemyDamageRequest>();

                            Entity target = request.Target;
                            if (HealthTable.HasComponent(target))
                            {
                                Status status = HealthTable[target];
                                status.Health -= request.Damage;

                                if (status.Health <= 0.0f)
                                {
                                    CommandBuffer.DestroyEntity(target);
                                }
                                else
                                {
                                    HealthTable[target] = status;
                                }
                            }
                        }
                    }
                    streamReader.EndForEachIndex();
                }
            }
        }
    }
}
