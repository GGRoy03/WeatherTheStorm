using UnityEngine;

using Unity.Collections;
using Unity.Jobs;

using WeatherTheStorm.Enemy;

//
// TODO:
// x) Fix the crash when the projectile reaches its position
// x) Do the collision stuff
// x) Figure out dependency stuff
// x) Figure out audio stuff
// x) Figure out pools... do it the dirty way I think...
// x) Enemy damage.
// x) Then we're done?
//


namespace WeatherTheStorm.Tower
{
    public class TowerManager : MonoBehaviour
    {
        [Header("Prototyping")]
        [SerializeField] private EnemyManager        m_StubEnemyManager;
        [SerializeField] private TowerAuthoring      m_StubTower;
        [SerializeField] private ProjectileAuthoring m_StubProjectile;


        [Header("Scale")]
        [SerializeField] private int                 m_MaximumTowerCount;
        [SerializeField] private int                 m_MaximumProjectileCount;
                         private ProjectileContainer m_ProjectileContainer;
                         private TowerContainer      m_TowerContainer;

        //
        // NOTE:
        // x) I don't really like this. The annoying part is since we don't own the game loop,
        //    there's no other way to pipe the data to the late update, I have to just store it
        //    on the class here I guess..
        //

        private JobHandle               m_TargetingJobHandle;
        private JobHandle               m_FiringJobHandle;
        private JobHandle               m_UpdateProjectilePositionJobHandle;
        private NativeArray<int>        m_TargetIndices;
        private NativeArray<Quaternion> m_TargetRotations;
        private NativeQueue<int>        m_CreateProjectileQueue;

        void Start()
        {
            //
            // NOTE:
            // x) A Projectile Prototype is basically the agnostic way to represent a projectile
            //    visually or logically. The components are simply  swapped depending on the config
            //    when a projectile is instantiated. This allows us to have a really simple pool
            //    of projectile objects.
            //
            // TODO:
            // x) There's a way to batch instantiate right?
            // x) Should be appended to the correct parent.
            //

            var projectilePrototype = new GameObject("Projectile Prototype", typeof(MeshFilter), typeof(MeshRenderer));
            var projectileObjects   = new GameObject[m_MaximumProjectileCount];
            for (int projectileIdx = 0; projectileIdx < m_MaximumProjectileCount; ++projectileIdx)
            {
                projectileObjects[projectileIdx] = Instantiate(projectilePrototype);
            }
            m_ProjectileContainer = new(m_MaximumProjectileCount, projectileObjects);

            var towerPrototype = new GameObject("Tower Prototype", typeof(MeshFilter), typeof(MeshRenderer));
            var towerObjects   = new GameObject[m_MaximumTowerCount];
            for (int towerIdx = 0; towerIdx < m_MaximumTowerCount; ++towerIdx)
            {
                towerObjects[towerIdx] = Instantiate(towerPrototype);
            }
            m_TowerContainer = new(m_MaximumTowerCount, towerObjects);

            //
            // NOTE:
            // x) Prototyping code to initialize a couple of towers.
            //

            for(int towerIdx = 0; towerIdx < 10; ++towerIdx)
            {
                var position    = new Vector3(UnityEngine.Random.Range(-5.0f, 5.0f), 0.0f, UnityEngine.Random.Range(-5.0f, 5.0f));
                var towerObject = m_TowerContainer.CreateTower(position);

                if(towerObject != null)
                {
                    var meshFilter = towerObject.GetComponent<MeshFilter>();
                    meshFilter.mesh = m_StubTower.m_Mesh;

                    var meshRenderer = towerObject.GetComponent<MeshRenderer>();
                    meshRenderer.materials = m_StubTower.m_Materials;

                    towerObject.transform.position = position;
                }
            }
        }

