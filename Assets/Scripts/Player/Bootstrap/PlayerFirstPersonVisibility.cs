using Unity.Entities;
using UnityEngine;
using DOTS.Player.Components;

namespace DOTS.Player.Bootstrap
{
    /// <summary>
    /// Toggles the third-person character body off while the camera is in first-person mode.
    /// In first-person the camera sits inside the head (<see cref="PlayerCameraSettings.FirstPersonOffset"/>),
    /// so the full body clips through the view and the third-person-authored animations have no useful
    /// on-screen effect. MVP is first-person only (see MOVEMENT_PLANNING.md "Camera Perspective"), so the
    /// body is hidden by default; third-person remains a dev/debug toggle (V key) for inspecting animations.
    ///
    /// This is the deliberate seam for the eventual first-person arms viewmodel (the "real fix"): the body
    /// is already hidden in first-person here, so that work only needs to add — and show — a dedicated arms
    /// rig in the same place. No part of this is throwaway.
    /// </summary>
    public class PlayerFirstPersonVisibility : MonoBehaviour
    {
        [Tooltip("The ECS entity whose PlayerCameraSettings.IsThirdPerson drives body visibility.")]
        public Entity TargetEntity;

        private Renderer[] _bodyRenderers;
        private bool _lastThirdPerson;
        private bool _initialized;

        private void Awake()
        {
            // Cache once — the body rig is instantiated before this component is added (see PlayerVisualSpawner).
            _bodyRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        }

        private void LateUpdate()
        {
            if (TargetEntity == Entity.Null)
            {
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var em = world.EntityManager;
            if (!em.Exists(TargetEntity) || !em.HasComponent<PlayerCameraSettings>(TargetEntity))
            {
                return;
            }

            bool thirdPerson = em.GetComponentData<PlayerCameraSettings>(TargetEntity).IsThirdPerson;
            if (_initialized && thirdPerson == _lastThirdPerson)
            {
                return;
            }

            SetBodyVisible(thirdPerson);
            _lastThirdPerson = thirdPerson;
            _initialized = true;
        }

        private void SetBodyVisible(bool visible)
        {
            if (_bodyRenderers == null)
            {
                return;
            }

            foreach (var renderer in _bodyRenderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }
    }
}
