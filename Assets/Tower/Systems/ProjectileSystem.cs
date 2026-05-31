using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using WeatherTheStorm.Enemy;

namespace WeatherTheStorm.Tower
{
    public struct RocketTag : IComponentData {};


    public struct HomingProjectile : IComponentData
    {
        public float  Speed;
        public Entity Target;
        public float3 LastKnownPos;
    };


    public struct ProjectileExplosion
    {
        public float Radius;
        public float Damage;
    };


    public struct ProjectileDamage : IComponentData
    {
        public float Damage;
    }

    public partial struct ProjectileSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {

        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            JobHandle updateHomingProjectiles = new HomingProjectileJob
            {
                PathPositionTable = SystemAPI.GetComponentLookup<PathPosition>(true),
                DeltaTime         = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(state.Dependency);

            //
            // TODO:
            // x) Can I filter the component lookup?
            // x) Write helpers for duplicate code (getting a command buffer)
            //

            var commandBuffer = SystemAPI
                .GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged)
                .AsParallelWriter();

            JobHandle updateRocketJob = new RocketCheckCollisionJob
            {
                PhysicsWorld   = SystemAPI.GetSingleton<PhysicsWorldSingleton>(),
                CommandBuffer  = commandBuffer,
            }.ScheduleParallel(updateHomingProjectiles);

            state.Dependency = updateRocketJob;
        }

        [BurstCompile]
        partial struct HomingProjectileJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<PathPosition> PathPositionTable;
                       public float                         DeltaTime;

            public void Execute(ref HomingProjectile homing, ref LocalTransform transform)
            {
                if(PathPositionTable.HasComponent(homing.Target))
                {
                    homing.LastKnownPos = PathPositionTable[homing.Target].Position;
                }

                float3 direction = math.normalize(homing.LastKnownPos - transform.Position);
                transform.Position += DeltaTime * homing.Speed * direction;
            }
        }

        //
        // NOTE:
        // x) We might not even need the rocket tag to be fair. Uhm. I just don't know at what point
        //    does the specific logic runs.
        //

        [BurstCompile]
        [WithAll(typeof(RocketTag))]
        partial struct RocketCheckCollisionJob : IJobEntity
        {
            [ReadOnly] public PhysicsWorldSingleton              PhysicsWorld;
                       public EntityCommandBuffer.ParallelWriter CommandBuffer; 

            public void Execute([ChunkIndexInQuery] int chunkIndex, in Entity entity, in ProjectileDamage damage, in LocalTransform transform)
            {
                //
                // TODO:
                // x) Figure out how to setup the collision filter.
                // x) Figure out when we want to run the rocket logic, because this code isn't specific
                //    to rockets, but to AOE projectiles.
                // x) Remove the hardcoded radius check.
                //
                
                NativeList<DistanceHit> hits = new NativeList<DistanceHit>(Allocator.Temp);
                if (PhysicsWorld.OverlapSphere(transform.Position, 5.0f, ref hits, CollisionFilter.Default))
                {
                    foreach (var hit in hits)
                    {
                        //
                        // TODO:
                        // x) Damage the enemies somehow, this is sort of tied to the damage stuff
                        //    discussed in the targeting system. It doesn't matter right now.
                        //    It's too tied to the gameplay to make some sort of decision here.
                        // x) There's also the visual stuff, let's say we wanted to make it explode,
                        //    well... How do we do that?
                        //

                        // Debug.Log("Hit!");
                    }

                    CommandBuffer.DestroyEntity(chunkIndex, entity);
                }
            }
        }
    }
}