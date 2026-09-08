using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace DungeonCrawler.Visuals
{
    // The actual fix for "no per-limb rigging" (and, more generally, "no way to rotate
    // any sub-part") on real imported meshes, not just the root-level sway SpriteAnimator
    // got earlier. A real mesh's parts all come in at identity local transform with their
    // vertices baked in absolute world space -- OBJ has no hierarchy/pivot concept at
    // all, so rotating one of those parts directly would spin it around the WHOLE
    // model's origin, not its own joint/hinge.
    //
    // The export pipeline (export_creatures.mjs, export_props.mjs) can tag any mesh with
    // a rig group name and a pivot point (see creatures.js's limbPair/clawedHand for
    // creature limbs, props.js's chestCommon for a hinge), bakes that mesh's vertices
    // RELATIVE TO the pivot instead of the whole model's space, and writes a
    // "<name>_rig.txt" sidecar recording each group's pivot position and member mesh
    // names. LoadPivots reads that sidecar, builds one empty pivot GameObject per group
    // positioned exactly there, and reparents the group's mesh pieces under it at local
    // zero -- since their geometry is already pivot-relative, that exactly reproduces
    // the original static pose, but now rotating the pivot transform correctly pivots
    // around the joint/hinge instead of the model's own origin.
    public static class ImportedMeshRig
    {
        // Returns every rig group found in rigData as {groupName -> pivot transform},
        // already reparented and positioned -- an empty dictionary if rigData is null or
        // has no groups (true for creatures/props with nothing rigged: snakes, oozes,
        // the training dummy, most props). Callers own what to DO with each pivot --
        // Attach() below wires creature limb pivots into ProceduralLimbAnimator; a chest
        // can just grab pivots["lid"] and rotate it directly on open.
        public static Dictionary<string, Transform> LoadPivots(Transform modelRoot, TextAsset rigData)
        {
            var pivots = new Dictionary<string, Transform>();
            if (rigData == null) return pivots;

            foreach (var rawLine in rigData.text.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;
                var fields = line.Split(' ');
                if (fields.Length < 5) continue;

                string groupName = fields[0];
                if (!float.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float px)) continue;
                if (!float.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float py)) continue;
                if (!float.TryParse(fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float pz)) continue;
                var pivotPos = new Vector3(px, py, pz);

                var pivotGO = new GameObject("Rig_" + groupName);
                pivotGO.transform.SetParent(modelRoot, false);
                pivotGO.transform.localPosition = pivotPos;
                pivotGO.transform.localRotation = Quaternion.identity;

                foreach (var memberName in fields[4].Split(','))
                {
                    var member = modelRoot.Find(memberName);
                    if (member == null) continue; // tolerate a stale sidecar rather than throwing
                    member.SetParent(pivotGO.transform, false);
                    member.localPosition = Vector3.zero;
                    member.localRotation = Quaternion.identity;
                }

                pivots[groupName] = pivotGO.transform;
            }

            return pivots;
        }

        // Creature-limb convenience wrapper over LoadPivots -- maps rig group names
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
        // Returns null if rigData is null or has no limb-shaped groups.
        public static ProceduralLimbAnimator Attach(Transform modelRoot, TextAsset rigData, Transform moveTracker)
        {
            var pivots = LoadPivots(modelRoot, rigData);
            if (pivots.Count == 0) return null;

            Transform leftHip = null, rightHip = null, leftShoulder = null, rightShoulder = null;
            foreach (var kv in pivots)
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
