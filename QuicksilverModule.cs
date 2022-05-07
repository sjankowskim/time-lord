using System.Collections;
using ThunderRoad;
using UnityEngine;

namespace Quicksilver
{
    public class QuicksilverModule : LevelModule
    {
        public bool instaActivation;
        public bool lightningIndicators;
        public bool lightningTrail;
        public float quicksilverSpeed;

        private bool quicksilverMode;
        private bool orgPlayerFallDamage;
        private bool orgHealthVignette;
        private Locomotion.CrouchMode orgMode;
        private EffectInstance leftInstance, rightInstance, trailInstance;

        public override IEnumerator OnLoadCoroutine()
        {
            Debug.Log("(Quicksilver) Loaded successfully!");
            Player.crouchOnJump = false;
            return base.OnLoadCoroutine();
        }

        public override void Update()
        {
            base.Update();

            if (Player.currentCreature == null) return;
            switch (GameManager.slowMotionState)
            {
                case GameManager.SlowMotionState.Disabled: // If the player is not using instant activation...
                    if (quicksilverMode)
                        StopQuicksilver();
                    break;
                case GameManager.SlowMotionState.Starting:
                    if (PlayerControl.handRight.usePressed || PlayerControl.handLeft.usePressed)
                        StartQuicksilver();
                    break;
                case GameManager.SlowMotionState.Stopping:
                    if (instaActivation)
                        GameManager.StopSlowMotion();
                    if (quicksilverMode)
                        StopQuicksilver();
                    break;
            }

            if (!quicksilverMode) return;
            if (!Player.local.locomotion.isGrounded)
                Player.local.locomotion.rb.AddForce(Physics.gravity / Mathf.Pow(Time.timeScale, 2f), ForceMode.Acceleration);
            Player.local.locomotion.rb.velocity = new Vector3(
                Player.local.locomotion.moveDirection.x / Time.timeScale * quicksilverSpeed,
                Player.local.locomotion.rb.velocity.y,
                Player.local.locomotion.moveDirection.z / Time.timeScale * quicksilverSpeed);
            UpdateJoints(false);
        }

        private void StartQuicksilver()
        {
            /*if (lightningIndicators)
            {
                EffectData lightningEffect = Catalog.GetData<EffectData>(Catalog.GetData<SpellCastLightning>("Lightning").chargeEffectId);
                lightningEffect.volumeDb = float.MinValue;

                leftInstance = lightningEffect.Spawn(Player.local.handLeft.ragdollHand.transform);
                leftInstance.SetIntensity(1f);
                leftInstance.Play();
                lightningEffect.Spawn()
                rightInstance = lightningEffect.Spawn(Player.local.handRight.ragdollHand.transform);
                rightInstance.SetIntensity(1f);
                rightInstance.Play();
            }

            if (lightningTrail)
            {
                Gradient color = new Gradient();
                GradientColorKey[] colorKeys = { new GradientColorKey(Color.magenta, 0.0f), new GradientColorKey(Color.magenta, 1.0f) };
                GradientAlphaKey[] alphaKeys = { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) };
                color.SetKeys(colorKeys, alphaKeys);
                color.mode = GradientMode.Blend;
                EffectData trailEffect = Catalog.GetData<EffectData>("LightningTrail");
                ((EffectModuleShader)trailEffect.modules[trailEffect.modules.Count - 1]).lifeTime = float.MaxValue;
                trailInstance = trailEffect.Spawn(Player.local.creature.ragdoll.meshRootBone);
                trailInstance.SetIntensity(1f);
                trailInstance.Play();
            }*/

            quicksilverMode = true;
            orgPlayerFallDamage = Player.fallDamage;
            orgHealthVignette = GameManager.options.healthVignette;
            orgMode = GameManager.options.stickCrouchMode;
            GameManager.options.stickCrouchMode = Locomotion.CrouchMode.Disabled;
            CameraEffects.healthVignette = false;
            Player.fallDamage = false;
            UpdateQuicksilver();
        }

