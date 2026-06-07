using Unity.Collections;
using Unity.Mathematics;

using UnityEngine;



namespace WeatherTheStorm.Tower
{
    public class TowerContainer
    {
        //
        // Internal Data Storage
        //

        private NativeArray<float3>         m_TowerPositions;
        private NativeArray<TowerTargeting> m_TowerTargetings;
        private NativeArray<TowerCooldown>  m_TowerCooldowns;
        private Transform[]                 m_TowerTransforms;
        private GameObject[]                m_GameObjects;
        private int                         m_Count;
        private readonly int                m_Capacity;

        //
        // Data Accessors : Controls what data can be modified
        //

        public NativeArray<float3>.ReadOnly         Positions  => m_TowerPositions.AsReadOnly();
        public NativeArray<TowerTargeting>.ReadOnly Targetings => m_TowerTargetings.AsReadOnly();
        public NativeArray<TowerCooldown>           Cooldowns  => m_TowerCooldowns;
        public Transform[]                          Transforms => m_TowerTransforms;
        public int                                  Length     => m_Count;


        public TowerContainer(int capacity, GameObject[] pool)
        {
            m_TowerPositions  = new NativeArray<float3>        (capacity, Allocator.Persistent);
            m_TowerCooldowns  = new NativeArray<TowerCooldown> (capacity, Allocator.Persistent);
            m_TowerTargetings = new NativeArray<TowerTargeting>(capacity, Allocator.Persistent);
            m_TowerTransforms = new Transform[capacity];
            m_GameObjects     = pool;
            m_Count           = 0;
            m_Capacity        = capacity;
        }

        ~TowerContainer()
        {
            if (m_TowerPositions.IsCreated)  m_TowerPositions.Dispose();
            if (m_TowerTargetings.IsCreated) m_TowerTargetings.Dispose();
            if (m_TowerCooldowns.IsCreated)  m_TowerCooldowns.Dispose();
        }

        public GameObject CreateTower(float3 position)
        {
            GameObject result = null;

            if(m_Count < m_Capacity)
            {
                int index       = m_Count++;
                var towerObject = m_GameObjects[index];

                m_TowerCooldowns[index]  = new(duration: 0.0f);
                m_TowerPositions[index]  = position;
                m_TowerTargetings[index] = new(mode: TargetMode.Closest, sqrRange: 5.0f);
                m_TowerTransforms[index] = towerObject.transform;

                result = towerObject;
            }

            return result;
        }
    }
}