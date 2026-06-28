using System.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using DOTS.Player.Components;

namespace DOTS.Player.Bootstrap
{
    /// <summary>
    /// Waits for the ECS player entity to exist, then instantiates the character prefab and wires
    /// PlayerVisualSync + PlayerAnimatorBridge. Replaces the debug capsule from PlayerEntityBootstrap.
    /// Add this to the Bootstrap GameObject and assign a character FBX or prefab in the Inspector.
    /// </summary>
    public class PlayerVisualSpawner : MonoBehaviour
    {
        [Tooltip("Assign the character FBX or prefab to use as the player visual (e.g. Assets/Models/Rigged Humanoid.heavier.fbx)")]
        [SerializeField] private GameObject characterPrefab;

        [Tooltip("Matches PlayerMovementConfig.GroundSpeed — used to normalize the Speed blend parameter.")]
        [SerializeField] private float runSpeed = 10f;

        [Tooltip("Local rotation applied to the character child after instantiation. Use Y=180 to flip a Blender model that spawns backwards.")]
        [SerializeField] private Vector3 characterRotationOffset = Vector3.zero;

        [Tooltip("Animator Controller to drive character animations. Build via Tools > Player > Build Animator Controller.")]
        [SerializeField] private RuntimeAnimatorController animatorController;

        [Tooltip("When enabled, the runtime animator bridge logs parameter writes, trigger dispatches, and state transitions.")]
        [SerializeField] private bool enableAnimatorDebugLogging;

        private IEnumerator Start()
        {
            // PlayerEntityBootstrap runs in InitializationSystemGroup on the first ECS update,
            // which happens after MonoBehaviour.Start() — yield one frame to let it run.
            yield return null;

            const float timeoutSeconds = 15f;
            float elapsed = 0f;
            Entity playerEntity = Entity.Null;

            while (elapsed < timeoutSeconds)
            {
                // Re-read each frame — world reference can change after domain reload.
                var world = World.DefaultGameObjectInjectionWorld;
                if (world != null && world.IsCreated)
                {
                    using var query = world.EntityManager.CreateEntityQuery(
                        ComponentType.ReadOnly<PlayerTag>());
                    int count = query.CalculateEntityCount();
                    if (count == 1)
                    {
                        playerEntity = query.GetSingletonEntity();
                        break;
                    }
                    if (count > 1)
                    {
                        Debug.LogWarning("[PlayerVisualSpawner] Multiple PlayerTag entities found — taking first. Visual may target wrong entity.");
                        playerEntity = query.ToEntityArray(Unity.Collections.Allocator.Temp)[0];
                        break;
                    }
                }

                yield return null;
                elapsed += Time.deltaTime;
            }

            if (playerEntity == Entity.Null)
            {
                Debug.LogError("[PlayerVisualSpawner] Timed out waiting for PlayerTag entity. Is PlayerEntityBootstrap enabled in ProjectFeatureConfig?");
                yield break;
            }

            var spawnWorld = World.DefaultGameObjectInjectionWorld;
            if (spawnWorld == null || !spawnWorld.IsCreated)
            {
                Debug.LogError("[PlayerVisualSpawner] ECS world was destroyed before visual could be spawned.");
                yield break;
            }
            SpawnVisual(spawnWorld.EntityManager, playerEntity);
        }

        private void SpawnVisual(EntityManager em, Entity playerEntity)
        {
            if (!em.Exists(playerEntity) || !em.HasComponent<LocalTransform>(playerEntity))
            {
                Debug.LogError("[PlayerVisualSpawner] Player entity is invalid at spawn time — visual skipped.");
                return;
            }
            var transform = em.GetComponentData<LocalTransform>(playerEntity);
            GameObject root;

            if (characterPrefab != null)
            {
                root = new GameObject("Player Visual");
                root.transform.SetPositionAndRotation(transform.Position, transform.Rotation);

                var character = Instantiate(characterPrefab, root.transform);
                character.name = characterPrefab.name;
                character.transform.localPosition = Vector3.zero;
                character.transform.localRotation = Quaternion.Euler(characterRotationOffset);

                // Strip CharacterController so Unity's PhysX doesn't fight PlayerVisualSync each frame.
                var cc = character.GetComponentInChildren<CharacterController>();
                if (cc != null) Destroy(cc);

                var animator = character.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    if (animatorController != null)
                        animator.runtimeAnimatorController = animatorController;

                    // Disable root motion — position is driven entirely by PlayerVisualSync.
                    animator.applyRootMotion = false;

                    var bridge = root.AddComponent<PlayerAnimatorBridge>();
                    bridge.TargetEntity = playerEntity;
                    bridge.CharacterAnimator = animator;
                    bridge.RunSpeed = runSpeed;
                    bridge.EnableDebugLogging = enableAnimatorDebugLogging;
                }
                else
                {
                    Debug.LogWarning("[PlayerVisualSpawner] No Animator on character prefab — bridge not wired. Assign an Animator Controller to the prefab for Phase B animations.");
                }
            }
            else
            {
                // Fallback so Play Mode still works without the prefab assigned
                root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                root.name = "Player Visual (Capsule Fallback)";
                root.transform.SetPositionAndRotation(transform.Position, transform.Rotation);
                root.GetComponent<Renderer>().material.color = new Color(0.2f, 0.4f, 1f);
                Destroy(root.GetComponent<UnityEngine.Collider>());
                Debug.LogWarning("[PlayerVisualSpawner] characterPrefab not assigned — using capsule fallback. Assign SM_Chr_Male_01.prefab in the Inspector.");
            }

            var sync = root.AddComponent<PlayerVisualSync>();
            sync.targetEntity = playerEntity;
            sync.visualOffset = Vector3.zero;

            // Hide the body in first-person (MVP default) so it doesn't clip through the head-mounted
            // camera. Wired for both the prefab and capsule-fallback bodies. This is the seam the future
            // first-person arms viewmodel hangs off — see PlayerFirstPersonVisibility.
            var fpVisibility = root.AddComponent<PlayerFirstPersonVisibility>();
            fpVisibility.TargetEntity = playerEntity;
        }
    }
}
