using Unity.Entities;
using DOTS.Terrain.WFC;

namespace DOTS.Terrain.WFC.Authoring
{
    public class DungeonElementBaker : Baker<DungeonElementAuthoring>
    {
        public override void Bake(DungeonElementAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Renderable);

            AddComponent(entity, new DungeonElementComponent
            {
                elementType = authoring.elementType
            });

            // This prefab is intended for runtime instantiation as an entity prefab
            AddComponent<Prefab>(entity);
        }
    }
}


