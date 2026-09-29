using UnityEngine;

/// <summary>
/// Cheap fluorescent-tube flicker for the Built-in Render Pipeline.
///
/// Perturbs a Light's intensity with Perlin noise (a slow buzz) plus occasional hard dropout
/// stutters, which is what makes a failing tube read as "wrong" to the player. Allocation-free
/// and has no dependency on URP or any post-processing package.
/// </summary>
[RequireComponent(typeof(Light))]
public class FlickerLight : MonoBehaviour
{
    [Tooltip("Intensity the flicker modulates around. 0 = capture the Light's authored intensity on Awake.")]
    public float baseIntensity = 0f;

    [Tooltip("How far the intensity swings, as a fraction of the base intensity.")]
    [Range(0f, 1f)] public float flickerAmount = 0.22f;

    [Tooltip("Noise speed. Higher = busier buzzing.")]
    public float flickerSpeed = 7f;

    [Tooltip("Expected hard dropouts per second (a tube stuttering out). 0 = smooth buzz only.")]
    public float dropoutChance = 0.35f;

    [Tooltip("How long a dropout lasts, in seconds.")]
    public float dropoutDuration = 0.06f;

    [Tooltip("Intensity multiplier applied during a dropout.")]
    [Range(0f, 1f)] public float dropoutMultiplier = 0.15f;

    Light target;
    float seed;
    float dropoutUntil;

    void Awake()
    {
        target = GetComponent<Light>();
        if (baseIntensity <= 0f) baseIntensity = target.intensity;
        seed = Random.value * 100f;
    }

    void Update()
    {
        float noise = Mathf.PerlinNoise(Time.time * flickerSpeed + seed, seed);
        float intensity = baseIntensity * (1f - flickerAmount * (1f - noise));

        if (Time.time < dropoutUntil)
        {
            intensity *= dropoutMultiplier;
        }
        else if (dropoutChance > 0f && Random.value < dropoutChance * Time.deltaTime)
        {
            dropoutUntil = Time.time + dropoutDuration;
        }

        target.intensity = intensity;
    }
}
