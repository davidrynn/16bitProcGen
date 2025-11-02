// CameraFollowSystem.cs
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(TransformSystemGroup))]
public partial struct CameraFollowSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<MainCameraTag>();
        state.RequireForUpdate<PlayerTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        // Identify the baked camera entity once per frame
        var cameraEntity = SystemAPI.GetSingletonEntity<MainCameraTag>();

        // Get the player entity (tagged) and read its LocalToWorld
        var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
        var playerLtw = state.EntityManager.GetComponentData<LocalToWorld>(playerEntity);

        // Extract world position and rotation from LocalToWorld
        float3 playerPos = playerLtw.Value.c3.xyz;
        float3x3 r3x3 = new float3x3(playerLtw.Value.c0.xyz, playerLtw.Value.c1.xyz, playerLtw.Value.c2.xyz);
        quaternion playerRot = new quaternion(r3x3);

        // Compute a simple follow (3rd-person) target
        // Tune these for your game
        float3 followOffset = new float3(0f, 2f, -4f);

        // Face the player; or use player forward if you prefer an over-the-shoulder aim
        float3 targetPos = playerPos + math.mul(playerRot, followOffset);
        float3 dir       = math.normalizesafe(playerPos - targetPos, new float3(0,0,1));
        quaternion rot   = quaternion.LookRotationSafe(dir, math.up());

        // Optional smoothing
        float dt = SystemAPI.Time.DeltaTime;
        const float posLerp = 12f, rotLerp = 16f;

        var camXform = state.EntityManager.GetComponentData<LocalTransform>(cameraEntity);
        camXform.Position = math.lerp(camXform.Position, targetPos, 1f - math.exp(-posLerp * dt));
        camXform.Rotation = math.slerp(camXform.Rotation, rot,       1f - math.exp(-rotLerp * dt));
        state.EntityManager.SetComponentData(cameraEntity, camXform);
    }
}
