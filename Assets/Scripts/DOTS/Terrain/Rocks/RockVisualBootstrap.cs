using Unity.Entities;
using UnityEngine;
using DOTS.Terrain.SurfaceScatter;

namespace DOTS.Terrain.Rocks
{
    /// <summary>
    /// Scene-side bootstrap that registers the RockRenderConfig managed singleton.
    /// Assign a rock mesh and material in the inspector and place this in active bootstrap scenes.
    /// </summary>
    public class RockVisualBootstrap : MonoBehaviour
    {
        [SerializeField] private Mesh rockMesh;
        [SerializeField] private Material rockMaterial;
        [SerializeField] private float rockScale = 1f;

        private void Start()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                return;
            }

            var em = world.EntityManager;
            SurfaceScatterRenderConfigBootstrapUtility.SetOrCreateManagedSingleton(
                em,
                new RockRenderConfig
                {
                    Mesh = rockMesh,
                    Material = rockMaterial,
                    UniformScale = rockScale,
                },
                nameof(RockRenderConfig));
        }
    }
}