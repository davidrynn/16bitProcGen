using Unity.Entities;
using UnityEngine;

namespace DOTS.Terrain.Trees
{
    /// <summary>
    /// Scene-side bootstrap that registers the TreeRenderConfig managed singleton.
    /// Assign a placeholder mesh (Unity Capsule is fine for MVP) and an unlit material.
    /// Place this MonoBehaviour in the same scene as TerrainBootstrapAuthoring.
    /// </summary>
    public class TreeVisualBootstrap : MonoBehaviour
    {
        [SerializeField] private Mesh     treeMesh;
        [SerializeField] private Material treeMaterial;
        [SerializeField] private float    treeScale = 1f;

        private void Start()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em     = world.EntityManager;
            var entity = em.CreateEntity();
            em.AddComponentObject(entity, new TreeRenderConfig
            {
                Mesh         = treeMesh,
                Material     = treeMaterial,
                UniformScale = treeScale,
            });
        }
    }
}
