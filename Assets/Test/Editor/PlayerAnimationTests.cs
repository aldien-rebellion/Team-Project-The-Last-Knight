using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Linq;

namespace TheLastKnight.Tests
{
    /// <summary>
    /// EditMode tests for Player animation clips and Animator Controller.
    /// Validates sprite keyframes, clip settings, controller states, parameters, and transitions.
    /// </summary>
    [TestFixture]
    public class PlayerAnimationTests
    {
        private const string AnimationFolder = "Assets/Animations/";
        private const string SpriteFolder = "Assets/Sprites/Player/";
        private const string ControllerPath = "Assets/Animations/_Player.controller";

        private AnimatorController _controller;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        }

        // ============================================================
        // 1. ANIMATION CLIP EXISTENCE
        // ============================================================

        private static readonly string[] ExpectedClips =
            { "Idle", "Walk", "Run", "Attack", "Dash", "Jump", "Dead", "Excalibur" };

        [Test, TestCaseSource(nameof(ExpectedClips))]
        public void AnimationClip_Exists(string clipName)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationFolder + clipName + ".anim");
            Assert.IsNotNull(clip, $"Animation clip '{clipName}.anim' not found at {AnimationFolder}");
        }

        // ============================================================
        // 2. SPRITE KEYFRAME VALIDATION
        // ============================================================

        private static readonly object[] ClipSpriteData =
        {
            new object[] { "Idle",      "player-idle",      3 },
            new object[] { "Walk",      "player-walk",      6 },
            new object[] { "Run",       "player-run",       4 },
            new object[] { "Attack",    "player-attack",    3 },
            new object[] { "Dash",      "player-dash",      4 },
            new object[] { "Jump",      "player-jump",      2 },
            new object[] { "Dead",      "player-dead",      3 },
            new object[] { "Excalibur", "player-excalibur", 3 },
        };

        [Test, TestCaseSource(nameof(ClipSpriteData))]
        public void AnimationClip_HasCorrectSpriteKeyframeCount(string clipName, string spriteBase, int expectedCount)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationFolder + clipName + ".anim");
            Assert.IsNotNull(clip, $"Clip '{clipName}' not found");

            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            var spriteBinding = bindings.FirstOrDefault(b =>
                b.type == typeof(SpriteRenderer) && b.propertyName == "m_Sprite");

            Assert.IsTrue(spriteBinding.type != null,
                $"Clip '{clipName}' has no SpriteRenderer.m_Sprite curve");

            var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, spriteBinding);
            Assert.AreEqual(expectedCount, keyframes.Length,
                $"Clip '{clipName}' expected {expectedCount} keyframes but has {keyframes.Length}");
        }

        [Test, TestCaseSource(nameof(ClipSpriteData))]
        public void AnimationClip_AllSpritesAreNotNull(string clipName, string spriteBase, int expectedCount)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationFolder + clipName + ".anim");
            Assert.IsNotNull(clip);

            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            var spriteBinding = bindings.FirstOrDefault(b =>
                b.type == typeof(SpriteRenderer) && b.propertyName == "m_Sprite");
            Assert.IsTrue(spriteBinding.type != null);

            var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, spriteBinding);
            for (int i = 0; i < keyframes.Length; i++)
            {
                Assert.IsNotNull(keyframes[i].value,
                    $"Clip '{clipName}' keyframe {i} (time={keyframes[i].time:F3}s) has a null sprite reference");
            }
        }

        [Test, TestCaseSource(nameof(ClipSpriteData))]
        public void AnimationClip_SpritesMatchExpectedFiles(string clipName, string spriteBase, int expectedCount)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationFolder + clipName + ".anim");
            Assert.IsNotNull(clip);

            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            var spriteBinding = bindings.FirstOrDefault(b =>
                b.type == typeof(SpriteRenderer) && b.propertyName == "m_Sprite");
            Assert.IsTrue(spriteBinding.type != null);

            var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, spriteBinding);

            for (int i = 0; i < keyframes.Length; i++)
            {
                var sprite = keyframes[i].value as Sprite;
                Assert.IsNotNull(sprite, $"Keyframe {i} is not a Sprite");

                string expectedNamePrefix = spriteBase + (i + 1);
                Assert.IsTrue(sprite.name.StartsWith(expectedNamePrefix),
                    $"Clip '{clipName}' keyframe {i}: expected sprite name starting with '{expectedNamePrefix}' but got '{sprite.name}'");
            }
        }

        // ============================================================
        // 3. CLIP SETTINGS (LOOP, FRAME RATE)
        // ============================================================

        private static readonly object[] ClipSettingsData =
        {
            //             clip name,    expectedLoop, minFrameRate
            new object[] { "Idle",       true,  4f },
            new object[] { "Walk",       true,  8f },
            new object[] { "Run",        true,  8f },
            new object[] { "Attack",     false, 8f },
            new object[] { "Dash",       false, 8f },
            new object[] { "Jump",       false, 4f },
            new object[] { "Dead",       false, 4f },
            new object[] { "Excalibur",  false, 4f },
        };

        [Test, TestCaseSource(nameof(ClipSettingsData))]
        public void AnimationClip_LoopSetting(string clipName, bool expectedLoop, float minFrameRate)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationFolder + clipName + ".anim");
            Assert.IsNotNull(clip);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            Assert.AreEqual(expectedLoop, settings.loopTime,
                $"Clip '{clipName}' loopTime expected={expectedLoop} actual={settings.loopTime}");
        }

        [Test, TestCaseSource(nameof(ClipSettingsData))]
        public void AnimationClip_FrameRateIsReasonable(string clipName, bool expectedLoop, float minFrameRate)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationFolder + clipName + ".anim");
            Assert.IsNotNull(clip);

            Assert.GreaterOrEqual(clip.frameRate, minFrameRate,
                $"Clip '{clipName}' frameRate {clip.frameRate} is below minimum {minFrameRate}");
        }

        // ============================================================
        // 4. ANIMATOR CONTROLLER VALIDATION
        // ============================================================

        [Test]
        public void Controller_Exists()
        {
            Assert.IsNotNull(_controller, $"AnimatorController not found at {ControllerPath}");
        }

        [Test]
        public void Controller_HasOneLayer()
        {
            Assert.IsNotNull(_controller);
            Assert.AreEqual(1, _controller.layers.Length, "Controller should have exactly 1 layer");
        }

        [Test, TestCaseSource(nameof(ExpectedClips))]
        public void Controller_HasState(string stateName)
        {
            Assert.IsNotNull(_controller);
            var rootSM = _controller.layers[0].stateMachine;
            var state = rootSM.states.FirstOrDefault(s => s.state.name == stateName);
            Assert.IsNotNull(state.state, $"Controller is missing state '{stateName}'");
        }

        [Test]
        public void Controller_DefaultStateIsIdle()
        {
            Assert.IsNotNull(_controller);
            var rootSM = _controller.layers[0].stateMachine;
            Assert.IsNotNull(rootSM.defaultState, "Controller has no default state");
            Assert.AreEqual("Idle", rootSM.defaultState.name,
                $"Default state should be 'Idle' but is '{rootSM.defaultState.name}'");
        }

        [Test, TestCaseSource(nameof(ExpectedClips))]
        public void Controller_StateHasMotionAssigned(string stateName)
        {
            Assert.IsNotNull(_controller);
            var rootSM = _controller.layers[0].stateMachine;
            var stateInfo = rootSM.states.FirstOrDefault(s => s.state.name == stateName);
            Assert.IsNotNull(stateInfo.state, $"State '{stateName}' not found");
            Assert.IsNotNull(stateInfo.state.motion,
                $"State '{stateName}' has no motion (AnimationClip) assigned");
        }

        // ============================================================
        // 5. PARAMETER VALIDATION
        // ============================================================

        // Parameters used by PlayerController.LateUpdate()
        private static readonly string[] RequiredBoolParams =
            { "IsIdle", "IsRunning", "IsAttacking" };

        // Additional parameters for future/complete coverage
        private static readonly string[] AllExpectedBoolParams =
            { "IsIdle", "IsRunning", "IsAttacking", "IsDashing", "IsJumping", "IsDead", "UseExcalibur" };

        [Test, TestCaseSource(nameof(RequiredBoolParams))]
        public void Controller_HasRequiredBoolParameter(string paramName)
        {
            Assert.IsNotNull(_controller);
            var param = _controller.parameters.FirstOrDefault(p => p.name == paramName);
            Assert.IsNotNull(param, $"Controller missing required parameter '{paramName}'");
            Assert.AreEqual(AnimatorControllerParameterType.Bool, param.type,
                $"Parameter '{paramName}' should be Bool but is {param.type}");
        }

        [Test, TestCaseSource(nameof(AllExpectedBoolParams))]
        public void Controller_HasBoolParameter(string paramName)
        {
            Assert.IsNotNull(_controller);
            var param = _controller.parameters.FirstOrDefault(p => p.name == paramName);
            Assert.IsNotNull(param, $"Controller missing parameter '{paramName}'");
            Assert.AreEqual(AnimatorControllerParameterType.Bool, param.type,
                $"Parameter '{paramName}' should be Bool but is {param.type}");
        }

        // ============================================================
        // 6. TRANSITION VALIDATION
        // ============================================================

        [Test]
        public void Transition_IdleToWalk_OnIsRunning()
        {
            Assert.IsNotNull(_controller);
            var rootSM = _controller.layers[0].stateMachine;
            var idleState = rootSM.states.FirstOrDefault(s => s.state.name == "Idle").state;
            Assert.IsNotNull(idleState, "Idle state not found");

            var transition = idleState.transitions
                .FirstOrDefault(t => t.destinationState != null && t.destinationState.name == "Walk");
            Assert.IsNotNull(transition, "No transition from Idle to Walk");

            var condition = transition.conditions
                .FirstOrDefault(c => c.parameter == "IsRunning");
            Assert.IsTrue(condition.parameter == "IsRunning",
                "Idle->Walk transition should use 'IsRunning' parameter");
            Assert.AreEqual(AnimatorConditionMode.If, condition.mode,
                "Idle->Walk condition should be 'If' (true)");
        }

        [Test]
        public void Transition_WalkToIdle_OnIsIdle()
        {
            Assert.IsNotNull(_controller);
            var rootSM = _controller.layers[0].stateMachine;
            var walkState = rootSM.states.FirstOrDefault(s => s.state.name == "Walk").state;
            Assert.IsNotNull(walkState, "Walk state not found");

            var transition = walkState.transitions
                .FirstOrDefault(t => t.destinationState != null && t.destinationState.name == "Idle");
            Assert.IsNotNull(transition, "No transition from Walk to Idle");

            var condition = transition.conditions
                .FirstOrDefault(c => c.parameter == "IsIdle");
            Assert.IsTrue(condition.parameter == "IsIdle",
                "Walk->Idle transition should use 'IsIdle' parameter");
        }

        [Test]
        public void Transition_AnyStateToAttack_OnIsAttacking()
        {
            Assert.IsNotNull(_controller);
            var rootSM = _controller.layers[0].stateMachine;

            var anyTransition = rootSM.anyStateTransitions
                .FirstOrDefault(t => t.destinationState != null && t.destinationState.name == "Attack");
            Assert.IsNotNull(anyTransition, "No AnyState transition to Attack");

            var condition = anyTransition.conditions
                .FirstOrDefault(c => c.parameter == "IsAttacking");
            Assert.IsTrue(condition.parameter == "IsAttacking",
                "AnyState->Attack should use 'IsAttacking' parameter");
            Assert.AreEqual(AnimatorConditionMode.If, condition.mode);
        }

        [Test]
        public void Transition_AnyStateToDash_OnIsDashing()
        {
            Assert.IsNotNull(_controller);
            var rootSM = _controller.layers[0].stateMachine;

            var anyTransition = rootSM.anyStateTransitions
                .FirstOrDefault(t => t.destinationState != null && t.destinationState.name == "Dash");
            Assert.IsNotNull(anyTransition, "No AnyState transition to Dash");

            var condition = anyTransition.conditions
                .FirstOrDefault(c => c.parameter == "IsDashing");
            Assert.IsTrue(condition.parameter == "IsDashing",
                "AnyState->Dash should use 'IsDashing' parameter");
        }

        [Test]
        public void Transition_AnyStateToJump_OnIsJumping()
        {
            Assert.IsNotNull(_controller);
            var rootSM = _controller.layers[0].stateMachine;

            var anyTransition = rootSM.anyStateTransitions
                .FirstOrDefault(t => t.destinationState != null && t.destinationState.name == "Jump");
            Assert.IsNotNull(anyTransition, "No AnyState transition to Jump");

            var condition = anyTransition.conditions
                .FirstOrDefault(c => c.parameter == "IsJumping");
            Assert.IsTrue(condition.parameter == "IsJumping",
                "AnyState->Jump should use 'IsJumping' parameter");
        }

        [Test]
        public void Transition_AnyStateToDead_OnIsDead()
        {
            Assert.IsNotNull(_controller);
            var rootSM = _controller.layers[0].stateMachine;

            var anyTransition = rootSM.anyStateTransitions
                .FirstOrDefault(t => t.destinationState != null && t.destinationState.name == "Dead");
            Assert.IsNotNull(anyTransition, "No AnyState transition to Dead");

            var condition = anyTransition.conditions
                .FirstOrDefault(c => c.parameter == "IsDead");
            Assert.IsTrue(condition.parameter == "IsDead",
                "AnyState->Dead should use 'IsDead' parameter");
        }

        [Test]
        public void Transition_AnyStateToExcalibur_OnUseExcalibur()
        {
            Assert.IsNotNull(_controller);
            var rootSM = _controller.layers[0].stateMachine;

            var anyTransition = rootSM.anyStateTransitions
                .FirstOrDefault(t => t.destinationState != null && t.destinationState.name == "Excalibur");
            Assert.IsNotNull(anyTransition, "No AnyState transition to Excalibur");

            var condition = anyTransition.conditions
                .FirstOrDefault(c => c.parameter == "UseExcalibur");
            Assert.IsTrue(condition.parameter == "UseExcalibur",
                "AnyState->Excalibur should use 'UseExcalibur' parameter");
        }

        // ============================================================
        // 7. ONE-SHOT CLIPS HAVE EXIT-TIME TRANSITIONS BACK
        // ============================================================

        private static readonly object[] OneShotClipTransitions =
        {
            new object[] { "Attack",    "IsAttacking"  },
            new object[] { "Dash",      "IsDashing"    },
            new object[] { "Excalibur", "UseExcalibur" },
        };

        [Test, TestCaseSource(nameof(OneShotClipTransitions))]
        public void Transition_OneShotReturnsToIdle_WithExitTime(string stateName, string paramName)
        {
            Assert.IsNotNull(_controller);
            var rootSM = _controller.layers[0].stateMachine;
            var state = rootSM.states.FirstOrDefault(s => s.state.name == stateName).state;
            Assert.IsNotNull(state, $"State '{stateName}' not found");

            var returnTransition = state.transitions
                .FirstOrDefault(t => t.destinationState != null && t.destinationState.name == "Idle");
            Assert.IsNotNull(returnTransition,
                $"State '{stateName}' has no transition back to Idle");
            Assert.IsTrue(returnTransition.hasExitTime,
                $"Transition {stateName}->Idle should have exitTime enabled (one-shot clip)");
        }

        // ============================================================
        // 8. PLAYER GAMEOBJECT VALIDATION
        // ============================================================

        [Test]
        public void Player_SpriteRendererHasSprite()
        {
            // Load from scene - find Player prefab or check current scene
            var player = GameObject.Find("Player");
            if (player == null)
            {
                Assert.Inconclusive("Player GameObject not found in current scene. Open a scene with Player to run this test.");
                return;
            }

            var sr = player.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(sr, "Player has no SpriteRenderer");
            Assert.IsNotNull(sr.sprite, "Player SpriteRenderer.sprite is null — no default sprite set");
        }

        [Test]
        public void Player_AnimatorHasController()
        {
            var player = GameObject.Find("Player");
            if (player == null)
            {
                Assert.Inconclusive("Player GameObject not found in current scene.");
                return;
            }

            var animator = player.GetComponent<Animator>();
            Assert.IsNotNull(animator, "Player has no Animator component");
            Assert.IsNotNull(animator.runtimeAnimatorController,
                "Player Animator has no RuntimeAnimatorController assigned");
        }
    }
}
