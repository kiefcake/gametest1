using UnityEngine;
using DungeonCrawler.Core;

namespace DungeonCrawler.Enemies
{
    // The Snake Pit camp's guardian -- PitSnake's Coil Strike stays inherited as-is, plus
    // one signature move: Venom Barrage. Unlike the other three chiefs (all radius pulses
    // around themselves), this one is a ranged fan -- PitSnake is normally melee-only, so
    // giving its chief a projectile spread makes it read as a genuinely different threat
    // from its own trash escort, not just a bigger copy of one.
    public class PitSnakeChief : PitSnake
    {
        [Header("Signature attack: Venom Barrage")]
        public float barrageInterval = 8f;
        public float barrageChannelTime = 0.8f;
        public int barrageCount = 7;
        public float barrageSpreadAngle = 100f;
        public float barrageDamage = 9f;
        public float barrageSpeed = 8f;
        private float barrageTimer;
        private bool channeling;
        private float channelElapsed;

        protected override void Awake()
        {
            base.Awake();
            barrageTimer = barrageInterval * 0.6f;
        }

        protected override void Update()
        {
            if (channeling)
            {
                channelElapsed += Time.deltaTime;
                if (channelElapsed >= barrageChannelTime)
                {
                    FireBarrage();
                    channeling = false;
                    SetInvulnerable(false);
                }
                return; // frozen mid-channel, same shape as every other chief/boss special
            }

            base.Update();
            if (health.IsDowned || target == null) return;

            barrageTimer -= Time.deltaTime;
            if (barrageTimer <= 0f)
            {
                channeling = true;
                channelElapsed = 0f;
                barrageTimer = barrageInterval * Random.Range(0.85f, 1.15f);
                SetInvulnerable(true); // the wind-up before rearing back to spit -- a real dodge window, not just a stat check
            }
        }

        private void FireBarrage()
        {
            if (target == null) return;
            Vector3 origin = transform.position + Vector3.up;
            Vector3 baseDir = (target.position + Vector3.up) - origin;
            float halfSpread = barrageSpreadAngle / 2f;

            for (int i = 0; i < barrageCount; i++)
            {
                float t = barrageCount > 1 ? (float)i / (barrageCount - 1) : 0.5f;
                float angleOffset = Mathf.Lerp(-halfSpread, halfSpread, t);
                Vector3 dir = Quaternion.Euler(0, angleOffset, 0) * baseDir;
                // Bleed, not Poison -- Snake Pit's own established gimmick (see PitSnake's
                // Attack()) is Bleed; Poison belongs to the Sunken Ruins/Bog Lurker line.
                Projectile.Spawn(origin, dir, barrageSpeed, barrageDamage, new Color(0.85f, 0.75f, 0.2f),
                    appliedEffect: StatusEffectType.Bleed, effectDuration: bleedDuration, effectMagnitude: bleedDamage);
            }
        }
    }
}
