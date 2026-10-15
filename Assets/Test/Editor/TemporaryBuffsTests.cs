using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;

namespace TheLastKnight.Tests
{
    public class TemporaryBuffsTests
    {
        private float _time;
        private object _buffs;
        private static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);
        private static object Prop(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private static object Kind(string name) => Enum.Parse(Find("TheLastKnight.Stats.TemporaryBuff"), name);
        private bool Active(string name) => (bool)Call(_buffs, "IsActive", Kind(name));
        private float Remaining(string name) => (float)Call(_buffs, "Remaining", Kind(name));

        [SetUp]
        public void SetUp()
        {
            _time = 100f;
            _buffs = Activator.CreateInstance(Find("TheLastKnight.Stats.TemporaryBuffs"), new object[] { (Func<float>)(() => _time) });
        }

        [TestCase("IronWill", 15f)]
        [TestCase("Might", 90f)]
        [TestCase("Swiftness", 90f)]
        [TestCase("Endurance", 90f)]
        [TestCase("Purity", 90f)]
        [TestCase("Regeneration", 90f)]
        [TestCase("Fortitude", 90f)]
        [TestCase("Undying", 90f)]
        [TestCase("CriticalDamage", 90f)]
        public void RefreshAndExactExpiry_AreConsistentInGameplayAndDisplay(string name, float duration)
        {
            Assert.IsFalse(Active(name));
            Call(_buffs, "Apply", Kind(name));
            Assert.AreEqual(duration, Remaining(name));
            _time += duration - 1;
            Assert.IsTrue(Active(name));
            Assert.AreEqual(1f, Remaining(name));
            Call(_buffs, "Apply", Kind(name));
            Assert.AreEqual(duration, Remaining(name), "Refresh resets duration without stacking another instance.");
            var list = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(Find("TheLastKnight.Stats.BuffSnapshot")));
            Call(_buffs, "GetActive", list);
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(name, Prop(list[0], "Kind").ToString());
            Assert.AreEqual(duration, Prop(list[0], "Remaining"));
            Assert.AreEqual(duration, Prop(list[0], "Duration"));
            _time += duration;
            Assert.IsFalse(Active(name));
            Assert.AreEqual(0f, Remaining(name));
            Call(_buffs, "GetActive", list);
            Assert.AreEqual(0, list.Count);
            Assert.IsTrue((bool)Call(_buffs, "Tick"), "Display reads must not consume gameplay invalidation.");
            Assert.IsFalse((bool)Call(_buffs, "Tick"));
        }

        [Test]
        public void Modifiers_DoNotStackOnRefresh_AndReturnToBaseOnExpiry()
        {
            foreach (var name in new[] { "Might", "IronWill", "Swiftness", "Fortitude", "Endurance", "CriticalDamage" })
            {
                Call(_buffs, "Apply", Kind(name));
                Call(_buffs, "Apply", Kind(name));
            }
            Assert.That((float)Prop(_buffs, "AttackBonus"), Is.EqualTo(0.47f).Within(0.0001f));
            Assert.AreEqual(1.25f, Prop(_buffs, "SpeedMultiplier"));
            Assert.AreEqual(1.25f, Prop(_buffs, "FortitudeMultiplier"));
            Assert.AreEqual(0.5f, Prop(_buffs, "StaminaCostMultiplier"));
            Assert.AreEqual(3f, Prop(_buffs, "DexCriticalMultiplier"));
            Assert.AreEqual(5f, Prop(_buffs, "ParryCriticalMultiplier"));
            _time += 15f;
            Assert.AreEqual(0.25f, Prop(_buffs, "AttackBonus"));
            _time += 75f;
            Assert.AreEqual(0f, Prop(_buffs, "AttackBonus"));
            Assert.AreEqual(1f, Prop(_buffs, "SpeedMultiplier"));
            Assert.AreEqual(1f, Prop(_buffs, "FortitudeMultiplier"));
            Assert.AreEqual(1f, Prop(_buffs, "StaminaCostMultiplier"));
            Assert.AreEqual(1.5f, Prop(_buffs, "DexCriticalMultiplier"));
            Assert.AreEqual(2f, Prop(_buffs, "ParryCriticalMultiplier"));
        }

        [Test]
        public void Removal_DoesNotAffectOtherBuffs_AndReapplicationStartsFresh()
        {
            Call(_buffs, "Apply", Kind("Might"));
            Call(_buffs, "Apply", Kind("Fortitude"));
            Call(_buffs, "Remove", Kind("Might"));
            Assert.IsFalse(Active("Might"));
            Assert.IsTrue(Active("Fortitude"));
            Assert.AreEqual(0f, Remaining("Might"));
            _time += 20f;
            Call(_buffs, "Apply", Kind("Might"));
            Assert.AreEqual(90f, Remaining("Might"));
            Assert.AreEqual(70f, Remaining("Fortitude"));
        }
    }
}
