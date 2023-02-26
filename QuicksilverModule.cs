using System.Collections;
using ThunderRoad;
using UnityEngine;
using HarmonyLib;
using System;

namespace Quicksilver
{
    public enum QuicksilverMusic
    {
        None,
        SweetDreams,
        TimeInABottle
    }

    public class MenuBookLoader : CustomData
    {
        MenuBook menu;
        public override void OnCatalogRefresh()
        {
            base.OnCatalogRefresh();
            menu = new MenuBook();
        }
    }

    public class QuicksilverData : MonoBehaviour
    {
        public bool useTimeLord;
        public bool useCustomTimescale;
        public float customTimescale;
        public bool inQuicksilver;
    }

    public class QuicksilverModule : LevelModule
    {
        // TIME LORD VARIABLES
        [Tooltip("Turns on/off the Time Lord mod.")]
        public bool useTimeLord = true;
        [Tooltip("Determines if you want to want to instantly exit out of slow-mo & Quicksilver.")]
        public bool useInstantStop = true;
        [Tooltip("Determines if you want to have lightning indicators appear on the player's wrists when in Quicksilver.")]
        public bool useLightningIndicators = false;
        [Tooltip("Determines if you want to have a lightning trail appear behind the player when in Quicksilver. (BROKEN ATM)")]
        public bool useLightningTrail = false;
        [Tooltip("Determines if you want to feel haptic feedback through your controllers when in Quicksilver.")]
        public bool useHaptics = true;
        [Tooltip("Determines if the player wants to use a separate time scale from the in-game one for Quicksilver.")]
        public bool useCustomTimescale = false;
        [Tooltip("Determines the player's time scale for Quicksilver if they decide to use the custom timescale option. (e.g. 50 = 0.5 timescale)")]
        [Range(1, 100)]
        public float customTimescale = 50f;
        // public bool lightningBody;
        [Tooltip("Determines what music to play in the background when in Quicksilver, if any.")]
        public QuicksilverMusic backgroundMusic = QuicksilverMusic.None;
        [Tooltip("Determines the volume of the background music.")]
        [Range(0, 100)]
        public float musicVolume = 100.0f;
        [Tooltip("Determines how fast the player moves when in Quicksilver.")]
        [Range(0, 100)]
        public float movementSpeed = 22.0f;
        public static QuicksilverData data;

        // SAVED DATA
        private static bool inQuicksilver;
        private bool orgPlayerFallDamage;
        private bool orgHealthVignette;
        private bool orgHaptics;
        private static float orgTimeScale;
        private Locomotion.CrouchMode orgMode;
        private EffectInstance leftInstance, rightInstance, trailInstance;
        private AudioSource music;
        private float quicksilverScale;

        public override IEnumerator OnLoadCoroutine()
        {
            data = GameManager.local.gameObject.AddComponent<QuicksilverData>();
            music = GameManager.local.gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            Player.crouchOnJump = false;
            new Harmony("Use").PatchAll();
            return base.OnLoadCoroutine();
        }

        [HarmonyPatch(typeof(SpellPowerSlowTime), "Use")]
        class SpellPowerSlowTimePatch
        {
            public static bool Prefix()
            {
                if ((PlayerControl.handRight.usePressed || PlayerControl.handLeft.usePressed) && data.useCustomTimescale && data.useTimeLord)
                {
                    orgTimeScale = Player.currentCreature.mana.GetPowerSlowTime().scale;
                    Player.currentCreature.mana.GetPowerSlowTime().scale = data.customTimescale / 100f;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(PlayerControl), "Jump")]
        class JumpPatch
        {
            public static bool Prefix(bool active)
            {
                if (active && data.inQuicksilver && Player.local.locomotion.isGrounded)
                {
                    Player.local.locomotion.rb.AddForce(new Vector3(0, Vector3.up.y * Physics.gravity.magnitude / Mathf.Pow(Time.timeScale, 2) * EvalFunc(Time.timeScale), 0), ForceMode.VelocityChange);
                }

                return true;
            }

            // Don't ask where this comes from.
            private static float EvalFunc(float x)
            {
                return 0.506f * x + 4.11f * Mathf.Pow(10f, -3f);
            }
        }

        private void InitValues()
        {
            data.useTimeLord = useTimeLord;
            data.useCustomTimescale = useCustomTimescale;
            data.customTimescale = customTimescale;
            data.inQuicksilver = inQuicksilver;
            music.volume = musicVolume / 100f;

            try
            {
                MenuBook.local.lerpSpeed = 3f / Time.timeScale;
                MenuBook.local.closeDelay = 1f * Time.timeScale;
                MenuBook.local.book.GetBookAnimator().speed = 3f / Time.timeScale;
                MenuBook.local.book.GetPageAnimator().speed = 5.6f / Time.timeScale;
            }
            catch { }
        }

