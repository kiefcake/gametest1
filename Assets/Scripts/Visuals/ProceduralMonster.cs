using UnityEngine;
using System.Collections.Generic;

namespace DungeonCrawler.Visuals
{
    // Procedural low-poly creature builder -- primitives only, no textures (matches this
    // project's existing no-texture art direction, same as HazardVisuals/SpriteVisual/
    // AbyssFinalDemon's imported mesh), assembled at runtime like everything else here.
    // Exists so enemies can have an actual 3D silhouette instead of a flat billboard
    // sprite, without needing an external modeling tool or asset -- there is no in-editor
    // way to generate real mesh files for this project (no login-gated service is wired
    // up), so this substitutes with geometry entirely under our own control.
    //
    // Every Build* method returns the assembled root (parented at local zero -- callers
    // add a SpriteAnimator to the root themselves for idle bob / attack pulse, same as
    // AbyssFinalDemon.AttachVisual does for its imported mesh) plus every Renderer it
    // created, so EnemyBase.SetInvulnerable can tint the whole model at once via
    // visualRenderers instead of just one sprite.
    public static class ProceduralMonster
    {
        public struct Built
        {
            public Transform root;
            public Renderer[] renderers;
        }

        // One primitive part, colored and parented -- the atom every Build* method below is
        // assembled from. Its own collider is destroyed: decorative only, EnemyBase's own
        // CharacterController is what actually collides with the world.
        public static Renderer AddPart(Transform parent, PrimitiveType type, Vector3 localPos,
            Vector3 localScale, Quaternion localRot, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            var renderer = go.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Standard")) { color = color };
            return renderer;
        }

        public struct HumanoidSpec
        {
            public Color bodyColor;
            public Color accentColor; // horns / held weapon
            public float scale;
            public bool horns;
            public bool weapon; // held forearm-aligned rod, reads as a staff or blade depending on tint
            public bool hunched; // shorter legs, forward-leaning head -- feral/skeletal silhouette instead of upright
        }

        // Torso + head + 2 arms + 2 legs -- the default biped silhouette. Covers imps,
        // skeletons, and boss-scale humanoids alike by varying scale/color/hunched/horns.
        public static Built Humanoid(Transform parent, HumanoidSpec spec)
        {
            var renderers = new List<Renderer>();
            var root = new GameObject("MonsterModel").transform;
            root.SetParent(parent, false);
            root.localPosition = Vector3.zero;

            float s = spec.scale;
            float legLen = spec.hunched ? 0.5f * s : 0.7f * s;
            float torsoY = legLen + 0.35f * s;

            renderers.Add(AddPart(root, PrimitiveType.Capsule, new Vector3(0, torsoY, 0),
                new Vector3(0.5f * s, 0.4f * s, 0.5f * s), Quaternion.identity, spec.bodyColor));

            float headY = torsoY + 0.55f * s;
            float headForward = spec.hunched ? 0.12f * s : 0f;
            var headRot = spec.hunched ? Quaternion.Euler(20f, 0, 0) : Quaternion.identity;
            renderers.Add(AddPart(root, PrimitiveType.Sphere, new Vector3(0, headY, headForward),
                Vector3.one * 0.35f * s, headRot, spec.bodyColor));

            if (spec.horns)
            {
                renderers.Add(AddPart(root, PrimitiveType.Cube, new Vector3(-0.13f * s, headY + 0.24f * s, headForward),
                    new Vector3(0.06f * s, 0.24f * s, 0.06f * s), Quaternion.Euler(0, 0, -18f), spec.accentColor));
                renderers.Add(AddPart(root, PrimitiveType.Cube, new Vector3(0.13f * s, headY + 0.24f * s, headForward),
                    new Vector3(0.06f * s, 0.24f * s, 0.06f * s), Quaternion.Euler(0, 0, 18f), spec.accentColor));
            }

            float armY = torsoY + 0.15f * s;
            renderers.Add(AddPart(root, PrimitiveType.Capsule, new Vector3(-0.45f * s, armY, 0),
                new Vector3(0.16f * s, 0.35f * s, 0.16f * s), Quaternion.Euler(0, 0, 20f), spec.bodyColor));
            renderers.Add(AddPart(root, PrimitiveType.Capsule, new Vector3(0.45f * s, armY, 0),
                new Vector3(0.16f * s, 0.35f * s, 0.16f * s), Quaternion.Euler(0, 0, -20f), spec.bodyColor));

            renderers.Add(AddPart(root, PrimitiveType.Capsule, new Vector3(-0.2f * s, legLen * 0.5f, 0),
                new Vector3(0.18f * s, legLen * 0.5f, 0.18f * s), Quaternion.identity, spec.bodyColor));
            renderers.Add(AddPart(root, PrimitiveType.Capsule, new Vector3(0.2f * s, legLen * 0.5f, 0),
                new Vector3(0.18f * s, legLen * 0.5f, 0.18f * s), Quaternion.identity, spec.bodyColor));

            if (spec.weapon)
            {
                renderers.Add(AddPart(root, PrimitiveType.Cube, new Vector3(0.6f * s, armY - 0.05f * s, 0.3f * s),
                    new Vector3(0.05f * s, 0.55f * s, 0.05f * s), Quaternion.Euler(25f, 0, -15f), spec.accentColor));
            }

            return new Built { root = root, renderers = renderers.ToArray() };
        }

