using UnityEngine;

namespace DungeonCrawler.World
{
    // Subtle Perlin-noise intensity variation so a torch's Light doesn't read as a static
    // point light -- cheap atmosphere with no particle/shader assets required.
    [RequireComponent(typeof(Light))]
    public class TorchFlicker : MonoBehaviour
    {
        public float flickerAmount = 0.35f;
        public float flickerSpeed = 6f;

        private Light lightComp;
        private float baseIntensity;
        private float phase;

        private void Awake()
        {
            lightComp = GetComponent<Light>();
            baseIntensity = lightComp.intensity;
            phase = Random.Range(0f, 100f); // desyncs multiple torches
        }

        private void Update()
        {
            float n = Mathf.PerlinNoise(phase, Time.time * flickerSpeed);
            lightComp.intensity = baseIntensity + (n - 0.5f) * flickerAmount;
        }
    }
}
