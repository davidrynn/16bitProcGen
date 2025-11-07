using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DOTS.Terrain;
using DOTS.Terrain.Weather;

namespace DOTS.Terrain.Tests
{
    /// <summary>
    /// Automated tests for weather system functionality
    /// New test covering weather features
    /// </summary>
    [TestFixture]
    public class WeatherSystemTests
    {
        private World testWorld;
        private EntityManager entityManager;

        [SetUp]
        public void SetUp()
        {
            testWorld = new World("Weather Test World");
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
        public void WeatherComponent_CanBeCreated()
        {
            var entity = entityManager.CreateEntity();
            
            entityManager.AddComponentData(entity, new WeatherComponent
            {
                weatherType = WeatherType.Rain,
                intensity = 0.5f,
                temperature = 15f,
                humidity = 0.7f
            });
            
            Assert.IsTrue(entityManager.HasComponent<WeatherComponent>(entity),
                "Entity should have WeatherComponent");
        }

        [Test]
        public void WeatherTypes_AllValid()
        {
            var weatherTypes = new[] {
                WeatherType.Clear,
                WeatherType.Rain,
                WeatherType.Snow,
                WeatherType.Storm
            };
            
            foreach (var type in weatherTypes)
            {
                var entity = entityManager.CreateEntity();
                entityManager.AddComponentData(entity, new WeatherComponent
                {
                    weatherType = type,
                    intensity = 1.0f,
                    temperature = 20f,
                    humidity = 0.5f
                });
                
                var component = entityManager.GetComponentData<WeatherComponent>(entity);
                Assert.AreEqual(type, component.weatherType,
                    $"Weather type {type} should be stored correctly");
                
                entityManager.DestroyEntity(entity);
            }
        }

        [Test]
        public void WeatherIntensity_ValidRange()
        {
            // Test that intensity values between 0 and 1 work
            float[] intensities = { 0f, 0.25f, 0.5f, 0.75f, 1.0f };
            
            foreach (var intensity in intensities)
            {
                var entity = entityManager.CreateEntity();
                entityManager.AddComponentData(entity, new WeatherComponent
                {
                    weatherType = WeatherType.Rain,
                    intensity = intensity,
                    temperature = 20f,
                    humidity = 0.5f
                });
                
                var component = entityManager.GetComponentData<WeatherComponent>(entity);
                Assert.AreEqual(intensity, component.intensity, 0.001f,
                    $"Intensity {intensity} should be stored correctly");
                
                entityManager.DestroyEntity(entity);
            }
        }

        [Test]
        public void WeatherTemperature_CanBeSet()
        {
            var entity = entityManager.CreateEntity();
            float temperature = 25.5f;
            
            entityManager.AddComponentData(entity, new WeatherComponent
            {
                weatherType = WeatherType.Clear,
                intensity = 0f,
                temperature = temperature,
                humidity = 0.3f
            });
            
            var component = entityManager.GetComponentData<WeatherComponent>(entity);
            Assert.AreEqual(temperature, component.temperature, 0.001f,
                "Temperature should be stored correctly");
        }

        [Test]
        public void WeatherHumidity_ValidRange()
        {
            // Test humidity values between 0 and 1
            float[] humidities = { 0f, 0.3f, 0.6f, 1.0f };
            
            foreach (var humidity in humidities)
            {
                var entity = entityManager.CreateEntity();
                entityManager.AddComponentData(entity, new WeatherComponent
                {
                    weatherType = WeatherType.Rain,
                    intensity = 0.5f,
                    temperature = 20f,
                    humidity = humidity
                });
                
                var component = entityManager.GetComponentData<WeatherComponent>(entity);
                Assert.AreEqual(humidity, component.humidity, 0.001f,
                    $"Humidity {humidity} should be stored correctly");
                
                entityManager.DestroyEntity(entity);
            }
        }

        [Test]
        public void WeatherState_CanBeChanged()
        {
            var entity = entityManager.CreateEntity();
            
            // Start with Clear weather
            entityManager.AddComponentData(entity, new WeatherComponent
            {
                weatherType = WeatherType.Clear,
                intensity = 0f,
                temperature = 20f,
                humidity = 0.3f
            });
            
            // Change to Rain
            var weather = entityManager.GetComponentData<WeatherComponent>(entity);
            weather.weatherType = WeatherType.Rain;
            weather.intensity = 0.8f;
            weather.humidity = 0.9f;
            entityManager.SetComponentData(entity, weather);
            
            var updated = entityManager.GetComponentData<WeatherComponent>(entity);
            Assert.AreEqual(WeatherType.Rain, updated.weatherType,
                "Weather should be updated to Rain");
            Assert.AreEqual(0.8f, updated.intensity, 0.001f,
                "Intensity should be updated");
        }

        [Test]
        public void MultipleWeatherZones_CanCoexist()
        {
            // Test multiple weather zones with different conditions
            var zone1 = entityManager.CreateEntity();
            var zone2 = entityManager.CreateEntity();
            
            entityManager.AddComponentData(zone1, new WeatherComponent
            {
                weatherType = WeatherType.Clear,
                intensity = 0f,
                temperature = 25f,
                humidity = 0.4f
            });
            
            entityManager.AddComponentData(zone2, new WeatherComponent
            {
                weatherType = WeatherType.Storm,
                intensity = 1.0f,
                temperature = 10f,
                humidity = 1.0f
            });
            
            var weather1 = entityManager.GetComponentData<WeatherComponent>(zone1);
            var weather2 = entityManager.GetComponentData<WeatherComponent>(zone2);
            
            Assert.AreNotEqual(weather1.weatherType, weather2.weatherType,
                "Different zones should have different weather");
            Assert.AreNotEqual(weather1.intensity, weather2.intensity,
                "Different zones should have different intensities");
        }
    }
}

