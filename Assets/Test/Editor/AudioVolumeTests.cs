using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace TheLastKnight.Tests
{
    public class AudioVolumeTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        private Type _managerType;
        private GameObject _holder;
        private GameObject _enemy;
        private Component _manager;
        private readonly float[] _savedVolumes = new float[3];
        private readonly bool[] _savedKeys = new bool[3];
        private readonly string[] _keys = { "MasterVolume", "MusicVolume", "EffectsVolume" };

        private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).First(t => t != null);

        private void Register(AudioSource source) => _managerType.GetMethod("RegisterEffectsSource")
            .Invoke(null, new object[] { source });

        private void SetVolumes(float master, float music, float effects) => _managerType.GetMethod("SetVolumes")
            .Invoke(_manager, new object[] { master, music, effects });

        [SetUp]
        public void SetUp()
        {
            _managerType = RuntimeType("TheLastKnight.Audio.AudioManager");
            Assert.That((UnityEngine.Object)_managerType.GetProperty("Instance").GetValue(null) == null, Is.True);
            _managerType.GetMethod("ResetEffectsSources", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            for (int i = 0; i < _keys.Length; i++)
            {
                _savedKeys[i] = PlayerPrefs.HasKey(_keys[i]);
                _savedVolumes[i] = PlayerPrefs.GetFloat(_keys[i]);
                PlayerPrefs.SetFloat(_keys[i], 1f);
            }
            _holder = new GameObject("Test_AudioVolumes");
            _manager = _holder.AddComponent(_managerType);
            // EditMode does not run Awake. Initialize only the audio settings dependencies.
            foreach (string field in new[] { "_bgmA", "_bgmB", "_sfx", "_loopingSfx", "_stoppableSfx" })
                _managerType.GetField(field, PrivateInstance).SetValue(_manager, _holder.AddComponent<AudioSource>());
            _managerType.GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { _manager });
            _enemy = new GameObject("Test_EnemyAudio");
        }

        [TearDown]
        public void TearDown()
        {
            if (_enemy != null) UnityEngine.Object.DestroyImmediate(_enemy);
            if (_holder != null) UnityEngine.Object.DestroyImmediate(_holder);
            if (_managerType != null)
            {
                _managerType.GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { null });
                _managerType.GetMethod("ResetEffectsSources", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            }
            for (int i = 0; i < _keys.Length; i++)
            {
                if (_savedKeys[i]) PlayerPrefs.SetFloat(_keys[i], _savedVolumes[i]);
                else PlayerPrefs.DeleteKey(_keys[i]);
            }
        }

        [Test]
        public void EffectsZero_MutesExistingAndNewSources_AndRestoresAuthoredGain()
        {
            var existing = _enemy.AddComponent<AudioSource>();
            existing.volume = 0.8f;
            existing.loop = true;
            Register(existing);
            SetVolumes(0.5f, 0.7f, 0f);
            Assert.That(existing.volume, Is.Zero);
            var spawned = _enemy.AddComponent<AudioSource>();
            spawned.volume = 0.6f;
            Register(spawned);
            Register(existing);
            Assert.That(spawned.volume, Is.Zero);
            SetVolumes(0.5f, 0.7f, 0.5f);
            Assert.That(existing.volume, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(spawned.volume, Is.EqualTo(0.15f).Within(0.0001f));
            var bgm = (AudioSource)_managerType.GetField("_bgmA", PrivateInstance).GetValue(_manager);
            Assert.That(bgm.volume, Is.EqualTo(0.35f).Within(0.0001f));
            SetVolumes(0f, 1f, 1f);
            Assert.That(existing.volume, Is.Zero);
            Assert.That(bgm.volume, Is.Zero);
        }

        [Test]
        public void SourceCreatedBeforeManager_UsesSavedSettings_AndRetainsBaseGain()
        {
            _managerType.GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { null });
            PlayerPrefs.SetFloat("MasterVolume", 0.5f);
            PlayerPrefs.SetFloat("EffectsVolume", 0f);
            var source = _enemy.AddComponent<AudioSource>();
            source.volume = 0.8f;
            Register(source);
            Assert.That(source.volume, Is.Zero);
            _managerType.GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { _manager });
            SetVolumes(0.5f, 1f, 1f);
            Assert.That(source.volume, Is.EqualTo(0.4f).Within(0.0001f));
        }

        [Test]
        public void UpdatingFadeGain_RespectsMuteAndRestoresCurrentFade()
        {
            var source = _enemy.AddComponent<AudioSource>();
            Register(source);
            SetVolumes(1f, 1f, 0f);
            _managerType.GetMethod("SetEffectsSourceVolume").Invoke(null, new object[] { source, 0.3f });
            Assert.That(source.volume, Is.Zero);
            SetVolumes(0.5f, 1f, 1f);
            Assert.That(source.volume, Is.EqualTo(0.15f).Within(0.0001f));
        }

        [TestCase("BlueSlimeAudioController")]
        [TestCase("SkullwolfAudioController")]
        [TestCase("FoxAudioController")]
        [TestCase("VolcanoxAudioController")]
        [TestCase("DragonSfxController")]
        public void EnemyControllerAwake_RegistersAllItsSources(string controllerName)
        {
            SetVolumes(1f, 1f, 0f);
            _enemy.AddComponent<AudioSource>();
            _enemy.AddComponent<AudioSource>();
            var controllerType = RuntimeType("TheLastKnight.AI." + controllerName);
            var controller = _enemy.AddComponent(controllerType);
            if (controllerName == "DragonSfxController")
                controllerType.GetField("_attackAudioSource", PrivateInstance)
                    .SetValue(controller, _enemy.GetComponents<AudioSource>()[1]);
            controllerType.GetMethod("Awake", PrivateInstance).Invoke(controller, null);
            // Some controllers use one source; assert each declared source they actually use.
            foreach (var field in controllerType.GetFields(PrivateInstance).Where(f => f.FieldType == typeof(AudioSource)))
            {
                var source = (AudioSource)field.GetValue(controller);
                if (source != null) Assert.That(source.volume, Is.Zero, field.Name);
            }
            SetVolumes(1f, 1f, 1f);
            var mainSource = _enemy.GetComponent<AudioSource>();
            Assert.That(mainSource.volume, Is.GreaterThan(0f));
        }

        [Test]
        public void DestroyedSource_DoesNotBreakLaterVolumeChanges()
        {
            var source = _enemy.AddComponent<AudioSource>();
            Register(source);
            UnityEngine.Object.DestroyImmediate(source);
            Assert.DoesNotThrow(() => SetVolumes(1f, 1f, 0f));
        }
    }
}
