using System.Collections;
using ThunderRoad;
using UnityEngine;
using HarmonyLib;

namespace Quicksilver
{
    public enum QuicksilverMusic
    {
        None,
        SweetDreams,
        TimeInABottle
    }

    public class QuicksilverData : MonoBehaviour
    {
        public bool useTimeLord;
        public bool useCustomTimescale;
        public float customTimescale;
    }

    public class QuicksilverModule : LevelModule
    {
        // TIME LORD VARIABLES
        [Tooltip("Turns on/off the Time Lord mod.")]
        public bool useTimeLord = true;
        [Tooltip("Determines if you want to want to instantly exit out of slow-mo & Quicksilver.")]
        public bool instantStop = true;
        [Tooltip("Determines if you want to have lightning indicators appear on the player's wrists when in Quicksilver.")]
        public bool lightningIndicators = false;
        [Tooltip("Determines if you want to have a lightning trail appear behind the player when in Quicksilver. (BROKEN ATM)")]
        public bool lightningTrail = false;
        // public bool lightningBody;
        // public QuicksilverMusic backgroundMusic = QuicksilverMusic.None;
        // public float musicVolume = 1.0f;
        [Tooltip("Determines how fast the player moves in Quicksilver.")]
        [Range(0, 100)]
        public float movementSpeed = 22.0f;
        [Tooltip("Determines if the player wants to use a separate time scale from the in-game one for Quicksilver.")]
        public bool useCustomTimescale = false;
        [Tooltip("Determines the player's time scale for Quicksilver if they decide to use the custom timescale option.")]
        [Range(0, 1)]
        public float customTimescale = 0.50f;
        public static QuicksilverData data;

        // SAVED DATA
        private bool quicksilverMode;
        private bool orgPlayerFallDamage;
        private bool orgHealthVignette;
        private static float orgTimeScale;
        private Locomotion.CrouchMode orgMode;
        private EffectInstance leftInstance, rightInstance, trailInstance, musicInstance;

        public override IEnumerator OnLoadCoroutine()
        {
            data = GameManager.local.gameObject.AddComponent<QuicksilverData>();
            Player.crouchOnJump = false;
            new Harmony("Use").PatchAll();
            return base.OnLoadCoroutine();
        }

        private void InitValues()
        {
            data.useTimeLord = useTimeLord;
            data.useCustomTimescale = useCustomTimescale;
            data.customTimescale = customTimescale;
        }

        [HarmonyPatch(typeof(SpellPowerSlowTime), "Use")]
        class SpellPowerSlowTimePatch
        {
            public static bool Prefix()
            {
                if (data.useCustomTimescale && data.useTimeLord)
                {
                    orgTimeScale = Player.currentCreature.mana.GetPowerSlowTime().scale;
                    Player.currentCreature.mana.GetPowerSlowTime().scale = data.customTimescale;
                }
                return true;
            }
        }

        public override void Update()
        {
            base.Update();
            InitValues();

            if (Player.currentCreature == null) return;
            switch (GameManager.slowMotionState)
            {
                case GameManager.SlowMotionState.Disabled:
                    if (quicksilverMode)
                        StopQuicksilver();
                    break;
                case GameManager.SlowMotionState.Starting:
                    if (useTimeLord && PlayerControl.handRight.usePressed || PlayerControl.handLeft.usePressed)
                        StartQuicksilver();
                    break;
                case GameManager.SlowMotionState.Stopping:
                    if (instantStop)
                        GameManager.StopSlowMotion();
                    if (quicksilverMode)
                        StopQuicksilver();
                    break;
            }

            if (quicksilverMode)
            {
                if (!Player.local.locomotion.isGrounded)
                    Player.local.locomotion.rb.AddForce(Physics.gravity / Mathf.Pow(Time.timeScale, 2f), ForceMode.Acceleration);
                Player.local.locomotion.rb.velocity = new Vector3(
                    Player.local.locomotion.moveDirection.x / Time.timeScale * movementSpeed,
                    Player.local.locomotion.rb.velocity.y,
                    Player.local.locomotion.moveDirection.z / Time.timeScale * movementSpeed);
                UpdateJoints(true);
            }
        }