        void Update()
        {
            //
            // And I guess we just unpack our dependency data here?
            //

            var enemyPositions = m_StubEnemyManager.GetEnemyPositions();
            int towerCount     = m_TowerContainer.Length;

            //
            // TowerTargetingJob ....
            //

            m_TargetIndices      = new NativeArray<int>(towerCount, Allocator.TempJob);
            m_TargetRotations    = new NativeArray<Quaternion>(towerCount, Allocator.TempJob);
            m_TargetingJobHandle = new TowerTargetingJob
            {
                TowerPositions  = m_TowerContainer.Positions,
                TowerTargetings = m_TowerContainer.Targetings,
                EnemyPositions  = enemyPositions,
                TargetIndices   = m_TargetIndices,
                TargetRotations = m_TargetRotations,
            }.Schedule(towerCount, 32);

            //
            // TowerTryFireJob ....
            //

            m_CreateProjectileQueue = new NativeQueue<int>(Allocator.TempJob);
            m_FiringJobHandle       = new TowerTryFireJob
            {
                TargetIndices         = m_TargetIndices,
                Cooldowns             = m_TowerContainer.Cooldowns,
                DeltaTime             = Time.deltaTime,
                CreateProjectileQueue = m_CreateProjectileQueue.AsParallelWriter(),
            }.Schedule(towerCount, 32, m_TargetingJobHandle);

            //
            // ...
            //

            m_UpdateProjectilePositionJobHandle = new UpdateProjectilePositionJob()
            {
                ProjectileTargets   = m_ProjectileContainer.Targets,
                EnemyPositions      = enemyPositions,
                ProjectilePositions = m_ProjectileContainer.Positions,
                DeltaTime           = Time.deltaTime
            }.Schedule(m_ProjectileContainer.Length, 32);


            //var CheckProjectileCollisionJob = new CheckProjectileCollisionJob()
            //{
            //    EnemyBVH    = new EnemyBoundingVolumeHierarchy(),
            //    Configs     = m_ProjectileContainer.Configs,
            //    LocalBounds = m_ProjectileContainer.LocalBounds,
            //    Positions   = m_ProjectileContainer.Positions.AsReadOnly(),
            //}.Schedule(m_ProjectileContainer.Length, 32);
        }

        private void SynchronizeTowerRotation()
        {
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
        }

        private void SynchronizeProjectilePosition()
        {
            m_UpdateProjectilePositionJobHandle.Complete();

            var transforms          = m_ProjectileContainer.Transforms;
            var projectilePositions = m_ProjectileContainer.Positions;
            for (int projectileIdx = 0; projectileIdx < m_ProjectileContainer.Length; ++projectileIdx)
            {
                transforms[projectileIdx].position = projectilePositions[projectileIdx];
            }
        }

        private void SynchronizeProjectileCreation()
        {
            m_FiringJobHandle.Complete();

            var enemyHandles = m_StubEnemyManager.GetEnenyHandles();
            while (m_CreateProjectileQueue.TryDequeue(out int towerIndex))
            {
                var towerPosition = m_TowerContainer.Positions[towerIndex];
                var targetHandle  = enemyHandles[towerIndex];
                var projectile    = m_ProjectileContainer.CreateProjectile(towerPosition, m_StubProjectile.m_LocalBounds, targetHandle);

                if(projectile != null)
                {
                    //
                    // NOTE:
                    // This would be done from some sort of config table instead of trying to
                    // read a bunch of components. The pools are the issue....
                    //

                    var meshFilter = projectile.GetComponent<MeshFilter>();
                    meshFilter.mesh = m_StubProjectile.m_Mesh;

                    var meshRenderer = projectile.GetComponent<MeshRenderer>();
                    meshRenderer.materials = m_StubProjectile.m_Materials;
                }
            }
        }

        private void LateUpdate()
        {
            SynchronizeTowerRotation();
            SynchronizeProjectilePosition();
            SynchronizeProjectileCreation();

            m_TargetIndices.Dispose();
            m_CreateProjectileQueue.Dispose();
            m_TargetRotations.Dispose();
        }
    }
}