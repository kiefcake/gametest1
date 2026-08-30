using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Audio;

namespace DungeonCrawler.Visuals
{
    // Subscribes to Health.OnDamaged/OnHealed to spawn a floating number and briefly
    // flash any SpriteRenderers under this object -- attach alongside Health so every
    // damage source (player abilities, enemy melee, status DoT ticks) gets the same
    // feedback without each of those call sites needing to know about visuals at all.
    [RequireComponent(typeof(Health))]
    public class HealthVFX : MonoBehaviour
    {
        public float flashDuration = 0.15f;

        private Health health;
        private SpriteRenderer[] sprites;
        private Color[] originalColors;
        private float flashTimer;

        private void Awake()
        {
            health = GetComponent<Health>();
            health.OnDamaged += HandleDamaged;
            health.OnHealed += HandleHealed;
        }

        private void OnDestroy()
        {
            if (health == null) return;
            health.OnDamaged -= HandleDamaged;
            health.OnHealed -= HandleHealed;
        }

        private void HandleDamaged(float amount)
        {
            DamageNumber.Spawn(transform.position + Vector3.up * 1.2f, amount, isHeal: false);
            SfxLibrary.PlayAt(SfxLibrary.Hit, transform.position, 0.35f);
            StartFlash();
        }

        private void HandleHealed(float amount)
        {
            DamageNumber.Spawn(transform.position + Vector3.up * 1.2f, amount, isHeal: true);
        }

        private void StartFlash()
        {
            if (sprites == null) sprites = GetComponentsInChildren<SpriteRenderer>();
            if (sprites.Length == 0) return; // e.g. the player has no visible sprite body -- damage number still shows

            if (originalColors == null || originalColors.Length != sprites.Length)
            {
                originalColors = new Color[sprites.Length];
                for (int i = 0; i < sprites.Length; i++) originalColors[i] = sprites[i].color;
            }

            flashTimer = flashDuration;
            for (int i = 0; i < sprites.Length; i++) sprites[i].color = Color.white;
        }

        private void Update()
        {
            if (flashTimer <= 0f) return;
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f)
            {
                for (int i = 0; i < sprites.Length; i++) sprites[i].color = originalColors[i];
            }
        }
    }
}
