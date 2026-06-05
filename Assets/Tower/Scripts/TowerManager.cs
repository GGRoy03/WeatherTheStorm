using UnityEngine;

using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine.Pool;


//
// NOTE:
// x) I first implemented this inside the ECS system, but it is way too complex for what I am
//    trying to achieve. Instead we'll still use the data-oriented approach, but we'll simlify it.
// x) If this code gets too heavy to run, we still have access to jobs since we use native arrays.
//    Jobs are much easier to use than full ECS.
//

namespace WeatherTheStorm.Tower
{
    public class TowerManager : MonoBehaviour
    {
        [SerializeField] private GameObject m_TowerPrefab;

        [SerializeField] private int                    m_MaximumTowerCount;
        [SerializeField] private int                    m_MaximumProjectileCount;
                         private int                    m_TowerCount;
                         private ObjectPool<GameObject> m_ProjectilePool;

        //
        // NOTE:
        // x) This is used internally to store the permanent data.
        //
        // TODO:
        // x) So regarding projectile data, we might just use a table instead indexed by enums.
        // x) Uhm, I mean... The only we really need is some sort of free list for the pool.
        //    which could just be a queue? Or actually, I think we'd rather use some sort of swap-back
        //    approach. Swap-Back wouldn't work... Uhm, yeah it would, because projectiles are never
        //    referenced I believe.
        //

        struct TowerProjectileConfig
        {
            float Damage;
        }


        private NativeArray<float3>                m_TowerPositions;
        private NativeArray<TowerCooldown>         m_TowerCooldowns;
        private NativeArray<TowerTargeting>        m_TowerTargetings;
        private Transform[]                        m_TowerTransforms;
        private GameObject                         m_ProjectileObjects;
        private NativeArray<TowerProjectileConfig> m_ProjectileConfig;

        //
        // NOTE:
        // x) I'm not even sure that running the main thread draining code in the late update is the
        //    right idea, but I am trying it out.
        //

        private JobHandle               m_TargetingJobHandle;
        private JobHandle               m_FiringJobHandle;
        private NativeArray<int>        m_TargetIndices;
        private NativeArray<Quaternion> m_TargetRotations;
        private NativeQueue<int>        m_CreateProjectileQueue;

        void Start()
        {
            //
            // NOTE:
            // x) That looks odd. It doesn't pre-create these objects which seems like the right
            //    thing to do? Whatever, could write a pre-warmer, but at that point might as well
            //    just write my own. Doens't really matter for now, but I don't really like it.
            //

            m_ProjectilePool = new ObjectPool<GameObject>(
                createFunc:      ()    => new GameObject(),
                actionOnGet:     (obj) => obj.SetActive(true),
                actionOnRelease: (obj) => obj.SetActive(false),
                actionOnDestroy: (obj) => Destroy(obj),
                collectionCheck: true,
                defaultCapacity: m_MaximumProjectileCount,
                maxSize: m_MaximumProjectileCount
                );

            m_TowerPositions  = new NativeArray<float3>        (m_MaximumTowerCount, Allocator.Persistent);
            m_TowerCooldowns  = new NativeArray<TowerCooldown> (m_MaximumTowerCount, Allocator.Persistent);
            m_TowerTargetings = new NativeArray<TowerTargeting>(m_MaximumTowerCount, Allocator.Persistent);
            m_TowerTransforms = new Transform[m_MaximumTowerCount];
            m_TowerCount      = 10;

            for(int towerIdx = 0; towerIdx < m_TowerCount; ++towerIdx)
            {
                GameObject towerObject = GameObject.Instantiate(m_TowerPrefab);
                var        position    = new Vector3(UnityEngine.Random.Range(-5.0f, 5.0f), 0.0f, UnityEngine.Random.Range(-5.0f, 5.0f));

                m_TowerCooldowns[towerIdx] = new()
                {
                    Elapsed  = 0.0f,
                    Duration = 1.0f,
                };

                m_TowerPositions[towerIdx] = new()
                {
                    x = position.x,
                    y = position.y,
                    z = position.z,
                };

                m_TowerTargetings[towerIdx] = new()
                {
                    Mode     = TargetMode.Closest,
                    SqrRange = 5.0f,
                };

                m_TowerTransforms[towerIdx] = towerObject.transform;

                towerObject.transform.position = position;
            }
        }

