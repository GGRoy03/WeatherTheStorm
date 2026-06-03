using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

using UnityEngine;
using WeatherTheStorm.Audio;
using WeatherTheStorm.Enemy;
using WeatherTheStorm.Helpers;


//
// TODO:
// x) There might be some schedulling inconsistencies relied to which system updates first between
//    the enemies, towers and projectiles, we'll need to order them correctly at some point.
// x) There might also be some weirdness due to how the physics world works, its rebuilt every frame
//    meaning changes done in this frame (moving the projectile) is not reflected when we do physics
//    queries, this could lead to weird bugs and visual inconsistencies,
//


namespace WeatherTheStorm.Tower
{
    public struct TowerFrameOutput : IComponentData
    {
        public NativeStream DamageRequestStream;
    }

    public struct HomingProjectile : IComponentData
    {
        public float  Speed;
        public float3 LastKnownPos;
    }

    public struct ProjectileTarget : IComponentData
    {
        public Entity Entity;
    }

    public struct ProjectileMissile : IComponentData
    {
        public float Damage;
        public float ExplosionRadius;
    }

    public struct ProjectileRequest : IComponentData, IEnableableComponent
    {
        public float3 From;
    }

    public struct ProjectileAudio : IComponentData
    {
        public UnityObjectRef<AudioClip>   HitSound;
        public UnityObjectRef<AudioSource> Source;
    }

    public partial struct ProjectileSystem : ISystem
    {
        private ComponentLookup<PathPosition> m_PathPositionTable;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_PathPositionTable = SystemAPI.GetComponentLookup<PathPosition>(true);

