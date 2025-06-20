using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeatherSystem : MonoBehaviour
{
    public static WeatherSystem Instance { get; private set; }

    [Header("Weather Settings")]
    [Tooltip("Minimum time between weather changes (in seconds)")]
    public float minWeatherDuration = 60f;
    [Tooltip("Maximum time between weather changes (in seconds)")]
    public float maxWeatherDuration = 300f;
    [Tooltip("How long weather transitions should take (in seconds)")]
    public float transitionDuration = 2f;
    [Tooltip("Minimum intensity for weather effects (0-1)")]
    [Range(0f, 1f)]
    public float minWeatherIntensity = 0.3f;
    [Tooltip("Maximum intensity for weather effects (0-1)")]
    [Range(0f, 1f)]
    public float maxWeatherIntensity = 1f;

    [Header("Weather Effects")]
    [Tooltip("Parent transform for all weather effect objects")]
    public Transform weatherEffectsParent;
    [Tooltip("Prefabs for different weather effects")]
    public Dictionary<WeatherType, GameObject> weatherEffectPrefabs;
    [Tooltip("Audio sources for weather sounds")]
    public Dictionary<WeatherType, AudioSource> weatherAudioSources;
    [Tooltip("Camera to follow for weather effects")]
    public Camera mainCamera;
    [Tooltip("How far above the camera the weather effects should be")]
    public float effectHeight = 50f;
    [Tooltip("How wide the weather effect area should be")]
    public float effectWidth = 100f;
    [Tooltip("How far the weather effect should extend")]
    public float effectDepth = 100f;

    [Header("Debug")]
    public bool debugMode = false;
    public WeatherType debugWeatherType;

    // Current state
    private WeatherType currentWeather;
    private WeatherType targetWeather;
    private float currentWeatherIntensity = 0f;
    private float targetWeatherIntensity = 0f;
    private BiomeData currentBiome;
    private Coroutine weatherTransitionCoroutine;
    private GameObject currentWeatherEffect;
    private AudioSource currentWeatherAudio;
    private ParticleSystem currentParticleSystem;
    private float currentBaseEmissionRate = 1000f; // Default base emission rate

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeWeatherSystem();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeWeatherSystem()
    {
        weatherEffectPrefabs = new Dictionary<WeatherType, GameObject>();
        weatherAudioSources = new Dictionary<WeatherType, AudioSource>();

        // Find main camera if not set
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        // Create weather effects parent if it doesn't exist
        if (weatherEffectsParent == null)
        {
            GameObject parent = new GameObject("WeatherEffects");
            weatherEffectsParent = parent.transform;
            parent.transform.SetParent(transform);
        }

        // Initialize weather effect prefabs
        foreach (WeatherType weatherType in System.Enum.GetValues(typeof(WeatherType)))
        {
            // Load prefabs from Resources folder
            string prefabPath = $"WeatherEffects/{weatherType}Effect";
            GameObject prefab = Resources.Load<GameObject>(prefabPath);
            if (prefab != null)
            {
                weatherEffectPrefabs[weatherType] = prefab;
            }
            else
            {
                Debug.LogWarning($"Could not find weather effect prefab: {prefabPath}");
            }
        }

        // Set initial weather
        currentWeather = WeatherType.Clear;
        targetWeather = WeatherType.Clear;
        currentWeatherIntensity = 0f;
        targetWeatherIntensity = 0f;

        // If in debug mode, set the initial weather
        if (debugMode)
        {
            SetWeather(debugWeatherType);
        }
    }

    private void Start()
    {
        // Set initial weather if in debug mode
        if (debugMode)
        {
            SetWeather(debugWeatherType);
        }
        else
        {
            StartCoroutine(WeatherCycle());
        }
    }

    private void Update()
    {
        // Update current weather effects
        UpdateWeatherEffects();
        
        // Only update position if we have an active weather effect
        if (currentWeatherEffect != null)
        {
            UpdateEffectPosition();
        }
    }

    private void UpdateEffectPosition()
    {
        if (mainCamera == null)
        {
            Debug.LogError("Main camera is null!");
            return;
        }
        
        if (currentWeatherEffect == null)
        {
            return;
        }

        // Position the effect above the camera
        Vector3 cameraPos = mainCamera.transform.position;
        Vector3 newPos = new Vector3(
            cameraPos.x,
            cameraPos.y + effectHeight,
            cameraPos.z
        );
        
        currentWeatherEffect.transform.position = newPos;

        // Update particle system shape if it exists
        if (currentParticleSystem != null)
        {
            var shape = currentParticleSystem.shape;
            shape.scale = new Vector3(effectWidth, 1f, effectDepth);
        }
    }

    private IEnumerator WeatherCycle()
    {
        while (true)
        {
            // Wait for random duration
            float waitTime = Random.Range(minWeatherDuration, maxWeatherDuration);
            yield return new WaitForSeconds(waitTime);

            // Only change weather if not in debug mode
            if (!debugMode)
            {
                ChangeWeather();
            }
        }
    }

    public void SetBiome(BiomeData biome)
    {
        currentBiome = biome;
        // Adjust weather probabilities based on new biome
        ChangeWeather();
    }

    private void ChangeWeather()
    {
        if (currentBiome == null) return;

        // Select new weather based on biome probabilities
        WeatherType newWeather = SelectNewWeather();
        SetWeather(newWeather);
    }

    private WeatherType SelectNewWeather()
    {
        if (currentBiome == null) return WeatherType.Clear;

        float totalProbability = 0f;
        float randomValue = Random.value;

        foreach (WeatherType weatherType in currentBiome.possibleWeatherTypes)
        {
            float probability = currentBiome.GetWeatherProbability(weatherType);
            totalProbability += probability;

            if (randomValue <= totalProbability)
            {
                return weatherType;
            }
        }

        return WeatherType.Clear;
    }

    public void SetWeather(WeatherType newWeather)
    {
        if (newWeather == currentWeather && currentWeatherEffect != null)
        {
            return;
        }

        targetWeather = newWeather;
        if (weatherTransitionCoroutine != null)
        {
            StopCoroutine(weatherTransitionCoroutine);
        }
        weatherTransitionCoroutine = StartCoroutine(TransitionWeather());
    }

    private IEnumerator TransitionWeather()
    {
        float startTime = Time.time;
        float startIntensity = currentWeatherIntensity;
        targetWeatherIntensity = maxWeatherIntensity;

        // Fade out current weather
        while (Time.time < startTime + transitionDuration)
        {
            float t = (Time.time - startTime) / transitionDuration;
            currentWeatherIntensity = Mathf.Lerp(startIntensity, 0f, t);
            yield return null;
        }

        // Switch weather effects
        if (currentWeatherEffect != null)
        {
            Destroy(currentWeatherEffect);
            currentWeatherEffect = null;
        }
        if (currentWeatherAudio != null)
        {
            currentWeatherAudio.Stop();
            currentWeatherAudio = null;
        }

        currentWeather = targetWeather;
        SpawnWeatherEffect(currentWeather);

        // Fade in new weather
        startTime = Time.time;
        startIntensity = minWeatherIntensity;
        while (Time.time < startTime + transitionDuration)
        {
            float t = (Time.time - startTime) / transitionDuration;
            currentWeatherIntensity = Mathf.Lerp(startIntensity, targetWeatherIntensity, t);
            yield return null;
        }

        currentWeatherIntensity = targetWeatherIntensity;
    }

    private void SpawnWeatherEffect(WeatherType weatherType)
    {
        if (weatherEffectPrefabs.TryGetValue(weatherType, out GameObject prefab))
        {
            // Destroy existing effect if any
            if (currentWeatherEffect != null)
            {
                Destroy(currentWeatherEffect);
            }

            // Create new effect
            currentWeatherEffect = Instantiate(prefab, weatherEffectsParent);
            
            // Force position update
            if (mainCamera != null)
            {
                Vector3 cameraPos = mainCamera.transform.position;
                Vector3 newPos = new Vector3(
                    cameraPos.x,
                    cameraPos.y + effectHeight,
                    cameraPos.z
                );
                currentWeatherEffect.transform.position = newPos;
            }

            // Get particle system
            currentParticleSystem = currentWeatherEffect.GetComponentInChildren<ParticleSystem>();
            if (currentParticleSystem != null)
            {
                // Configure particle system for weather type
                ConfigureParticleSystem(weatherType);
                
                // Force particle system to play
                currentParticleSystem.Play();
            }
            else
            {
                Debug.LogError($"No particle system found in weather effect prefab: {weatherType}");
            }

            // Set initial intensity
            SetWeatherEffectIntensity(currentWeatherIntensity);
        }
        else
        {
            Debug.LogError($"No prefab found for weather type: {weatherType}");
        }
    }

    private string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    private void ConfigureParticleSystem(WeatherType weatherType)
    {
        if (currentParticleSystem == null) return;

        var main = currentParticleSystem.main;
        var emission = currentParticleSystem.emission;
        var shape = currentParticleSystem.shape;

        // Force particle system to stop before reconfiguring
        currentParticleSystem.Stop();
        currentParticleSystem.Clear();

        // Disable any modules that might affect emission rate
        var lifetimeByEmitterSpeed = currentParticleSystem.lifetimeByEmitterSpeed;
        lifetimeByEmitterSpeed.enabled = false;

        switch (weatherType)
        {
            case WeatherType.Rain:
                main.startSpeed = 40f;
                main.startSize = 0.05f;
                main.gravityModifier = 1f;
                main.startLifetime = 0.5f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.playOnAwake = true;
                currentBaseEmissionRate = 1000f;
                emission.rateOverTime = currentBaseEmissionRate;
                shape.shapeType = ParticleSystemShapeType.Box;
                break;

            case WeatherType.Snow:
                main.startSpeed = 2f;
                main.startSize = 0.2f;
                main.gravityModifier = 0.1f;
                main.startLifetime = 5f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                currentBaseEmissionRate = 500f;
                emission.rateOverTime = currentBaseEmissionRate;
                shape.shapeType = ParticleSystemShapeType.Box;
                break;

            case WeatherType.Sandstorm:
                main.startSpeed = 15f;
                main.startSize = 0.15f;
                main.gravityModifier = 0.2f;
                main.startLifetime = 3f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                currentBaseEmissionRate = 800f;
                emission.rateOverTime = currentBaseEmissionRate;
                shape.shapeType = ParticleSystemShapeType.Box;
                break;

            case WeatherType.Fog:
                main.startSpeed = 0.5f;
                main.startSize = 5f;
                main.gravityModifier = 0f;
                main.startLifetime = 10f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                currentBaseEmissionRate = 50f;
                emission.rateOverTime = currentBaseEmissionRate;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                break;

            case WeatherType.Thunderstorm:
                main.startSpeed = 45f;
                main.startSize = 0.08f;
                main.gravityModifier = 1.2f;
                main.startLifetime = 0.4f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.playOnAwake = true;
                currentBaseEmissionRate = 1500f;
                emission.rateOverTime = currentBaseEmissionRate;
                shape.shapeType = ParticleSystemShapeType.Box;
                break;

            case WeatherType.AcidRain:
                main.startSpeed = 35f;
                main.startSize = 0.06f;
                main.gravityModifier = 1.1f;
                main.startLifetime = 0.6f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.playOnAwake = true;
                currentBaseEmissionRate = 1200f;
                emission.rateOverTime = currentBaseEmissionRate;
                shape.shapeType = ParticleSystemShapeType.Box;
                break;

            case WeatherType.Blizzard:
                main.startSpeed = 8f;
                main.startSize = 0.3f;
                main.gravityModifier = 0.05f;
                main.startLifetime = 8f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                currentBaseEmissionRate = 800f;
                emission.rateOverTime = currentBaseEmissionRate;
                shape.shapeType = ParticleSystemShapeType.Box;
                break;

            case WeatherType.HeatWave:
                
                break;

            case WeatherType.VolcanicAsh:
                main.startSpeed = 3f;
                main.startSize = 0.4f;
                main.gravityModifier = 0.3f;
                main.startLifetime = 12f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                currentBaseEmissionRate = 400f;
                emission.rateOverTime = currentBaseEmissionRate;
                shape.shapeType = ParticleSystemShapeType.Box;
                break;
        }

        // Set the shape size
        shape.scale = new Vector3(effectWidth, 1f, effectDepth);
        
        // Force particle system to play
        currentParticleSystem.Play();
    }

    private void UpdateWeatherEffects()
    {
        if (currentWeatherEffect != null)
        {
            SetWeatherEffectIntensity(currentWeatherIntensity);
        }
    }

    private void SetWeatherEffectIntensity(float intensity)
    {
        if (currentWeatherEffect == null)
        {
            Debug.LogError("Current weather effect is null!");
            return;
        }

        // Clamp intensity between min and max
        intensity = Mathf.Clamp(intensity, minWeatherIntensity, maxWeatherIntensity);

        // Update particle systems
        if (currentParticleSystem != null)
        {
            var emission = currentParticleSystem.emission;
            float newRate = currentBaseEmissionRate * intensity; 
            emission.rateOverTime = newRate;
        }
        else
        {
            Debug.LogWarning("No particle system found to update intensity");
        }

        // Update audio
        AudioSource audioSource = currentWeatherEffect.GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.volume = intensity;
            if (intensity > minWeatherIntensity && !audioSource.isPlaying)
            {
                audioSource.Play();
            }
            else if (intensity <= minWeatherIntensity && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }
        else
        {
            Debug.LogWarning("No audio source found on weather effect");
        }
    }

    // Public methods for other systems to query weather state
    public WeatherType GetCurrentWeather() => currentWeather;
    public float GetWeatherIntensity() => currentWeatherIntensity;
    public bool IsWeatherActive(WeatherType weatherType) => currentWeather == weatherType && currentWeatherIntensity > 0.1f;
} 