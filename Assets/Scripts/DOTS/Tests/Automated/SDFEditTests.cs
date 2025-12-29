using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using DOTS.Terrain;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class SDFEditTests
    {
        private World testWorld;
        private EntityManager entityManager;

        [SetUp]
        public void SetUp()
        {
            testWorld = new World("SDFEdit Test World");
            entityManager = testWorld.EntityManager;
        }

        [TearDown]
        public void TearDown()
        {
            if (testWorld != null && testWorld.IsCreated)
            {
                testWorld.Dispose();
            }
        }

        [Test]
        public void DynamicBuffer_StoresAddAndSubtractOperations()
        {
            var entity = entityManager.CreateEntity();
            var buffer = entityManager.AddBuffer<SDFEdit>(entity);

            buffer.Add(new SDFEdit
            {
                Center = new float3(1f, 2f, 3f),
                Radius = 4f,
                Operation = SDFEditOperation.Add
            });

            buffer.Add(new SDFEdit
            {
                Center = new float3(-2f, 0.5f, 6f),
                Radius = 2.5f,
                Operation = SDFEditOperation.Subtract
            });

            Assert.AreEqual(2, buffer.Length, "Buffer should contain two edits");
            Assert.AreEqual(SDFEditOperation.Add, buffer[0].Operation);
            Assert.AreEqual(SDFEditOperation.Subtract, buffer[1].Operation);
            Assert.AreEqual(new float3(1f, 2f, 3f), buffer[0].Center);
            Assert.AreEqual(4f, buffer[0].Radius);
        }

        [Test]
        public void BufferCopyToNativeArray_PreservesValues()
        {
            var entity = entityManager.CreateEntity();
            var buffer = entityManager.AddBuffer<SDFEdit>(entity);
            buffer.Add(new SDFEdit
            {
                Center = float3.zero,
                Radius = 3f,
                Operation = SDFEditOperation.Add
            });

            using var edits = new NativeArray<SDFEdit>(buffer.Length, Allocator.Temp);
            buffer.AsNativeArray().CopyTo(edits);

            Assert.AreEqual(buffer[0].Center, edits[0].Center);
            Assert.AreEqual(buffer[0].Radius, edits[0].Radius);
            Assert.AreEqual(buffer[0].Operation, edits[0].Operation);
        }
    }
}