            //
            // NOTE:
            // I just can't believe there's no reasonable way to pipe data through systems.
            // The simplest would be to use the command buffer, but the complexity is just not
            // worth it. We'll use the saner approach of having yet another singleton and storing
            // our outputs on it and assume that the consumer system releases it.
            //

            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, new TowerFrameOutput());
        }

        //
        // TODO:
        // x) The dependency between the TriggerMissleJob and the ProjectileCollisionJob isn't
        //    real since the changes done in the collision job do not do anything until next frame.
        //    The problem is that both try to write to the same command buffer which unity doesn't
        //    allow. We'll add a dependency for now.
        //

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            m_PathPositionTable.Update(ref state);

            var commandBufferSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var commandBuffer = commandBufferSingleton.CreateCommandBuffer(state.WorldUnmanaged);
            var commandBufferParallel = commandBuffer.AsParallelWriter();

            JobHandle updateHomingProjectileJob = new HomingProjectileJob
            {
                PathPositionTable = m_PathPositionTable,
                CommandBuffer     = commandBufferParallel,
                DeltaTime         = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(state.Dependency);

            JobHandle projectileCollisionJob = new ProjectileCollisionJob
            {
                PhysicsWorld  = SystemAPI.GetSingleton<PhysicsWorldSingleton>(),
                CommandBuffer = commandBufferParallel,
            }.ScheduleParallel(updateHomingProjectileJob);

            JobHandle triggerMissileJob;
            {
                //
                // NOTE:
                // x) WorkerCount is the amount of threads that Unity uses as thread that consumes the
                //    jobs, we add 1, because the main thread could also be taking in some of the work.
                // x) NativeStream seems like the correct structure here, but we have to be careful and
                //    add a BeginForEachIndex call into each of the job that uses it and use the passed
                //    in thread index.
                // x) None of these jobs depend on each other, they simply write some damage request
                //    however they want. There's a slight issue with this being that we can technically
                //    damage dead entities which is probably fine overall.
                //

                int workerCount        = JobsUtility.JobWorkerCount + 1;
                var damageStream       = new NativeStream(workerCount, Allocator.TempJob);
                var spatialSoundStream = new NativeStream(workerCount, Allocator.TempJob);

                triggerMissileJob = new ProjectileMissileJob
                {
                    PhysicsWorld        = SystemAPI.GetSingleton<PhysicsWorldSingleton>(),
                    CommandBuffer       = commandBufferParallel,
                    DamageRequestStream = damageStream.AsWriter(),
                    SpatialSoundStream  = spatialSoundStream.AsWriter(),
                }.Schedule(projectileCollisionJob);

                JobHandle playbackSpatialSoundJob = new ProjectileSpatialSoundJob
                {
                    SpatialSoundStream = spatialSoundStream
                }.Schedule(triggerMissileJob);
                spatialSoundStream.Dispose(playbackSpatialSoundJob);

                //
                // NOTE:
                // See OnCreate for why we do this.
                //

                SystemAPI.SetSingleton(new TowerFrameOutput { DamageRequestStream = damageStream });
            }

            state.Dependency = triggerMissileJob;
        }

        //
        // TODO:
        // Cleanup this whole thing.
        //

        [BurstCompile]
        partial struct HomingProjectileJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<PathPosition> PathPositionTable;
            public EntityCommandBuffer.ParallelWriter CommandBuffer;
            public float DeltaTime;

            public void Execute([EntityIndexInQuery] int chunkIndex, in Entity entity, in ProjectileTarget target, ref HomingProjectile homing, ref LocalTransform transform)
            {
                if (PathPositionTable.HasComponent(target.Entity))
                {
                    homing.LastKnownPos = PathPositionTable[target.Entity].Position;

                    float3 direction = math.normalize(homing.LastKnownPos - transform.Position);
                    transform.Position += DeltaTime * homing.Speed * direction;
                }
                else
                {
                    float distance = math.distancesq(homing.LastKnownPos, transform.Position);
                    if (distance < 0.005f)
                    {
                        CommandBuffer.SetComponentEnabled<ProjectileRequest>(chunkIndex, entity, true);
                    }
                    else
                    {
                        float3 direction = math.normalize(homing.LastKnownPos - transform.Position);
                        transform.Position += DeltaTime * homing.Speed * direction;
                    }
                }

            }
        }

        [BurstCompile]
        [WithPresent(typeof(ProjectileRequest))]
        partial struct ProjectileCollisionJob : IJobEntity
        {
            [ReadOnly] public PhysicsWorldSingleton PhysicsWorld;
            public EntityCommandBuffer.ParallelWriter CommandBuffer;

            public void Execute([ChunkIndexInQuery] int chunkIndex, in Entity entity, in LocalTransform transform)
            {
                CollisionWorld collisionWorld = PhysicsWorld.CollisionWorld;

                //
                // NOTE:
                // It's not specified what this returns when not found, so we simply check for a
                // valid index. Not finding is also an error I guess? Do we just destroy it to
                // prevent infinitely processing it?
                //

                int projectileRigidBodyIndex = collisionWorld.GetRigidBodyIndex(entity);
                if (projectileRigidBodyIndex >= 0 && projectileRigidBodyIndex < collisionWorld.NumBodies)
                {
                    OverlapAabbInput overlapInput = new()
                    {
                        Aabb = collisionWorld.Bodies[projectileRigidBodyIndex].CalculateAabb(),
                        Filter = ECSPhysicsHelpers.ProjectileFilter,
                    };

                    var hits = new NativeList<int>(Allocator.Temp);
                    if (collisionWorld.OverlapAabb(overlapInput, ref hits))
                    {
                        //
                        // NOTE:
                        // As far as I understand, when we call Overlap AABB on the collision world
                        // it will also overlap the projectile's bounding box, thus always returning
                        // at least 1. If we have more than one hits, it means something else the
                        // projectile collides with has been hit.
                        //

                        if (hits.Length > 1)
                        {
                            //
                            // NOTE:
                            // We pay the cost of structural changes and frame of lag here for simplicity
                            // and because I can't find another simple way to do this. If we were to use
                            // something like a native stream, we'd read it from multiple jobs such
                            // as the missile job and use it to sparsely lookup into some table the
                            // associated projectile which would probably just be terrible.
                            //

                            CommandBuffer.SetComponent(chunkIndex, entity, new ProjectileRequest
                            {
                                From = transform.Position,
                            });
                            CommandBuffer.SetComponentEnabled<ProjectileRequest>(chunkIndex, entity, true);
                        }
                    }
                }
            }
        }

        [BurstCompile]
        partial struct ProjectileMissileJob : IJobEntity
        {
            [NativeSetThreadIndex] private readonly int ThreadIndex;

            [ReadOnly] public PhysicsWorldSingleton   PhysicsWorld;
            public EntityCommandBuffer.ParallelWriter CommandBuffer;
            public NativeStream.Writer                DamageRequestStream;
            public NativeStream.Writer                SpatialSoundStream;

            public void Execute([ChunkIndexInQuery] int chunkIndex, in Entity entity, in ProjectileRequest request, in ProjectileMissile missile)
            {
                DamageRequestStream.BeginForEachIndex(ThreadIndex);
                SpatialSoundStream.BeginForEachIndex(ThreadIndex);
                {
                    CollisionWorld collisionWorld = PhysicsWorld.CollisionWorld;

                    var hits = new NativeList<DistanceHit>(Allocator.Temp);
                    if (collisionWorld.OverlapSphere(request.From, missile.ExplosionRadius, ref hits, ECSPhysicsHelpers.CheckAgainstEnemyFilter))
                    {
                        foreach (DistanceHit hit in hits)
                        {
                            DamageRequestStream.Write(new EnemyDamageRequest
                            {
                                Target = hit.Entity,
                                Damage = missile.Damage,
                            });

                            SpatialSoundStream.Write(new SpatialSoundRequest
                            {
                                Position = request.From,
                                Type = GameAudio.MissileLaunch,
                            });
                        }
                    }

                    CommandBuffer.DestroyEntity(chunkIndex, entity);
                }
                SpatialSoundStream.EndForEachIndex();
                DamageRequestStream.EndForEachIndex();
            }
        }

        //
        // TODO:
        // x) That's the big downside of this approach is that, we need to process these streams
        //    of data, but the logic is always the same... I wonder, can we just like put it in
        //    another file or something? Would that work? What's the difference between a job entity
        //    and a job?
        //
        // NOTE:
        // x) I can't find a way to burst compile this, because I assume it goes to write memory
        //    in the managed part which burst doesn't handle. Uhm, there are ways to make this work
        //    if the make the audio stuff live in the ECS/DOTS part, but it's just a pain in the ass.
        // x) This is just a dump copy job to allow systems that want to play audio to still burst
        //    compile. I don't want to spend time profiling this so I just assume it's faster to do this.
        //

        partial struct ProjectileSpatialSoundJob : IJob
        {
            public NativeStream SpatialSoundStream;
            public void Execute()
            {
                var streamReader = SpatialSoundStream.AsReader();
                for (int pocketIdx = 0; pocketIdx < streamReader.ForEachCount; ++pocketIdx)
                {
                    streamReader.BeginForEachIndex(pocketIdx);
                    {
                        while (streamReader.RemainingItemCount > 0)
                        {
                            var spatialSoundRequest = streamReader.Read<SpatialSoundRequest>();
                            SoundProducer.PlaySpatialSound(spatialSoundRequest);
                        }
                    }
                    streamReader.EndForEachIndex();
                }
            }
        }
    }
}