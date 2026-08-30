using UnityEngine;

namespace DungeonCrawler.Visuals
{
    // Shared helper for attaching a billboarded sprite child to a code-spawned GameObject.
    // Everything in this project is built via AddComponent in code rather than prefabs
    // (see DefaultContentFactory), so visuals need the same no-prefab-required path --
    // this is that path for enemies, pickups, and the player's weapon icon alike.
    public static class SpriteVisual
    {
        public static SpriteRenderer Attach(Transform parent, Sprite sprite, Vector3 localOffset,
            float scale = 1f, int sortingOrder = 0)
        {
            if (sprite == null) return null;

            var go = new GameObject("Visual_" + sprite.name);
            go.transform.SetParent(parent);
            go.transform.localPosition = localOffset;
            go.transform.localScale = Vector3.one * scale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            go.AddComponent<BillboardSprite>();
            go.AddComponent<SpriteAnimator>(); // idle bob by default; EnemyBase triggers PulseAttack() on attack
            return sr;
        }
    }
}
