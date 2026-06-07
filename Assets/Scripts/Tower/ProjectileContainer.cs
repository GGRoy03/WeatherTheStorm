using Unity.Collections;
using Unity.Mathematics;
using Unity.XR.OpenVR;
using UnityEngine;

using WeatherTheStorm.Enemy;


namespace WeatherTheStorm.Tower
{
    public class ProjectileContainer
    {
        //
        // Internal Data Storage
        //

        private NativeArray<ProjectileConfig> m_Configs;
        private NativeArray<ProjectileTarget> m_Targets;
        private NativeArray<AABB>             m_LocalBounds;
        private NativeArray<float3>           m_Positions;
        private Transform[]                   m_Transforms;
        private GameObject[]                  m_GameObjects;

        private int m_Count;
        private int m_Capacity;

        //
        // Data Accessors : Controls what data can be modified
        //

        public NativeArray<ProjectileConfig>.ReadOnly Configs     => m_Configs.AsReadOnly();
        public NativeArray<ProjectileTarget>.ReadOnly Targets     => m_Targets.AsReadOnly();
        public NativeArray<AABB>.ReadOnly             LocalBounds => m_LocalBounds.AsReadOnly();
        public NativeArray<float3>                    Positions   => m_Positions;
        public Transform[]                            Transforms  => m_Transforms;
        public int                                    Length      => m_Count;

        public ProjectileContainer(int capacity, GameObject[] pool)
        {
            m_Transforms  = new Transform[capacity];
            m_Configs     = new NativeArray<ProjectileConfig>(capacity, Allocator.Persistent);
            m_LocalBounds = new NativeArray<AABB>(capacity, Allocator.Persistent);
            m_Positions   = new NativeArray<float3>(capacity, Allocator.Persistent);
            m_Targets     = new NativeArray<ProjectileTarget>(capacity, Allocator.Persistent);
            m_GameObjects = pool;
            m_Count       = 0;
            m_Capacity    = capacity;
        }

        ~ProjectileContainer()
        {
            if (m_Configs.IsCreated)     m_Configs.Dispose();
            if (m_LocalBounds.IsCreated) m_LocalBounds.Dispose();
            if (m_Positions.IsCreated)   m_Positions.Dispose();
            if (m_Targets.IsCreated)     m_Targets.Dispose();
        }

        public GameObject CreateProjectile(float3 position, AABB localBounds, EnemyHandle targetHandle)
        {
            GameObject result = null;

            if (m_Count < m_Capacity)
            {
                int index = m_Count++;

                m_Configs[index]     = new(damage: 5.0f);
                m_LocalBounds[index] = localBounds;
                m_Positions[index]   = position;
                m_Targets[index]     = new(handle: targetHandle, velocity: 1.0f);
                m_Transforms[index] = m_GameObjects[index].transform;
                result               = m_GameObjects[index];
            }

            return result;
        }
    };
}