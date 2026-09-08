using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace DungeonCrawler.Visuals
{
    // The actual fix for "no per-limb rigging" (and, more generally, "no way to move a
    // sub-part independently") on real imported meshes.
    //
    // What did NOT work, tried first: exporting a rigged part's vertices relative to a
    // pivot point WITHIN the same combined .obj as the rest of the model, then finding
    // it by name (via Transform.Find, then a recursive search once the shallow Find
    // turned out to be the wrong culprit) and reparenting it onto a runtime pivot
    // GameObject. Confirmed by direct testing (dumping the actual imported hierarchy to
    // a file) that this can't work at all: Unity's OBJ importer, regardless of the
    // `preserveHierarchy` setting, flattens every named "o"/"g" group within ONE file
    // into a single combined mesh with zero separate child Transforms -- there is
    // nothing to find or reparent, because the sub-part was never a distinct
    // GameObject to begin with.
    //
    // What actually works: give each independently-movable part its OWN separate
    // OBJ+MTL file (see export_creatures.mjs/export_props.mjs's own comments). Unity
    // then gives each one its own real, separate, independently-Instantiate-able
    // GameObject purely by virtue of being a separate asset -- no importer hierarchy
    // behavior to depend on at all. A creature/prop with rigged parts exports as
    // "<id>_body.obj" (everything static) plus one "<id>_<group>.obj" per rig group,
    // with a "<id>_rig.txt" sidecar recording each group's resolved pivot position.
    public static class ImportedMeshRig
    {
        // Loads and instantiates every rig group listed in "<baseResourcePath>_rig.txt"
        // as its own separate model, parented under modelRoot and positioned at its own
        // pivot -- callers rotate/translate the returned Transform directly, no further
        // setup needed. Returns an empty dictionary if there's no sidecar (true for
        // most creatures/props: snakes, oozes, the dummy, and every non-rigged prop) or
        // if a listed group's own model file is missing (tolerated, not thrown, the
        // same way a stale/missing resource is handled everywhere else in this file).
        public static Dictionary<string, Transform> LoadRigGroups(Transform modelRoot, string baseResourcePath)
        {
            var groups = new Dictionary<string, Transform>();
            var rigData = Resources.Load<TextAsset>(baseResourcePath + "_rig");
            if (rigData == null) return groups;

            foreach (var rawLine in rigData.text.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;
                var fields = line.Split(' ');
                if (fields.Length < 4) continue;

                string groupName = fields[0];
                if (!float.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float px)) continue;
                if (!float.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float py)) continue;
                if (!float.TryParse(fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float pz)) continue;

                var groupModel = Resources.Load<GameObject>(baseResourcePath + "_" + groupName);
                if (groupModel == null) continue;

                var groupGO = Object.Instantiate(groupModel, modelRoot);
                groupGO.name = groupName;
                groupGO.transform.localPosition = new Vector3(px, py, pz);
                groupGO.transform.localRotation = Quaternion.identity;
                groups[groupName] = groupGO.transform;
            }

            return groups;
        }

        // Creature-limb convenience wrapper over LoadRigGroups -- maps rig group names
        // (always "<prefix>_<L|R>", "leg" -> hip, anything else ("arm"/"sleeve") ->
        // shoulder) into ProceduralLimbAnimator's 4 named fields and wires it up. This is
        // the exact same walk-cycle swing (movement-triggered, phase-desynced, damped
        // back to neutral when idle) ProceduralMonster.Humanoid's own primitive-built
        // pivots already use, just now driving real mesh pieces instead of primitive
        // capsules -- one limb-swing behavior for the whole game, not two.
        //
        // moveTracker should be the ENEMY's own root transform, not modelRoot -- see
        // ProceduralLimbAnimator's own doc comment on why (modelRoot gets bobbed
        // vertically every frame by SpriteAnimator, which would misread as walking).
        // Returns null if there's no rig sidecar or no limb-shaped groups in it.
        public static ProceduralLimbAnimator Attach(Transform modelRoot, string baseResourcePath, Transform moveTracker)
        {
            var groups = LoadRigGroups(modelRoot, baseResourcePath);
            if (groups.Count == 0) return null;

            Transform leftHip = null, rightHip = null, leftShoulder = null, rightShoulder = null;
            foreach (var kv in groups)
            {
                var parts = kv.Key.Split('_');
                if (parts.Length != 2) continue;
                bool isHip = parts[0] == "leg";
                bool isLeft = parts[1] == "L";

                if (isHip && isLeft) leftHip = kv.Value;
                else if (isHip) rightHip = kv.Value;
                else if (isLeft) leftShoulder = kv.Value;
                else rightShoulder = kv.Value;
            }

            if (leftHip == null && rightHip == null && leftShoulder == null && rightShoulder == null)
                return null;

            var animator = modelRoot.gameObject.AddComponent<ProceduralLimbAnimator>();
            animator.moveTracker = moveTracker;
            animator.leftHip = leftHip;
            animator.rightHip = rightHip;
            animator.leftShoulder = leftShoulder;
            animator.rightShoulder = rightShoulder;
            return animator;
        }
    }
}
