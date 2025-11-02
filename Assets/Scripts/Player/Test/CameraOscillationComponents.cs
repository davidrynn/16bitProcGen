using Unity.Entities;

namespace DOTS.Player.Test
{
    public struct CameraOscillationTag : IComponentData {}

    public struct CameraOscillationSettings : IComponentData
    {
        public float Amplitude;
        public float Frequency;
    }
    
    public struct VirtualCameraLink : IComponentData
    {
        public Entity CameraEntity;
    }
}