        private void StartQuicksilver()
        {
            if (lightningIndicators)
            {
                EffectData lightningEffect = Catalog.GetData<EffectData>(Catalog.GetData<SpellCastLightning>("Lightning").chargeEffectId);
                lightningEffect.volumeDb = float.MinValue;

                leftInstance = lightningEffect.Spawn(Player.local.handLeft.ragdollHand.transform);
                leftInstance.SetIntensity(1f);
                leftInstance.Play();
                rightInstance = lightningEffect.Spawn(Player.local.handRight.ragdollHand.transform);
                rightInstance.SetIntensity(1f);
                rightInstance.Play();
            }

            if (lightningTrail)
               Catalog.GetData<EffectData>("ImbueLightning").Spawn(Player.local.creature.ragdoll.meshRootBone).Play();

            /*if (backgroundMusic != QuicksilverMusic.None)
            {
                EffectData music;
                switch (backgroundMusic)
                {
                    default:
                        music = Catalog.GetData<EffectData>("SweetDreams");
                        break;
                    case QuicksilverMusic.TimeInABottle:
                        music = Catalog.GetData<EffectData>("TimeInABottle");
                        break;
                }
                musicInstance = music.Spawn(Player.local.transform);
                if (data.useCustomTimescale)
                {
                    musicInstance.SetSpeed(1 / data.customTimescale);
                    musicInstance.effects.ForEach(e => e.gameObject.GetComponentInChildren<AudioSource>().pitch = 1 / data.customTimescale);
                }
                else
                {
                    musicInstance.SetSpeed(1 / Time.timeScale);
                    musicInstance.effects.ForEach(e => e.gameObject.GetComponentInChildren<AudioSource>().pitch = 1 / Time.timeScale);
                }
                musicInstance.Play();
            }*/

            /*if ()
            {
                EffectData bodyEffect = Catalog.GetData<EffectData>(Catalog.GetData<SpellCastLightning>("Lightning").boltLoopEffectId);
                bodyInstance = bodyEffect.Spawn(Player.local.creature.ragdoll.transform);
                bodyInstance.Play();
            }*/

            quicksilverMode = true;
            orgPlayerFallDamage = Player.fallDamage;
            orgHealthVignette = GameManager.options.healthVignette;
            CameraEffects.RefreshHealth();
            orgMode = GameManager.options.stickCrouchMode;
            GameManager.options.stickCrouchMode = Locomotion.CrouchMode.Disabled;
            CameraEffects.healthVignette = false;
            Player.fallDamage = false;
            UpdateQuicksilver();
        }

        private void UpdateQuicksilver()
        {
            // SPEED MODIFIERS
            Player.currentCreature.data.ragdollData.fingerSpeed = 5f / Time.timeScale; // FINGER SPEED - Found in CreatureData.RagdollData.fingerSpeed
            Player.currentCreature.animator.speed = 1f / Time.timeScale; // ANIMATOR SPEED - Controls how fast the legs move when walking/running
            Player.local.locomotion.airSpeed = 0.02f / Time.timeScale;

            // TURN MODIFIERS
            Player.currentCreature.turnSpeed = 6f / Time.timeScale; // RAGDOLL TURN SPEED - Rotates the player's ragdoll; found in Creature.turnSpeed
            Player.local.locomotion.turnSpeed = 2f / Time.timeScale; // PLAYER TURN SPEED - Rotates the player's perspective; found in Locomotion.turnSpeed
            PlayerControl.local.snapTurnDelay = 0.25f * Time.timeScale; // SNAP TURN DELAY - Found in PlayerControl.snapTurnDelay
            
            // LEFT FOOT MODIFIERS
            Player.currentCreature.footLeft.playerFoot.kickExtendDuration = 0.2f * Time.timeScale; // EXTEND DURATION - Found in PlayerFoot.kickExtendDuration
            Player.currentCreature.footLeft.playerFoot.kickStayDuration = 0.1f * Time.timeScale; // STAY DURATION - Found in PlayerFoot.kickStayDuration
            Player.currentCreature.footLeft.playerFoot.kickReturnDuration = 0.4f * Time.timeScale; // RETURN DURATION - Found in PlayerFoot.kickReturnDuration

            // RIGHT FOOT MODIFIERS
            Player.currentCreature.footRight.playerFoot.kickExtendDuration = 0.2f * Time.timeScale; // EXTEND DURATION - Found in PlayerFoot.kickExtendDuration
            Player.currentCreature.footRight.playerFoot.kickStayDuration = 0.1f * Time.timeScale; // STAY DURATION - Found in PlayerFoot.kickStayDuration
            Player.currentCreature.footRight.playerFoot.kickReturnDuration = 0.4f * Time.timeScale; // RETURN DURATION - Found in PlayerFoot.kickReturnDuration
        }

        private void StopQuicksilver()
        {
            if (data.useCustomTimescale)
                Player.currentCreature.mana.GetPowerSlowTime().scale = orgTimeScale;

            if (leftInstance != null)
            {
                leftInstance.Stop();
                rightInstance.Stop();
                leftInstance = null;
                rightInstance = null;

            }

            if (trailInstance != null)
            {
                trailInstance.Stop();
                trailInstance = null;
            }

            if (musicInstance != null)
            {
                musicInstance.Stop();
                // musicInstance = null;
            }

            quicksilverMode = false;
            Player.fallDamage = orgPlayerFallDamage;
            CameraEffects.healthVignette = orgHealthVignette;
            GameManager.options.stickCrouchMode = orgMode;
            UpdateQuicksilver();
            Player.local.locomotion.rb.velocity = new Vector3(
                Player.local.locomotion.moveDirection.x,
                Player.local.locomotion.rb.velocity.y,
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