        public override void Update()
        {
            base.Update();
            InitValues();

            if (Player.currentCreature == null) return;
            switch (GameManager.slowMotionState)
            {
                case GameManager.SlowMotionState.Disabled:
                    if (inQuicksilver)
                        StopQuicksilver();
                    break;
                case GameManager.SlowMotionState.Starting:
                    if (useTimeLord && (PlayerControl.handRight.usePressed || PlayerControl.handLeft.usePressed))
                        StartQuicksilver();
                    break;
                case GameManager.SlowMotionState.Stopping:
                    if (useInstantStop)
                        GameManager.StopSlowMotion();
                    if (inQuicksilver)
                        StopQuicksilver();
                    break;
            }

            if (inQuicksilver)
            {
                Player.local.locomotion.rb.velocity = new Vector3(
                    Player.local.locomotion.moveDirection.x / Time.timeScale * movementSpeed,
                    Player.local.locomotion.rb.velocity.y,
                    Player.local.locomotion.moveDirection.z / Time.timeScale * movementSpeed);
                GameManager.options.rumble = useHaptics;

                UpdateJoints(true);
            }
        }

        private void ChangeColors(ParticleSystem system)
        {
            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(Color.red);
            var main = system.main;
            main.startColor = new ParticleSystem.MinMaxGradient(Color.red);
            Debug.LogWarning("Changed color");
        }

        private void StartQuicksilver()
        {
            // Indicate that the player is in Quicksilver mode
            inQuicksilver = true;

            // Check if they are using lightning indicators
            if (useLightningIndicators)
            {
                EffectData lightningEffect = Catalog.GetData<EffectData>(Catalog.GetData<SpellCastLightning>("Lightning").chargeEffectId);
                lightningEffect.volumeDb = float.MinValue;

                leftInstance = lightningEffect.Spawn(Player.local.handLeft.ragdollHand.transform);
/*                foreach (Effect effect in leftInstance.effects)
                {
                    //Debug.LogWarning(effect);
                    if (effect is EffectVfx effectVfx)
                    {
                        //Debug.Log("HELLO: " + effectVfx.vfx.GetGradient(effectVfx.vfx.GetInstanceID()));
                    }
                }*/
                leftInstance.SetIntensity(1f);
                leftInstance.Play();
                rightInstance = lightningEffect.Spawn(Player.local.handRight.ragdollHand.transform);
                rightInstance.SetIntensity(1f);
                rightInstance.Play();
            }

            // Check if they are using lightning trail
            if (useLightningTrail)
            {
                trailInstance = Catalog.GetData<EffectData>("ImbueLightning").Spawn(Player.local.creature.ragdoll.meshRootBone);
                trailInstance.SetIntensity(1f);
                trailInstance.Play();
            }

            // Check if they are using background music
            if (backgroundMusic != QuicksilverMusic.None)
            {
                if (backgroundMusic == QuicksilverMusic.SweetDreams)
                    Catalog.LoadAssetAsync<AudioClip>("ChillioX.Quicksilver.SweetDreams", value => music.clip = value, "ChillioX");
                else
                    Catalog.LoadAssetAsync<AudioClip>("ChillioX.Quicksilver.TimeInABottle", value => music.clip = value, "ChillioX");
                music.Play();
            }

            /*if ()
            {
                EffectData bodyEffect = Catalog.GetData<EffectData>(Catalog.GetData<SpellCastLightning>("Lightning").boltLoopEffectId);
                bodyInstance = bodyEffect.Spawn(Player.local.creature.ragdoll.transform);
                bodyInstance.Play();
            }*/

            // Gather original settings
            orgPlayerFallDamage = Player.fallDamage;
            orgHealthVignette = CameraEffects.healthVignette;
            orgMode = GameManager.options.stickCrouchMode;
            orgHaptics = GameManager.options.rumble;

            // Remove red vignette because it is really annoying
            CameraEffects.RefreshHealth();

            GameManager.options.stickCrouchMode = Locomotion.CrouchMode.Disabled;
            CameraEffects.healthVignette = false;
            Player.fallDamage = false;
            UpdateQuicksilver(true);
            Player.local.locomotion.customGravity = 1 / Mathf.Pow(Time.timeScale, 2);
            quicksilverScale = Time.timeScale;
        }