        public struct FloatingSpec
        {
            public Color robeColor;
            public Color accentColor; // eyes / orb
            public float scale;
            public bool orb; // small companion sphere orbiting near the hand -- reads as an active spellcaster
        }

        // Wide-based tapered "robe" (a Cylinder, since Unity has no built-in cone) + a
        // hooded head with no visible legs -- casters that hover/channel rather than plant
        // themselves like a melee brute (AbyssMage, FrostLich).
        public static Built FloatingCaster(Transform parent, FloatingSpec spec)
        {
            var renderers = new List<Renderer>();
            var root = new GameObject("MonsterModel").transform;
            root.SetParent(parent, false);
            root.localPosition = Vector3.zero;

            float s = spec.scale;
            float robeY = 0.55f * s;

            renderers.Add(AddPart(root, PrimitiveType.Cylinder, new Vector3(0, robeY, 0),
                new Vector3(0.5f * s, 0.55f * s, 0.5f * s), Quaternion.identity, spec.robeColor));

            float headY = robeY + 0.7f * s;
            // Slightly darker hood tint reads as shadowed under a cowl without needing an
            // actual cone mesh.
            Color hoodColor = spec.robeColor * 0.7f;
            hoodColor.a = 1f;
            renderers.Add(AddPart(root, PrimitiveType.Sphere, new Vector3(0, headY, 0),
                Vector3.one * 0.32f * s, Quaternion.identity, hoodColor));

            renderers.Add(AddPart(root, PrimitiveType.Sphere, new Vector3(-0.1f * s, headY + 0.02f * s, 0.28f * s),
                Vector3.one * 0.05f * s, Quaternion.identity, spec.accentColor));
            renderers.Add(AddPart(root, PrimitiveType.Sphere, new Vector3(0.1f * s, headY + 0.02f * s, 0.28f * s),
                Vector3.one * 0.05f * s, Quaternion.identity, spec.accentColor));

            if (spec.orb)
            {
                renderers.Add(AddPart(root, PrimitiveType.Sphere, new Vector3(0.55f * s, robeY + 0.1f * s, 0.2f * s),
                    Vector3.one * 0.14f * s, Quaternion.identity, spec.accentColor));
            }

            return new Built { root = root, renderers = renderers.ToArray() };
        }

        public struct BlobSpec
        {
            public Color bodyColor;
            public Color accentColor; // eyes
            public float scale;
        }

        // Stacked, off-center spheres with no limbs -- an amorphous silhouette for oozing/
        // swamp-muck enemies (BogLurker) rather than a humanoid one.
        public static Built Blob(Transform parent, BlobSpec spec)
        {
            var renderers = new List<Renderer>();
            var root = new GameObject("MonsterModel").transform;
            root.SetParent(parent, false);
            root.localPosition = Vector3.zero;

            float s = spec.scale;
            renderers.Add(AddPart(root, PrimitiveType.Sphere, new Vector3(0, 0.35f * s, 0),
                Vector3.one * 0.75f * s, Quaternion.identity, spec.bodyColor));
            renderers.Add(AddPart(root, PrimitiveType.Sphere, new Vector3(0.2f * s, 0.65f * s, 0.15f * s),
                Vector3.one * 0.45f * s, Quaternion.identity, spec.bodyColor));
            renderers.Add(AddPart(root, PrimitiveType.Sphere, new Vector3(-0.15f * s, 0.55f * s, -0.2f * s),
                Vector3.one * 0.35f * s, Quaternion.identity, spec.bodyColor));

            renderers.Add(AddPart(root, PrimitiveType.Sphere, new Vector3(-0.15f * s, 0.75f * s, 0.35f * s),
                Vector3.one * 0.08f * s, Quaternion.identity, spec.accentColor));
            renderers.Add(AddPart(root, PrimitiveType.Sphere, new Vector3(0.15f * s, 0.75f * s, 0.35f * s),
                Vector3.one * 0.08f * s, Quaternion.identity, spec.accentColor));

            return new Built { root = root, renderers = renderers.ToArray() };
        }
    }
}
