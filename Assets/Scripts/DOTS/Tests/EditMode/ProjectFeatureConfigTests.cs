using NUnit.Framework;
using UnityEngine;
using DOTS.Core.Authoring;

namespace DOTS.Tests.EditMode
{
    /// <summary>
    /// R6 P1 contract tests (LANDMARK_DRAW_DISTANCE_SPEC.md): the camera far plane resolves to
    /// max(world reference distance, landmark draw distance), and disabling the landmark feature
    /// (0) collapses to pre-R6 behavior.
    /// </summary>
    [TestFixture]
    public class ProjectFeatureConfigTests
    {
        private const float Tolerance = 0.001f;

        private ProjectFeatureConfig _config;

        [SetUp]
        public void SetUp() => _config = ScriptableObject.CreateInstance<ProjectFeatureConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        [Test]
        public void DerivedLandmarkFarClip_Defaults_RaiseFarPlaneToLandmarkDistance()
        {
            Assert.AreEqual(600f, _config.DerivedCameraFarClip, Tolerance,
                "World reference distance default changed — retune the landmark spec if intentional.");
            Assert.AreEqual(2000f, _config.DerivedLandmarkFarClip, Tolerance);
        }

        [Test]
        public void DerivedLandmarkFarClip_Disabled_CollapsesToWorldReference()
        {
            _config.LandmarkDrawDistance = 0f;

            Assert.AreEqual(_config.DerivedCameraFarClip, _config.DerivedLandmarkFarClip, Tolerance);
        }

        [Test]
        public void DerivedLandmarkFarClip_NeverBelowWorldReference()
        {
            // A landmark distance inside the world edge must not SHRINK the far plane.
            _config.LandmarkDrawDistance = 100f;

            Assert.AreEqual(_config.DerivedCameraFarClip, _config.DerivedLandmarkFarClip, Tolerance);
        }
    }
}
