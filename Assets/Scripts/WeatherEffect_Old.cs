using UnityEngine;

public class WeatherEffect_Old : MonoBehaviour
{
    [Header("Effect Settings")]
    public WeatherType weatherType;
    public float baseIntensity = 1f;
    public bool affectsLighting = true;
    public bool affectsFog = true;
    public bool affectsParticles = true;
    public bool affectsAudio = true;

    [Header("Lighting Settings")]
    public Color weatherLightColor = Color.white;
    public float weatherLightIntensity = 1f;
    public float weatherAmbientIntensity = 1f;

    [Header("Fog Settings")]
    public Color weatherFogColor = Color.white;
    public float weatherFogDensity = 0.01f;

    [Header("Particle Settings")]
    public float particleEmissionRate = 100f;
    public float particleSpeed = 1f;
    public float particleSize = 1f;

    [Header("Audio Settings")]
    public AudioClip weatherSound;
    [Range(0f, 1f)]
    public float weatherVolume = 0.5f;
    public float weatherPitch = 1f;

    private ParticleSystem[] particleSystems;
    private AudioSource audioSource;
    private Light weatherLight;
    private float currentIntensity = 0f;

    private void Awake()
    {
        // Get components
        particleSystems = GetComponentsInChildren<ParticleSystem>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Setup audio
        if (weatherSound != null)
        {
            audioSource.clip = weatherSound;
            audioSource.volume = weatherVolume;
            audioSource.pitch = weatherPitch;
            audioSource.loop = true;
        }

        // Setup light if needed
        if (affectsLighting)
        {
            weatherLight = GetComponent<Light>();
            if (weatherLight == null)
            {
                weatherLight = gameObject.AddComponent<Light>();
                weatherLight.type = LightType.Directional;
            }
            weatherLight.color = weatherLightColor;
            weatherLight.intensity = weatherLightIntensity;
        }

        // Setup particles
        if (affectsParticles)
        {
            foreach (ParticleSystem ps in particleSystems)
            {
                var main = ps.main;
                main.startSpeed = particleSpeed;
                main.startSize = particleSize;

                var emission = ps.emission;
                emission.rateOverTime = particleEmissionRate;
            }
        }
    }

    public void SetIntensity(float intensity)
    {
        currentIntensity = intensity * baseIntensity;

        // Update particles
        if (affectsParticles)
        {
            foreach (ParticleSystem ps in particleSystems)
            {
                var emission = ps.emission;
                emission.rateOverTime = particleEmissionRate * currentIntensity;
            }
        }

        // Update audio
        if (affectsAudio && audioSource != null)
        {
            audioSource.volume = weatherVolume * currentIntensity;
            if (currentIntensity > 0 && !audioSource.isPlaying)
            {
                audioSource.Play();
            }
            else if (currentIntensity <= 0 && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }

        // Update lighting
        if (affectsLighting && weatherLight != null)
        {
            weatherLight.intensity = weatherLightIntensity * currentIntensity;
            RenderSettings.ambientLight = Color.Lerp(
                RenderSettings.ambientLight,
                weatherLightColor * weatherAmbientIntensity,
                currentIntensity
            );
        }

        // Update fog
        if (affectsFog)
        {
            RenderSettings.fogColor = Color.Lerp(
                RenderSettings.fogColor,
                weatherFogColor,
                currentIntensity
            );
            RenderSettings.fogDensity = Mathf.Lerp(
                RenderSettings.fogDensity,
                weatherFogDensity,
                currentIntensity
            );
        }
    }

    private void OnDestroy()
    {
        // Reset render settings when effect is destroyed
        if (affectsLighting)
        {
            RenderSettings.ambientLight = Color.white;
        }
        if (affectsFog)
        {
            RenderSettings.fogColor = Color.white;
            RenderSettings.fogDensity = 0.01f;
        }
    }
} 