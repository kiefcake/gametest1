using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Audio;

namespace DungeonCrawler.Visuals
{
    // Subscribes to Health.OnDamaged/OnHealed to spawn a floating number and briefly
    // flash any Renderers under this object -- attach alongside Health so every damage
    // source (player abilities, enemy melee, status DoT ticks) gets the same feedback
    // without each of those call sites needing to know about visuals at all. Generic
    // Renderer rather than SpriteRenderer specifically: this is added to both the player
    // and every enemy, and every enemy's visual is now a MeshRenderer-based
    // ProceduralMonster model (or AbyssFinalDemon's imported mesh) rather than a sprite
    // billboard -- SpriteRenderer-only used to match everything back when sprites were
    // the only visual this codebase had, but silently flashed nothing once that changed.
    [RequireComponent(typeof(Health))]
    public class HealthVFX : MonoBehaviour
    {
        public float flashDuration = 0.15f;

        private Health health;
        private Renderer[] renderers;
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
            if (renderers == null) renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return; // e.g. no visible body yet -- damage number still shows

            if (originalColors == null || originalColors.Length != renderers.Length)
            {
                originalColors = new Color[renderers.Length];
                for (int i = 0; i < renderers.Length; i++) originalColors[i] = renderers[i].material.color;
            }

            flashTimer = flashDuration;
            for (int i = 0; i < renderers.Length; i++) renderers[i].material.color = Color.white;
        }

        private void Update()
        {
            if (flashTimer <= 0f) return;
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f)
            {
                for (int i = 0; i < renderers.Length; i++) renderers[i].material.color = originalColors[i];
            }
        }
    }
}
