using UnityEngine;
using DungeonCrawler.Classes;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Enemies
{
    // Simple dodgeable bolt -- travels in a straight line at a speed slow enough that a
    // player who's paying attention can sidestep it (see RangedImp.projectileSpeed),
    // rather than an instant hitscan. Damages the player only: nothing here bothers with
    // an owner-exclusion list since the only thing it's allowed to hit is PlayerCharacter.
    public class Projectile : MonoBehaviour
    {
        private Vector3 direction;
        private float speed;
        private float damage;
        private float lifetime;
        private float age;

        public static Projectile Spawn(Vector3 pos, Vector3 dir, float speed, float damage, Color color, float lifetime = 4f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile";
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.3f;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.material = new Material(Shader.Find("Standard")) { color = color };

            // The Sphere primitive already ships with a SphereCollider -- just flip it to a
            // trigger instead of destroying and rebuilding one.
            var col = go.GetComponent<SphereCollider>();
            col.isTrigger = true;

            // At least one Rigidbody is needed in a collider pair for OnTriggerEnter to
            // fire at all (see WorldPickup/PlayerCharacter for the same rule discovered
            // there) -- kinematic so it isn't pushed around by physics.
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = 2.5f;
            light.intensity = 1.2f;

            var proj = go.AddComponent<Projectile>();
            proj.direction = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;
            proj.speed = speed;
            proj.damage = damage;
            proj.lifetime = lifetime;
            return proj;
        }

        private void Update()
        {
            transform.position += direction * speed * Time.deltaTime;
            age += Time.deltaTime;
            if (age >= lifetime) Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<PlayerCharacter>();
            if (player == null || player.health == null) return;

            player.health.TakeDamage(damage, ignoreDef: false);
            ImpactBurst.Spawn(transform.position, new Color(0.8f, 0.2f, 0.9f));
            Destroy(gameObject);
        }
    }
}
