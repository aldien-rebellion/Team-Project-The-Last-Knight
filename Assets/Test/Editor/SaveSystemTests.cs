using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace TheLastKnight.Tests
{
    public class SaveSystemTests
    {
        private Type _system, _dataType;
        private string _directory, _path;
        private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("TheLastKnight.Core." + name)).First(t => t != null);

        [SetUp]
        public void SetUp()
        {
            _system = RuntimeType("SaveSystem"); _dataType = RuntimeType("PlayerSaveData");
            _directory = Path.Combine(Path.GetTempPath(), "LastKnightSaveTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _path = Path.Combine(_directory, "save.json");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                if (File.Exists(_path + suffix)) File.Delete(_path + suffix);
            Directory.Delete(_directory);
        }

        private object State(int gold = 120) => JsonUtility.FromJson(
            "{\"version\":1,\"initialized\":true,\"scene\":\"CityCenter\",\"position\":{\"x\":5,\"y\":-3,\"z\":0}," +
            "\"hp\":80,\"stamina\":65,\"level\":4,\"exp\":30,\"statPoints\":2,\"strength\":12,\"vitality\":14," +
            "\"dexterity\":11,\"agility\":13,\"gold\":" + gold + ",\"potions\":4,\"runes\":[true,false,true,true]," +
            "\"churchKey\":true,\"introSeen\":true,\"victory\":false,\"difficulty\":2}", _dataType);

        private bool Save(object state) => (bool)_system.GetMethod("Save").Invoke(null, new[] { state, null, _path });
        private bool Load(out object state)
        {
            object[] args = { null, _path };
            bool result = (bool)_system.GetMethod("TryLoad").Invoke(null, args);
            state = args[0]; return result;
        }

        [Test]
        public void AtomicSave_RoundTripsEveryProgressionField()
        {
            var original = State();
            Assert.That(Save(original), Is.True);
            Assert.That(File.Exists(_path + ".tmp"), Is.False);
            Assert.That(Load(out var loaded), Is.True);
            Assert.That(JsonUtility.ToJson(loaded), Is.EqualTo(JsonUtility.ToJson(original)));
        }

        [Test]
        public void CorruptPrimarySave_RecoversPreviousAtomicBackup()
        {
            var previous = State(120);
            Assert.That(Save(previous), Is.True);
            Assert.That(Save(State(200)), Is.True);
            File.WriteAllText(_path, "{broken");
            Assert.That(Load(out var recovered), Is.True);
            Assert.That(JsonUtility.ToJson(recovered), Is.EqualTo(JsonUtility.ToJson(previous)));
        }

        [Test]
        public void InvalidState_DoesNotReplaceValidSave()
        {
            Assert.That(Save(State()), Is.True);
            var invalid = State(999);
            _dataType.GetField("runes").SetValue(invalid, new bool[1]);
            Assert.That(Save(invalid), Is.False);
            Assert.That(Load(out var loaded), Is.True);
            Assert.That((int)_dataType.GetField("gold").GetValue(loaded), Is.EqualTo(120));
        }

        [Test]
        public void CorruptSaveWithoutBackup_IsRejected()
        {
            File.WriteAllText(_path, "{broken");
            Assert.That(Load(out var loaded), Is.False);
            Assert.That(loaded, Is.Null);
        }
    }
}
