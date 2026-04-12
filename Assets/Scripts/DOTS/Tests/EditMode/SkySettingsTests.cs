using NUnit.Framework;
using UnityEngine;
using DOTS.Rendering.Sky;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class SkySettingsTests
    {
        private const float Tolerance = 0.001f;

        [Test]
        public void Default_HasExpectedValues()
        {
            var s = SkySettings.Default;

            Assert.AreEqual(new Color(0.85f, 0.75f, 0.55f, 1.0f), s.horizonColor);
            Assert.AreEqual(new Color(0.30f, 0.50f, 0.80f, 1.0f), s.zenithColor);
            Assert.AreEqual(1.0f, s.gradientExponent, Tolerance);
            Assert.AreEqual(0.0f, s.horizonHeight, Tolerance);
        }

        // --- Gradient exponent clamping ---

        [Test]
        public void Clamped_ExponentZero_ClampsToMinimum()
        {
            var s = SkySettings.Default;
            s.gradientExponent = 0.0f;
            var c = s.Clamped();
            Assert.AreEqual(0.01f, c.gradientExponent, Tolerance);
        }

        [Test]
        public void Clamped_ExponentNegative_ClampsToMinimum()
        {
            var s = SkySettings.Default;
            s.gradientExponent = -2.0f;
            var c = s.Clamped();
            Assert.AreEqual(0.01f, c.gradientExponent, Tolerance);
        }

        [Test]
        public void Clamped_ExponentAboveMax_ClampsToMax()
        {
            var s = SkySettings.Default;
            s.gradientExponent = 15.0f;
            var c = s.Clamped();
            Assert.AreEqual(10.0f, c.gradientExponent, Tolerance);
        }

        [Test]
        public void Clamped_ExponentInRange_Unchanged()
        {
            var s = SkySettings.Default;
            s.gradientExponent = 3.0f;
            var c = s.Clamped();
            Assert.AreEqual(3.0f, c.gradientExponent, Tolerance);
        }

        // --- Horizon height clamping ---

        [Test]
        public void Clamped_HorizonHeightAboveMax_ClampsToMax()
        {
            var s = SkySettings.Default;
            s.horizonHeight = 1.0f;
            var c = s.Clamped();
            Assert.AreEqual(0.5f, c.horizonHeight, Tolerance);
        }

        [Test]
        public void Clamped_HorizonHeightBelowMin_ClampsToMin()
        {
            var s = SkySettings.Default;
            s.horizonHeight = -1.0f;
            var c = s.Clamped();
            Assert.AreEqual(-0.5f, c.horizonHeight, Tolerance);
        }

        [Test]
        public void Clamped_HorizonHeightInRange_Unchanged()
        {
            var s = SkySettings.Default;
            s.horizonHeight = 0.3f;
            var c = s.Clamped();
            Assert.AreEqual(0.3f, c.horizonHeight, Tolerance);
        }

        // --- Gradient math (mirrors shader logic) ---

        [TestCase(1.0f, 0.5f, 0.5f, Description = "Linear exponent at midpoint")]
        [TestCase(10.0f, 0.5f, 0.000977f, Description = "High exponent at midpoint")]
        [TestCase(2.0f, 0.5f, 0.25f, Description = "Quadratic exponent at midpoint")]
        [TestCase(0.5f, 0.5f, 0.70711f, Description = "Sqrt exponent at midpoint")]
        public void GradientMath_ExponentAtMidpoint(float exponent, float y, float expectedT)
        {
            float h0 = 0.0f;
            float h1 = 1.0f;
            float t = Mathf.Clamp01((y - h0) / (h1 - h0));
            float tPrime = Mathf.Pow(t, Mathf.Clamp(exponent, 0.01f, 10.0f));

            Assert.AreEqual(expectedT, tPrime, Tolerance);
        }

        [Test]
        public void GradientMath_BelowHorizon_ClampedToZero()
        {
            float h0 = 0.0f;
            float h1 = 1.0f;
            float y = -0.5f;
            float t = Mathf.Clamp01((y - h0) / (h1 - h0));

            Assert.AreEqual(0.0f, t, Tolerance);
        }

        [Test]
        public void GradientMath_AtZenith_ClampedToOne()
        {
            float h0 = 0.0f;
            float h1 = 1.0f;
            float y = 1.0f;
            float t = Mathf.Clamp01((y - h0) / (h1 - h0));

            Assert.AreEqual(1.0f, t, Tolerance);
        }

        [Test]
        public void GradientMath_HorizonOffset_ShiftsGradientStart()
        {
            float h0 = 0.3f;
            float h1 = 1.0f;
            float y = 0.3f; // At horizon offset
            float t = Mathf.Clamp01((y - h0) / (h1 - h0));

            Assert.AreEqual(0.0f, t, Tolerance);
        }

        [Test]
        public void GradientMath_NegativeHorizonOffset()
        {
            float h0 = -0.3f;
            float h1 = 1.0f;
            float y = 0.0f;
            float t = Mathf.Clamp01((y - h0) / (h1 - h0));

            // t = (0 - (-0.3)) / (1 - (-0.3)) = 0.3 / 1.3 ≈ 0.2308
            Assert.AreEqual(0.3f / 1.3f, t, Tolerance);
        }

        // --- Color edge cases ---

        [Test]
        public void ColorInterpolation_SameColor_ReturnsSameColor()
        {
            var color = new Color(0.5f, 0.5f, 0.8f, 1.0f);
            float t = 0.5f;
            var result = Color.Lerp(color, color, t);

            Assert.AreEqual(color.r, result.r, Tolerance);
            Assert.AreEqual(color.g, result.g, Tolerance);
            Assert.AreEqual(color.b, result.b, Tolerance);
        }

        [Test]
        public void ColorInterpolation_BlackToWhite_ProducesGrayscale()
        {
            var horizon = new Color(0f, 0f, 0f, 1f);
            var zenith = new Color(1f, 1f, 1f, 1f);
            float t = 0.5f;
            var result = Color.Lerp(horizon, zenith, t);

            Assert.AreEqual(0.5f, result.r, Tolerance);
            Assert.AreEqual(0.5f, result.g, Tolerance);
            Assert.AreEqual(0.5f, result.b, Tolerance);
        }

        [Test]
        public void Clamped_PreservesColors()
        {
            var s = new SkySettings
            {
                horizonColor = Color.red,
                zenithColor = Color.blue,
                gradientExponent = 2.0f,
                horizonHeight = 0.1f
            };

            var c = s.Clamped();
            Assert.AreEqual(Color.red, c.horizonColor);
            Assert.AreEqual(Color.blue, c.zenithColor);
        }

        [Test]
        public void SerializationRoundTrip_FieldsMatch()
        {
            var original = new SkySettings
            {
                horizonColor = new Color(0.1f, 0.2f, 0.3f, 1.0f),
                zenithColor = new Color(0.4f, 0.5f, 0.6f, 1.0f),
                gradientExponent = 2.5f,
                horizonHeight = -0.25f
            };

            var json = JsonUtility.ToJson(original);
            var deserialized = JsonUtility.FromJson<SkySettings>(json);

            Assert.AreEqual(original.horizonColor, deserialized.horizonColor);
            Assert.AreEqual(original.zenithColor, deserialized.zenithColor);
            Assert.AreEqual(original.gradientExponent, deserialized.gradientExponent, Tolerance);
            Assert.AreEqual(original.horizonHeight, deserialized.horizonHeight, Tolerance);
        }

        // --- Phase 2: SkySettings.Lerp ---

        [Test]
        public void Lerp_AtZero_ReturnsFirst()
        {
            var a = new SkySettings
            {
                horizonColor = Color.red,
                zenithColor = Color.blue,
                gradientExponent = 1.0f,
                horizonHeight = -0.2f
            };
            var b = new SkySettings
            {
                horizonColor = Color.green,
                zenithColor = Color.white,
                gradientExponent = 4.0f,
                horizonHeight = 0.3f
            };

            var result = SkySettings.Lerp(a, b, 0f);
            Assert.AreEqual(a.horizonColor, result.horizonColor);
            Assert.AreEqual(a.zenithColor, result.zenithColor);
            Assert.AreEqual(a.gradientExponent, result.gradientExponent, Tolerance);
            Assert.AreEqual(a.horizonHeight, result.horizonHeight, Tolerance);
        }

        [Test]
        public void Lerp_AtOne_ReturnsSecond()
        {
            var a = SkySettings.Default;
            var b = new SkySettings
            {
                horizonColor = Color.black,
                zenithColor = Color.black,
                gradientExponent = 5.0f,
                horizonHeight = 0.4f
            };

            var result = SkySettings.Lerp(a, b, 1f);
            Assert.AreEqual(b.horizonColor, result.horizonColor);
            Assert.AreEqual(b.zenithColor, result.zenithColor);
            Assert.AreEqual(b.gradientExponent, result.gradientExponent, Tolerance);
            Assert.AreEqual(b.horizonHeight, result.horizonHeight, Tolerance);
        }

        [Test]
        public void Lerp_AtHalf_InterpolatesCorrectly()
        {
            var a = new SkySettings
            {
                horizonColor = new Color(0f, 0f, 0f, 1f),
                zenithColor = new Color(0f, 0f, 0f, 1f),
                gradientExponent = 1.0f,
                horizonHeight = 0.0f
            };
            var b = new SkySettings
            {
                horizonColor = new Color(1f, 1f, 1f, 1f),
                zenithColor = new Color(1f, 1f, 1f, 1f),
                gradientExponent = 3.0f,
                horizonHeight = 0.4f
            };

            var result = SkySettings.Lerp(a, b, 0.5f);
            Assert.AreEqual(0.5f, result.horizonColor.r, Tolerance);
            Assert.AreEqual(2.0f, result.gradientExponent, Tolerance);
            Assert.AreEqual(0.2f, result.horizonHeight, Tolerance);
        }

        [Test]
        public void Lerp_ClampsT_AboveOne()
        {
            var a = SkySettings.Default;
            var b = new SkySettings
            {
                horizonColor = Color.black,
                zenithColor = Color.black,
                gradientExponent = 5.0f,
                horizonHeight = 0.4f
            };

            var result = SkySettings.Lerp(a, b, 2.0f);
            Assert.AreEqual(b.gradientExponent, result.gradientExponent, Tolerance);
        }

        [Test]
        public void Lerp_ClampsT_BelowZero()
        {
            var a = SkySettings.Default;
            var b = new SkySettings
            {
                horizonColor = Color.black,
                zenithColor = Color.black,
                gradientExponent = 5.0f,
                horizonHeight = 0.4f
            };

            var result = SkySettings.Lerp(a, b, -1.0f);
            Assert.AreEqual(a.gradientExponent, result.gradientExponent, Tolerance);
        }

        // --- Phase 2: CloudSettings ---

        [Test]
        public void CloudSettings_Default_HasExpectedValues()
        {
            var c = CloudSettings.Default;
            Assert.AreEqual(1f, c.cloudColor.r, Tolerance);
            Assert.AreEqual(0.6f, c.cloudShadowColor.r, Tolerance);
            Assert.AreEqual(0.01f, c.scrollSpeed.x, Tolerance);
            Assert.AreEqual(3f, c.noiseScale, Tolerance);
            Assert.AreEqual(0.45f, c.coverageThreshold, Tolerance);
            Assert.AreEqual(0.15f, c.edgeSoftness, Tolerance);
            Assert.AreEqual(0.6f, c.opacity, Tolerance);
        }

        [Test]
        public void CloudSettings_Lerp_AtHalf()
        {
            var a = new CloudSettings
            {
                cloudColor = Color.white,
                cloudShadowColor = Color.black,
                scrollSpeed = Vector2.zero,
                noiseScale = 2f,
                coverageThreshold = 0f,
                edgeSoftness = 0.1f,
                opacity = 0f
            };
            var b = new CloudSettings
            {
                cloudColor = Color.black,
                cloudShadowColor = Color.white,
                scrollSpeed = new Vector2(0.1f, 0.1f),
                noiseScale = 6f,
                coverageThreshold = 1f,
                edgeSoftness = 0.5f,
                opacity = 1f
            };

            var result = CloudSettings.Lerp(a, b, 0.5f);
            Assert.AreEqual(0.5f, result.cloudColor.r, Tolerance);
            Assert.AreEqual(4f, result.noiseScale, Tolerance);
            Assert.AreEqual(0.5f, result.coverageThreshold, Tolerance);
            Assert.AreEqual(0.5f, result.opacity, Tolerance);
        }
    }
}
