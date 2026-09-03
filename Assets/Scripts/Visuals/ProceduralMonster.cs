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
    // visualRenderers instead of just one sprite. Humanoid additionally exposes its hip/
    // shoulder pivots so a caller can drive a walk cycle via ProceduralLimbAnimator.
    public static class ProceduralMonster
    {
        public struct Built
        {
            public Transform root;
            public Renderer[] renderers;
            // Populated by Humanoid only (identity Quaternion default is exactly the rest
            // pose, so a caller that ignores these for a non-Humanoid archetype needs no
            // null-check gymnastics -- ProceduralLimbAnimator just no-ops on a null pivot).
            public Transform leftHip, rightHip, leftShoulder, rightShoulder;
        }

        // One primitive part, colored and parented -- the atom every Build* method below is
        // assembled from. Its own collider is destroyed: decorative only, EnemyBase's own
        // CharacterController is what actually collides with the world. emissive gives the
        // part a genuine self-lit glow (via the Standard shader's emission channel) rather
        // than just a bright flat color -- used for eyes, so they read as "lit from within"
        // even in a dim room instead of going as dark as everything else around them.
        public static Renderer AddPart(Transform parent, PrimitiveType type, Vector3 localPos,
            Vector3 localScale, Quaternion localRot, Color color, bool emissive = false)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            var renderer = go.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Standard")) { color = color };
            if (emissive)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 2f);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            renderer.material = mat;
            return renderer;
        }

        public struct HumanoidSpec
        {
            public Color bodyColor;
            public Color accentColor; // horns / claws / held weapon / eye glow
            public float scale;
            public bool horns;
            public bool weapon; // held forearm-aligned rod, reads as a staff or blade depending on tint
            public bool hunched; // shorter legs, forward-leaning head -- feral/skeletal silhouette instead of upright
        }

        // Torso + head + 2 arms + 2 legs -- the default biped silhouette. Covers imps,
        // skeletons, and boss-scale humanoids alike by varying scale/color/hunched/horns.
        //
        // Legs and arms are built as an empty pivot (LeftHip/RightHip/LeftShoulder/
        // RightShoulder) positioned at the hip/shoulder, with the actual limb capsule as
        // its child -- at rest (pivot rotation identity) this renders pixel-for-pixel the
        // same as a limb built directly under root, but it means ProceduralLimbAnimator
        // can swing a limb from its real attachment point instead of spinning it around
        // its own belly button.
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

            // Bigger than a human head's real proportions on purpose -- an oversized head
            // is one of the cheapest ways to read "creature" instead of "action figure."
            float headY = torsoY + 0.58f * s;
            float headForward = spec.hunched ? 0.12f * s : 0f;
            var headRot = spec.hunched ? Quaternion.Euler(20f, 0, 0) : Quaternion.identity;
            renderers.Add(AddPart(root, PrimitiveType.Sphere, new Vector3(0, headY, headForward),
                Vector3.one * 0.4f * s, headRot, spec.bodyColor));

            // Pointed ears flared from the sides of the head -- the single cheapest thing
            // that stops the head reading as a plain ball.
            renderers.Add(AddPart(root, PrimitiveType.Cube, new Vector3(-0.33f * s, headY + 0.13f * s, headForward),
                new Vector3(0.05f * s, 0.22f * s, 0.12f * s), Quaternion.Euler(0, 0, 32f), spec.bodyColor));
            renderers.Add(AddPart(root, PrimitiveType.Cube, new Vector3(0.33f * s, headY + 0.13f * s, headForward),
                new Vector3(0.05f * s, 0.22f * s, 0.12f * s), Quaternion.Euler(0, 0, -32f), spec.bodyColor));

            // Glowing eyes, tinted from accentColor -- the one detail that makes the model
            // read as looking at you rather than a static prop, and ties the same accent
            // family (horns/claws/weapon) into the face too.
            float eyeZ = headForward + 0.35f * s;
            renderers.Add(AddPart(root, PrimitiveType.Sphere, new Vector3(-0.14f * s, headY + 0.03f * s, eyeZ),
                Vector3.one * 0.065f * s, Quaternion.identity, spec.accentColor, emissive: true));
            renderers.Add(AddPart(root, PrimitiveType.Sphere, new Vector3(0.14f * s, headY + 0.03f * s, eyeZ),
                Vector3.one * 0.065f * s, Quaternion.identity, spec.accentColor, emissive: true));

            if (spec.horns)
            {
                renderers.Add(AddPart(root, PrimitiveType.Cube, new Vector3(-0.13f * s, headY + 0.32f * s, headForward),
                    new Vector3(0.06f * s, 0.24f * s, 0.06f * s), Quaternion.Euler(0, 0, -18f), spec.accentColor));
                renderers.Add(AddPart(root, PrimitiveType.Cube, new Vector3(0.13f * s, headY + 0.32f * s, headForward),
                    new Vector3(0.06f * s, 0.24f * s, 0.06f * s), Quaternion.Euler(0, 0, 18f), spec.accentColor));
            }

            float armY = torsoY + 0.15f * s;
            var leftShoulder = BuildLimbPivot(root, new Vector3(-0.45f * s, armY, 0));
            var leftArmCapsule = AddPart(leftShoulder, PrimitiveType.Capsule, Vector3.zero,
                new Vector3(0.16f * s, 0.35f * s, 0.16f * s), Quaternion.Euler(0, 0, 20f), spec.bodyColor);
            renderers.Add(leftArmCapsule);
            AddClawPair(leftArmCapsule.transform, renderers, spec.accentColor);

            var rightShoulder = BuildLimbPivot(root, new Vector3(0.45f * s, armY, 0));
            var rightArmCapsule = AddPart(rightShoulder, PrimitiveType.Capsule, Vector3.zero,
                new Vector3(0.16f * s, 0.35f * s, 0.16f * s), Quaternion.Euler(0, 0, -20f), spec.bodyColor);
            renderers.Add(rightArmCapsule);
            AddClawPair(rightArmCapsule.transform, renderers, spec.accentColor);

            var leftHip = BuildLimbPivot(root, new Vector3(-0.2f * s, legLen, 0));
            renderers.Add(AddPart(leftHip, PrimitiveType.Capsule, new Vector3(0, -legLen * 0.5f, 0),
                new Vector3(0.18f * s, legLen * 0.5f, 0.18f * s), Quaternion.identity, spec.bodyColor));

            var rightHip = BuildLimbPivot(root, new Vector3(0.2f * s, legLen, 0));
            renderers.Add(AddPart(rightHip, PrimitiveType.Capsule, new Vector3(0, -legLen * 0.5f, 0),
                new Vector3(0.18f * s, legLen * 0.5f, 0.18f * s), Quaternion.identity, spec.bodyColor));

            if (spec.weapon)
            {
                // Parented to the shoulder pivot (scale 1, so this local offset is plain
                // s-scaled units, same as the original root-relative position) rather than
                // the arm capsule -- rides along with the whole arm's walk-cycle swing,
                // which is the more natural read for something held in a swinging hand.
                renderers.Add(AddPart(rightShoulder, PrimitiveType.Cube, new Vector3(0.15f * s, -0.05f * s, 0.3f * s),
                    new Vector3(0.05f * s, 0.55f * s, 0.05f * s), Quaternion.Euler(25f, 0, -15f), spec.accentColor));
            }

            return new Built
            {
                root = root,
                renderers = renderers.ToArray(),
                leftHip = leftHip,
                rightHip = rightHip,
                leftShoulder = leftShoulder,
                rightShoulder = rightShoulder,
            };
        }

        // An empty pivot at a limb's attachment point -- ProceduralLimbAnimator rotates
        // these directly (their rest local rotation is always identity by construction),
        // while the limb capsule itself is a child carrying whatever static tilt it needs.
        private static Transform BuildLimbPivot(Transform root, Vector3 localPos)
        {
            var pivot = new GameObject("LimbPivot").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = localPos;
            return pivot;
        }

        // Two tiny angled cubes at a limb capsule's own tip -- reads as claws/talons
        // without a real hand mesh. Parented to the CAPSULE itself (not its pivot) at
        // local Y = -1 (a unit capsule's own bottom pole before its parent's non-uniform
        // scale is applied) so the claws land exactly at the capsule's tip and rotate
        // rigidly with it, regardless of the capsule's own static tilt or the pivot's
        // walk-cycle swing above it -- no world-space trig required.
        private static void AddClawPair(Transform capsule, List<Renderer> renderers, Color color)
        {
            renderers.Add(AddPart(capsule, PrimitiveType.Cube, new Vector3(0.15f, -1f, 0.15f),
                new Vector3(0.3f, 0.35f, 0.3f), Quaternion.Euler(35f, 0, 15f), color));
            renderers.Add(AddPart(capsule, PrimitiveType.Cube, new Vector3(-0.15f, -1f, 0.15f),
                new Vector3(0.3f, 0.35f, 0.3f), Quaternion.Euler(35f, 0, -15f), color));
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

            // Tattered hem -- a handful of short strips hanging past the robe's own clean
            // bottom edge, uneven lengths/angles, so the silhouette reads worn/sinister
            // instead of a smooth traffic-cone robe.
            for (int i = 0; i < 5; i++)
            {
                float angle = i * (360f / 5) + 15f;
                float rad = angle * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(Mathf.Sin(rad) * 0.42f * s, robeY - 0.5f * s, Mathf.Cos(rad) * 0.42f * s);
                float tatterLen = Random.Range(0.18f, 0.32f) * s;
                Color hemColor = spec.robeColor * 0.75f; hemColor.a = 1f;
                renderers.Add(AddPart(root, PrimitiveType.Cube, pos + new Vector3(0, -tatterLen * 0.5f, 0),
                    new Vector3(0.12f * s, tatterLen * 0.5f, 0.04f * s), Quaternion.Euler(0, angle, Random.Range(-8f, 8f)), hemColor));
            }

            float headY = robeY + 0.7f * s;
            // Slightly darker hood tint reads as shadowed under a cowl without needing an
            // actual cone mesh.
            Color hoodColor = spec.robeColor * 0.7f;
            hoodColor.a = 1f;
            renderers.Add(AddPart(root, PrimitiveType.Sphere, new Vector3(0, headY, 0),
                Vector3.one * 0.32f * s, Quaternion.identity, hoodColor));

            renderers.Add(AddPart(root, PrimitiveType.Sphere, new Vector3(-0.1f * s, headY + 0.02f * s, 0.28f * s),
                Vector3.one * 0.06f * s, Quaternion.identity, spec.accentColor, emissive: true));
            renderers.Add(AddPart(root, PrimitiveType.Sphere, new Vector3(0.1f * s, headY + 0.02f * s, 0.28f * s),
                Vector3.one * 0.06f * s, Quaternion.identity, spec.accentColor, emissive: true));

            if (spec.orb)
            {
                renderers.Add(AddPart(root, PrimitiveType.Sphere, new Vector3(0.55f * s, robeY + 0.1f * s, 0.2f * s),
                    Vector3.one * 0.14f * s, Quaternion.identity, spec.accentColor, emissive: true));
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

            // Dripping tendrils hanging from the underside -- sells "wet ooze" better than
            // a clean sphere stack alone.
            renderers.Add(AddPart(root, PrimitiveType.Capsule, new Vector3(0.1f * s, 0.08f * s, 0.25f * s),
                new Vector3(0.06f * s, 0.14f * s, 0.06f * s), Quaternion.identity, spec.bodyColor));
            renderers.Add(AddPart(root, PrimitiveType.Capsule, new Vector3(-0.2f * s, 0.05f * s, 0.1f * s),
                new Vector3(0.05f * s, 0.1f * s, 0.05f * s), Quaternion.identity, spec.bodyColor));

            renderers.Add(AddPart(root, PrimitiveType.Sphere, new Vector3(-0.15f * s, 0.75f * s, 0.35f * s),
                Vector3.one * 0.09f * s, Quaternion.identity, spec.accentColor, emissive: true));
            renderers.Add(AddPart(root, PrimitiveType.Sphere, new Vector3(0.15f * s, 0.75f * s, 0.35f * s),
                Vector3.one * 0.09f * s, Quaternion.identity, spec.accentColor, emissive: true));

            return new Built { root = root, renderers = renderers.ToArray() };
        }

        public struct SerpentSpec
        {
            public Color bodyColor;
            public Color accentColor; // eyes / tongue / belly scale stripe
            public float scale;
            public float length; // in body-segment units, e.g. 5-8 for a normal trash mob, more for something boss-scale
        }

        // Legless -- a chain of shrinking capsule segments laid along local +Z in a gentle
        // S-curve, head at the front. Snake Pit's trash mobs (PitSnake, PitDartThrower).
        //
        // Positions are computed up front so each segment's rotation can be derived from
        // its neighbors (the curve's own tangent) via LookRotation instead of every segment
        // just pointing straight down +Z like a stack of disconnected beads.
        public static Built Serpent(Transform parent, SerpentSpec spec)
        {
            var renderers = new List<Renderer>();
            var root = new GameObject("MonsterModel").transform;
            root.SetParent(parent, false);
            root.localPosition = Vector3.zero;

            float s = spec.scale;
            int segments = Mathf.Max(4, Mathf.RoundToInt(spec.length));
            float segSpacing = 0.32f * s;

            // index 0 = frontmost body segment (just behind the head), rising index moves
            // toward the tail tip at -Z, per every other archetype's "faces +Z" convention.
            var positions = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float offsetX = Mathf.Sin(i * 0.9f) * 0.15f * s;
                positions[i] = new Vector3(offsetX, 0.18f * s, -i * segSpacing);
            }

            for (int i = 0; i < segments; i++)
            {
                float t = segments > 1 ? (float)i / (segments - 1) : 0f;
                float radius = Mathf.Lerp(0.28f, 0.1f, t) * s;

                Vector3 tangent = i < segments - 1 ? positions[i + 1] - positions[i] : positions[i] - positions[i - 1];
                if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.back;
                // Capsule primitives are elongated along their own local Y by default -- the
                // extra Euler(90,0,0) reorients that long axis onto local Z first, so
                // LookRotation's target direction is what ends up following the tangent.
                Quaternion rot = Quaternion.LookRotation(tangent.normalized, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);

                renderers.Add(AddPart(root, PrimitiveType.Capsule, positions[i],
                    new Vector3(radius, segSpacing * 0.75f, radius), rot, spec.bodyColor));

                // Belly-stripe accent on a few segments near the head only -- cheap extra
                // scale detail, skipped further back so it doesn't cost a part per segment.
                if (i < segments - 1 && i < 5)
                {
                    renderers.Add(AddPart(root, PrimitiveType.Cube, positions[i] + new Vector3(0, -radius * 0.85f, 0),
                        new Vector3(radius * 0.5f, 0.02f * s, segSpacing * 0.45f), rot, spec.accentColor));
                }
            }

            // Head: wider than tall (a Sphere squashed non-uniformly) so it reads as a
            // triangular wedge instead of Humanoid's round skull -- the head shape is the
            // other big cue (besides the tongue) that separates a snake from a legless blob.
            Vector3 headDir = (positions[0] - positions[1]).normalized;
            Vector3 headPos = positions[0] + headDir * (0.22f * s);
            Quaternion headRot = Quaternion.LookRotation(headDir, Vector3.up);
            renderers.Add(AddPart(root, PrimitiveType.Sphere, headPos,
                new Vector3(0.34f * s, 0.2f * s, 0.4f * s), headRot, spec.bodyColor));

            // Glowing eyes, tinted from accentColor -- same technique as Humanoid/
            // FloatingCaster. Positioned via headRot rather than a fixed +Z offset so they
            // still sit correctly on the head even where the S-curve bends it sideways.
            Vector3 eyeCenter = headPos + headRot * new Vector3(0, 0.06f * s, 0.14f * s);
            Vector3 eyeSide = headRot * (Vector3.right * 0.14f * s);
            renderers.Add(AddPart(root, PrimitiveType.Sphere, eyeCenter - eyeSide,
                Vector3.one * 0.055f * s, Quaternion.identity, spec.accentColor, emissive: true));
            renderers.Add(AddPart(root, PrimitiveType.Sphere, eyeCenter + eyeSide,
                Vector3.one * 0.055f * s, Quaternion.identity, spec.accentColor, emissive: true));

            // Forked tongue flick, jutting straight out from the snout tip -- the single
            // most snake-identifying detail on the whole model.
            Vector3 tongueBase = headPos + headRot * new Vector3(0, -0.02f * s, 0.22f * s);
            renderers.Add(AddPart(root, PrimitiveType.Capsule, tongueBase,
                new Vector3(0.015f * s, 0.12f * s, 0.015f * s), headRot * Quaternion.Euler(90f, 10f, 0f), spec.accentColor));
            renderers.Add(AddPart(root, PrimitiveType.Capsule, tongueBase,
                new Vector3(0.015f * s, 0.12f * s, 0.015f * s), headRot * Quaternion.Euler(90f, -10f, 0f), spec.accentColor));

            return new Built { root = root, renderers = renderers.ToArray() };
        }
    }
}