        private void UpdateQuicksilver()
        {
            /*Debug.LogWarning("Finger Speed: " + Player.currentCreature.data.ragdollData.fingerSpeed);
            Debug.LogWarning("Animator Speed: " + Player.currentCreature.animator.speed);
            Debug.LogWarning("Ragdoll Turn Speed: " + Player.currentCreature.turnSpeed);
            Debug.LogWarning("Player Turn Speed: " + Player.local.locomotion.turnSpeed);
            Debug.LogWarning("SnapTurnDelay Speed: " + PlayerControl.local.snapTurnDelay);*/

            // SPEED MODIFIERS
            Player.currentCreature.data.ragdollData.fingerSpeed = 5f / Time.timeScale; // FINGER SPEED - Found in CreatureData.RagdollData.fingerSpeed
            Player.currentCreature.animator.speed = 1f / Time.timeScale; // ANIMATOR SPEED - Controls how fast the legs move when walking/running

            // TURN MODIFIERS
            Player.currentCreature.turnSpeed = 6f / Time.timeScale; // RAGDOLL TURN SPEED - Rotates the player's ragdoll; found in Creature.turnSpeed
            Player.local.locomotion.turnSpeed = 2f / Time.timeScale; // PLAYER TURN SPEED - Rotates the player's perspective; found in Locomotion.turnSpeed
            PlayerControl.local.snapTurnDelay = 0.25f * Time.timeScale; // SNAP TURN DELAY - Found in PlayerControl.snapTurnDelay

            /*Debug.LogWarning("Finger Speed: " + Player.currentCreature.data.ragdollData.fingerSpeed);
            Debug.LogWarning("Animator Speed: " + Player.currentCreature.animator.speed);
            Debug.LogWarning("Ragdoll Turn Speed: " + Player.currentCreature.turnSpeed);
            Debug.LogWarning("Player Turn Speed: " + Player.local.locomotion.turnSpeed);
            Debug.LogWarning("SnapTurnDelay Speed: " + PlayerControl.local.snapTurnDelay);*/

            
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
           /* if (lightningIndicators)
            {
                leftInstance.Stop();
                rightInstance.Stop();
            }

            if (lightningTrail)
                trailInstance.Stop();*/

            quicksilverMode = false;
            Player.fallDamage = orgPlayerFallDamage;
            CameraEffects.healthVignette = orgHealthVignette;
            GameManager.options.stickCrouchMode = orgMode;
            UpdateQuicksilver();
            Player.local.locomotion.rb.velocity = new Vector3(
                Player.local.locomotion.moveDirection.x,
                Player.local.locomotion.rb.velocity.y,
                Player.local.locomotion.moveDirection.z);
            UpdateJoints(true);
        }

        private void UpdateJoints(bool isExiting)
        {
            if (!isExiting)
            {
                if (Player.currentCreature.handLeft.grabbedHandle)
                    Player.local.handLeft.ragdollHand.grabbedHandle.SetJointDrive(
                        new Vector2(100f / Time.timeScale, 1),
                        new Vector2(100f / Time.timeScale, 1));
                else
                    Player.local.handLeft.link.SetJointConfig(
                        Player.local.handLeft.link.controllerJoint,
                        new Vector2(10f / Time.timeScale, 1),
                        new Vector2(10f / Time.timeScale, 1),
                        Player.currentCreature.data.forceMaxPosition * 10 / Time.timeScale,
                        Player.currentCreature.data.forceMaxRotation * 10 / Time.timeScale);
                if (Player.currentCreature.handRight.grabbedHandle)
                    Player.local.handRight.ragdollHand.grabbedHandle.SetJointDrive(
                        new Vector2(100f / Time.timeScale, 1),
                        new Vector2(100f / Time.timeScale, 1));
                else
                    Player.local.handRight.link.SetJointConfig(
                        Player.local.handRight.link.controllerJoint,
                        new Vector2(10f / Time.timeScale, 1),
                        new Vector2(10f / Time.timeScale, 1),
                        Player.currentCreature.data.forceMaxPosition * 10 / Time.timeScale,
                        Player.currentCreature.data.forceMaxRotation * 10 / Time.timeScale);
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
