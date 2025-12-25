// CameraFollowSystem.cs
// Simple test system for basic camera following without PlayerCameraLink
// Only activates when PlayerCameraLink components are NOT present (test scenarios only)
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DOTS.Player.Components;

    [DisableAutoCreation]
    [BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(TransformSystemGroup))]
public partial struct CameraFollowSystem : ISystem
{
    private EntityQuery playerCameraLinkQuery;
    private EntityQuery cameraQuery;
    private EntityQuery playerQuery;
    
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<MainCameraTag>();
        state.RequireForUpdate<PlayerTag>();
        
        // Create queries once
        playerCameraLinkQuery = state.GetEntityQuery(ComponentType.ReadOnly<PlayerCameraLink>());
        cameraQuery = state.GetEntityQuery(ComponentType.ReadOnly<MainCameraTag>());
        playerQuery = state.GetEntityQuery(ComponentType.ReadOnly<PlayerTag>());
    }

    public void OnUpdate(ref SystemState state)
    {
        // Disable this system if PlayerCameraLink components exist (production player system is active)
        if (!playerCameraLinkQuery.IsEmpty)
        {
            state.Enabled = false;
            return;
        }
        
        // This system is for simple test scenarios only
        // Check if we have exactly one camera and player before proceeding
        int cameraCount = cameraQuery.CalculateEntityCount();
        int playerCount = playerQuery.CalculateEntityCount();
        
        if (cameraCount != 1 || playerCount != 1)
        {
            // Wrong number of cameras or players - skip this frame (don't disable, might be temporary)
            return;
        }
        
        // Identify the baked camera entity once per frame
        var cameraEntity = SystemAPI.GetSingletonEntity<MainCameraTag>();

        // Get the player entity (tagged) and read its components
        var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
        var playerTransform = state.EntityManager.GetComponentData<LocalTransform>(playerEntity);
        float3 playerPos = playerTransform.Position;

        // Get player's view angles (yaw and pitch) from PlayerViewComponent
        if (!state.EntityManager.HasComponent<PlayerViewComponent>(playerEntity))
        {
            UnityEngine.Debug.LogWarning("[CameraFollowSystem] Player entity missing PlayerViewComponent!");
            return;
        }
        
        var playerView = state.EntityManager.GetComponentData<PlayerViewComponent>(playerEntity);
        
        // Apply only yaw rotation to camera position offset (keeps camera at consistent height)
        quaternion yawRotation = quaternion.AxisAngle(math.up(), math.radians(playerView.YawDegrees));
        
        // Camera offset: behind and above player (like a head position)
        float3 followOffset = new float3(0f, 1.6f, -3f); // Head height (~1.6m) and 3m back
        float3 targetPos = playerPos + math.mul(yawRotation, followOffset);

        // Camera rotation: combine yaw and pitch for camera viewing angle
        quaternion pitchRotation = quaternion.AxisAngle(math.right(), math.radians(playerView.PitchDegrees));
        quaternion combinedRotation = math.mul(yawRotation, pitchRotation);
        quaternion rot = combinedRotation;

        // Optional smoothing
        float dt = SystemAPI.Time.DeltaTime;
        
        var camXform = state.EntityManager.GetComponentData<LocalTransform>(cameraEntity);
        
        // In test mode, DeltaTime is often 0, so skip smoothing and snap to target
        if (dt < 0.001f)
        {
            // Instant snap for tests
            camXform.Position = targetPos;
            camXform.Rotation = rot;
        }
        else
        {
            // Normal smoothing for runtime
            const float posLerp = 12f, rotLerp = 16f;
            float posBlend = 1f - math.exp(-posLerp * dt);
            float rotBlend = 1f - math.exp(-rotLerp * dt);
            camXform.Position = math.lerp(camXform.Position, targetPos, posBlend);
            camXform.Rotation = math.slerp(camXform.Rotation, rot, rotBlend);
        }
        
        state.EntityManager.SetComponentData(cameraEntity, camXform);
        
        // Also update LocalToWorld for consistency (needed in test scenarios without full transform hierarchy)
        if (state.EntityManager.HasComponent<LocalToWorld>(cameraEntity))
        {
            state.EntityManager.SetComponentData(cameraEntity, new LocalToWorld
            {
                Value = float4x4.TRS(camXform.Position, camXform.Rotation, new float3(camXform.Scale))
            });
        }
    }
}
