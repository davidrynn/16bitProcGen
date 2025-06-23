using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SimpleWeatherType
{
    Clear,
    Rain,
    Snow,
    Storm,
    Fog
}

[System.Serializable]
public struct WeatherEffect
{
    public SimpleWeatherType weatherType;
    public GameObject effectPrefab;
    public AudioClip soundEffect;
    [Range(0f, 1f)]
    public float intensity;
}

public class WeatherSystem : MonoBehaviour
{
    public static WeatherSystem Instance { get; private set; }

    [Header("Weather Settings")]
    public float weatherChangeInterval = 30f; // Seconds between weather changes
    public float transitionDuration = 2f; // How long transitions take

    [Header("Weather Effects")]
    public WeatherEffect[] weatherEffects;

    [Header("Effect Positioning")]
    public Camera mainCamera;
    public float effectHeight = 10f;
    public float effectWidth = 100f;
    public float effectDepth = 100f;

    [Header("Debug")]
    public bool debugMode = false;
    public SimpleWeatherType debugWeather;

    // Current state
    private SimpleWeatherType currentWeather = SimpleWeatherType.Clear;
    private SimpleWeatherType targetWeather = SimpleWeatherType.Clear;
    private GameObject currentEffect;
    private AudioSource audioSource;
    private BiomeData currentBiome;
    private Coroutine weatherCoroutine;

    // Store original emission rates for proper fading
    private Dictionary<ParticleSystem, float> originalEmissionRates = new Dictionary<ParticleSystem, float>();

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
        // Find main camera if not set
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        // Add audio source if not present
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Set initial weather
        currentWeather = SimpleWeatherType.Clear;
        targetWeather = SimpleWeatherType.Clear;
    }

    private void Start()
    {
        if (debugMode)
        {
            SetWeather(debugWeather);
        }
        else
        {
            StartWeatherCycle();
        }
    }

    private void Update()
    {
        // Update effect position to follow camera
        if (currentEffect != null && mainCamera != null)
        {
            UpdateEffectPosition();
        }
    }

    private void UpdateEffectPosition()
    {
        Vector3 cameraPos = mainCamera.transform.position;
        Vector3 newPos = new Vector3(
            cameraPos.x,
            cameraPos.y + effectHeight,
            cameraPos.z
        );
        
        currentEffect.transform.position = newPos;

        // Update particle system shape if it exists
        var particleSystems = currentEffect.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particleSystems)
        {
            var shape = ps.shape;
            if (shape.enabled)
            {
                shape.scale = new Vector3(effectWidth, 1f, effectDepth);
            }
        }
    }

    public void SetBiome(BiomeData biome)
    {
        currentBiome = biome;

        // Change weather based on biome
        if (currentBiome != null && Random.value < currentBiome.weatherChangeChance)
        {
            SetWeather(currentBiome.defaultWeather);
        }
    }

    public void SetWeather(SimpleWeatherType newWeather)
    {
        if (newWeather == currentWeather) return;

        targetWeather = newWeather;

        if (weatherCoroutine != null)
        {
            StopCoroutine(weatherCoroutine);
        }

        weatherCoroutine = StartCoroutine(TransitionWeather());
    }

    private void StartWeatherCycle()
    {
        StartCoroutine(WeatherCycle());
    }

    private IEnumerator WeatherCycle()
    {
        while (true)
        {
            yield return new WaitForSeconds(weatherChangeInterval);

            if (!debugMode && currentBiome != null)
            {
                // Random weather change based on biome
                if (Random.value < currentBiome.weatherChangeChance)
                {
                    SimpleWeatherType newWeather = GetRandomWeatherForBiome(currentBiome);
                    SetWeather(newWeather);
                }
            }
        }
    }

    private SimpleWeatherType GetRandomWeatherForBiome(BiomeData biome)
    {
        // Simple biome-based weather logic
        switch (biome.biomeType)
        {
            case BiomeType.Desert:
                return Random.value < 0.8f ? SimpleWeatherType.Clear : SimpleWeatherType.Storm;
            case BiomeType.Forest:
                return Random.value < 0.6f ? SimpleWeatherType.Clear : SimpleWeatherType.Rain;
            case BiomeType.Mountains:
                return Random.value < 0.5f ? SimpleWeatherType.Clear : SimpleWeatherType.Snow;
            case BiomeType.Arctic:
                return Random.value < 0.7f ? SimpleWeatherType.Snow : SimpleWeatherType.Clear;
            case BiomeType.Swamp:
                return Random.value < 0.4f ? SimpleWeatherType.Fog : SimpleWeatherType.Rain;
            default:
                return SimpleWeatherType.Clear;
        }
    }

    private IEnumerator TransitionWeather()
    {
        // Fade out current effect
        if (currentEffect != null)
        {
            float fadeTime = 0f;
            while (fadeTime < transitionDuration)
            {
                fadeTime += Time.deltaTime;
                float alpha = 1f - (fadeTime / transitionDuration);
                
                // Fade out particle systems using stored original rates
                var particleSystems = currentEffect.GetComponentsInChildren<ParticleSystem>();
                foreach (var ps in particleSystems)
                {
                    if (originalEmissionRates.ContainsKey(ps))
                    {
                        var emission = ps.emission;
                        emission.rateOverTime = originalEmissionRates[ps] * alpha;
                    }
                }
                
                yield return null;
            }
            
            Destroy(currentEffect);
            currentEffect = null;
            originalEmissionRates.Clear();
        }

        // Fade in new effect
        WeatherEffect effect = GetWeatherEffect(targetWeather);
        if (effect.effectPrefab != null)
        {
            // Create effect at camera position
            Vector3 spawnPos = mainCamera != null ? 
                mainCamera.transform.position + Vector3.up * effectHeight : 
                transform.position;
            
            currentEffect = Instantiate(effect.effectPrefab, spawnPos, Quaternion.identity);
            currentEffect.transform.SetParent(transform);
            
            // Store original emission rates and set to 0 initially
            var particleSystems = currentEffect.GetComponentsInChildren<ParticleSystem>();
            originalEmissionRates.Clear();
            
            foreach (var ps in particleSystems)
            {
                var emission = ps.emission;
                float originalRate = emission.rateOverTime.constant;
                originalEmissionRates[ps] = originalRate;
                emission.rateOverTime = 0f;
            }
            
            float fadeTime = 0f;
            while (fadeTime < transitionDuration)
            {
                fadeTime += Time.deltaTime;
                float alpha = fadeTime / transitionDuration;
                float intensity = effect.intensity <= 0f ? 1f : effect.intensity;
                
                foreach (var ps in particleSystems)
                {
                    if (originalEmissionRates.ContainsKey(ps))
                    {
                        var emission = ps.emission;
                        emission.rateOverTime = originalEmissionRates[ps] * alpha * intensity;
                    }
                }
                
                yield return null;
            }
        }

        // Play sound effect
        if (effect.soundEffect != null && audioSource != null)
        {
            audioSource.clip = effect.soundEffect;
            audioSource.Play();
        }

        currentWeather = targetWeather;
    }

    private WeatherEffect GetWeatherEffect(SimpleWeatherType weatherType)
    {
        foreach (var effect in weatherEffects)
        {
            if (effect.weatherType == weatherType)
            {
                return effect;
            }
        }

        // Return default clear weather effect
        return new WeatherEffect { weatherType = SimpleWeatherType.Clear };
    }

    // Public getters
    public SimpleWeatherType GetCurrentWeather() => currentWeather;
    public bool IsWeatherActive(SimpleWeatherType weatherType) => currentWeather == weatherType;
}