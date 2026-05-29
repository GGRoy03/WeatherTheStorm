using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private int      m_EntityCount;
    [SerializeField] private Mesh     m_EnemyMesh;
    [SerializeField] private Material m_EnemyMaterial;

    private EntityManager m_EntityManager;


    void Start()
    {
        //m_EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        //var desc = new RenderMeshDescription(
        //    shadowCastingMode: ShadowCastingMode.On,
        //    receiveShadows: true
        //);

        //var renderMeshArray = new RenderMeshArray(
        //    new Material[] { m_EnemyMaterial },
        //    new Mesh[] { m_EnemyMesh }
        //);

        //var prototype = m_EntityManager.CreateEntity();
        //RenderMeshUtility.AddComponents(
        //    prototype,
        //    m_EntityManager,
        //    desc,
        //    renderMeshArray,
        //    MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0)
        //);
        //m_EntityManager.AddComponent<PathAgent>(prototype);
        //m_EntityManager.AddComponent<LocalTransform>(prototype);

        //using var entities = m_EntityManager.Instantiate(
        //    prototype, m_EntityCount, Unity.Collections.Allocator.Temp
        //);
        //m_EntityManager.DestroyEntity(prototype);

        //for (int i = 0; i < entities.Length; i++)
        //{
        //    m_EntityManager.SetComponentData(entities[i], LocalTransform.FromPosition(
        //        new float3(
        //            UnityEngine.Random.Range(-50f, 50f),
        //            0f,
        //            UnityEngine.Random.Range(-50f, 50f)
        //        )
        //    ));
        //    m_EntityManager.SetComponentData(entities[i], new PathAgent
        //    {
        //        Destination = float3.zero,
        //        Speed = 2f
        //    });
        //}
    }
}