        private void UpdateQuicksilver(bool entering)
        {
            // times & durations: Multiply
            // Speeds & rates: Divide

            // SPEED MODIFIERS
            Player.currentCreature.data.ragdollData.fingerSpeed = 5f / Time.timeScale;
            Player.currentCreature.animator.speed = 1f / Time.timeScale;
            Player.local.locomotion.airSpeed = entering ? Player.local.locomotion.forwardSpeed : 0.04f;

            // TURN MODIFIERS
            Player.currentCreature.turnSpeed = 6f / Time.timeScale;
            Player.local.locomotion.turnSpeed = 2f / Time.timeScale;
            PlayerControl.local.snapTurnDelay = 0.25f * Time.timeScale;

            // LEFT FOOT MODIFIERS
            Player.local.footLeft.kickExtendDuration = 0.2f * Time.timeScale;
            Player.local.footLeft.kickStayDuration = 0.1f * Time.timeScale;
            Player.local.footLeft.kickReturnDuration = 0.4f * Time.timeScale;

            // RIGHT FOOT MODIFIERS
            Player.local.footRight.kickExtendDuration = 0.2f * Time.timeScale;
            Player.local.footRight.kickStayDuration = 0.1f * Time.timeScale;
            Player.local.footRight.kickReturnDuration = 0.4f * Time.timeScale;
        }

        private void StopQuicksilver()
        {
            // Indicate that the player is no longer in quicksilver mode
            inQuicksilver = false;

            // Check if they are using custom timescale. Reset scale as necessary.
            if (data.useCustomTimescale)
                Player.currentCreature.mana.GetPowerSlowTime().scale = orgTimeScale;

            // Check if they are using lightning indicators. Stop as necessary.
            if (useLightningIndicators)
            {
                leftInstance.Stop();
                rightInstance.Stop();
            }

            // Check if they are using lightning trail. Stop as necessary.
            if (useLightningTrail)
                trailInstance.Stop();

            // Check if they are using background music. Stop as necessary.
            if (backgroundMusic != QuicksilverMusic.None)
                music.Stop();

            // Restore all original value
            Player.fallDamage = orgPlayerFallDamage;
            CameraEffects.healthVignette = orgHealthVignette;
            GameManager.options.stickCrouchMode = orgMode;
            GameManager.options.rumble = orgHaptics;

            // Reset the player attributes to normal scale
            UpdateQuicksilver(false);

            // Reset the custom gravity felt by the player
            Player.local.locomotion.customGravity = 0f;

            // Stops the player from jumping and flying super high into the sky
            Player.local.locomotion.rb.velocity = new Vector3(
                Player.local.locomotion.moveDirection.x,
                Player.local.locomotion.rb.velocity.y * Mathf.Pow(quicksilverScale, 2),
                Player.local.locomotion.moveDirection.z);

            UpdateJoints(false);
        }

        private void UpdateJoints(bool isEntering)
        {
            if (isEntering)
            {
                if (Player.currentCreature.handLeft.grabbedHandle)
                    Player.local.handLeft.ragdollHand.grabbedHandle.SetJointDrive(
                        new Vector2(100f / Time.timeScale, 1),
                        new Vector2(100f / Time.timeScale, 1));
                else
                    Player.local.handLeft.link.SetJointConfig(
                        Player.local.handLeft.link.controllerJoint,
                        new Vector2(100f / Time.timeScale, 1),
                        new Vector2(100f / Time.timeScale, 1),
                        Player.currentCreature.data.forceMaxPosition * 100 / Time.timeScale,
                        Player.currentCreature.data.forceMaxRotation * 100 / Time.timeScale);
                if (Player.currentCreature.handRight.grabbedHandle)
                    Player.local.handRight.ragdollHand.grabbedHandle.SetJointDrive(
                        new Vector2(100f / Time.timeScale, 1),
                        new Vector2(100f / Time.timeScale, 1));
                else
                    Player.local.handRight.link.SetJointConfig(
                        Player.local.handRight.link.controllerJoint,
                        new Vector2(100f / Time.timeScale, 1),
                        new Vector2(100f / Time.timeScale, 1),
                        Player.currentCreature.data.forceMaxPosition * 100 / Time.timeScale,
                        Player.currentCreature.data.forceMaxRotation * 100 / Time.timeScale);
            }
            else
            {
                if (Player.currentCreature.handLeft.grabbedHandle)
                    Player.local.handLeft.ragdollHand.grabbedHandle.RefreshAllJointDrives();
                else
                    Player.local.handLeft.link.RefreshJointConfig();
                if (Player.currentCreature.handRight.grabbedHandle)
                    Player.local.handRight.ragdollHand.grabbedHandle.RefreshAllJointDrives();
                else
                    Player.local.handRight.link.RefreshJointConfig();
            }
        }
    }
}