        private void OnDestroy()
        {
            //
            // NOTE:
            // x) Native Memory is unmanaged so we have to explicitly release these.
            //

            if (m_TowerPositions.IsCreated)
            {
                m_TowerPositions.Dispose();
            }

            if(m_TowerCooldowns.IsCreated)
            {
                m_TowerCooldowns.Dispose();
            }

            if(m_TowerTargetings.IsCreated)
            {
                m_TowerTargetings.Dispose();
            }
        }

        void Update()
        {
            //
            // NOTE:
            // x)
            //
            // TODO:
            // x) Get the real enemy positions
            //

            var enemyPositions   = new NativeArray<float3>(50, Allocator.TempJob);
            m_TargetIndices      = new NativeArray<int>(m_TowerCount, Allocator.TempJob);
            m_TargetRotations    = new NativeArray<Quaternion>(m_TowerCount, Allocator.TempJob);
            m_TargetingJobHandle = new TowerTargetingJob
            {
                TowerPositions  = m_TowerPositions,
                TowerTargetings = m_TowerTargetings,
                EnemyPositions  = enemyPositions,
                TargetIndices   = m_TargetIndices,
                TargetRotations = m_TargetRotations,
            }.Schedule(m_TowerCount, 32);

            m_CreateProjectileQueue = new NativeQueue<int>(Allocator.TempJob);
            m_FiringJobHandle       = new TowerTryFireJob
            {
                TargetIndices         = m_TargetIndices,
                Cooldowns             = m_TowerCooldowns,
                DeltaTime             = Time.deltaTime,
                CreateProjectileQueue = m_CreateProjectileQueue.AsParallelWriter(),
            }.Schedule(m_TowerCount, 32, m_TargetingJobHandle);


            //
            // NOTE:
            // x) For the moving projectile code... I think we're missing some bits of information.
            //    Namely the enemy index. But also, now that I think about it, the enemy index would
            //    probably be some sort of handle, because we need some sort of stable identifier.
            //    Meaning the enemy system is quite hard to code as well. I need both a BVH for
            //    spatial queries and an entity handle system. We have to start working on the enemies.
            // x) So like we have to start enemy stuff. We'll first experiment with basic enemy spawning
            //    and trying to pass data between systems (namely positions). Then we need to touch
            //    on the BVH stuff to see if its manageable. Then like, if the BVH is fine and all
            //    we'll try to use it here. We also need an entity handle system, we'll do it like in 
            //    my engine.
            //
            //
            // TODO:
            // x) Moving Projectile Code (Generic)
            // x) Hit-Testing Projectile Code (Generic)
            // x) Cleanups
            //

            enemyPositions.Dispose(m_TargetingJobHandle);
        }

        private void LateUpdate()
        {
            //
            // NOTE:
            // x) Since we're accessing the Transform of the object, we have to do this
            //    on the main thread. Luckily, the targeting job has enough information to
            //    compute the needed rotation.
            // x) We only depend on the targeting job here since the action of shooting has no
            //    effect on the tower visual.
            //

            m_TargetingJobHandle.Complete();

            for (int towerIdx = 0; towerIdx < m_TargetIndices.Length; ++towerIdx)
            {
                int targetIdx = m_TargetIndices[towerIdx];
                if (targetIdx != 0)
                {
                    Quaternion targetRotation = m_TargetRotations[towerIdx];
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, Time.deltaTime);
                }
            }

            //
            // NOTE:
            // x) There's a way to bypass the dependency between the spawning work and the spawning
            //    request job and it's to simply run this loop one frame too late. For now we'll pay
            //    the cost.
            //

            m_FiringJobHandle.Complete();

            while (m_CreateProjectileQueue.TryDequeue(out int towerIndex))
            {
                var projectileObject = m_ProjectilePool.Get();
                if (projectileObject != null)
                {
                    //
                    // TODO: Actually set the visual for this.
                    //
                }
            }

            m_TargetIndices.Dispose();
            m_CreateProjectileQueue.Dispose();
            m_TargetRotations.Dispose();
        }
    }
}
