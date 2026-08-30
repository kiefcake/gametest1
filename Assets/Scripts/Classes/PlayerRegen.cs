using UnityEngine;
using DungeonCrawler.Core;

namespace DungeonCrawler.Classes
{
    // Health.Heal / Mana.Regen already existed but nothing called them on a timer --
    // this is that timer. Rate scales with VIT per the locked stat design ("VIT: HP/MP
    // regen rate"), so a VIT-heavy build actually feels sustain-focused.
    [RequireComponent(typeof(PlayerCharacter))]
    public class PlayerRegen : MonoBehaviour
    {
        private PlayerCharacter player;

        private void Awake()
        {
            player = GetComponent<PlayerCharacter>();
        }

        private void Update()
        {
            if (player.health == null || player.health.IsDowned) return;
            if (player.Stats == null) return;

            float perSecond = 1f + player.Stats.GetValue(StatType.VIT) * 0.3f;

            if (player.health.CurrentHP < player.health.maxHP)
                player.health.Heal(perSecond * Time.deltaTime);

            if (player.mana != null && player.mana.CurrentMP < player.mana.maxMP)
                player.mana.Regen(perSecond * Time.deltaTime);
        }
    }
}
