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
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
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

        [Test]
        public void MultiWorld_CanSaveAndListMultipleWorlds()
        {
            var propEditor = _system.GetProperty("EditorTestSavePath");
            propEditor.SetValue(null, _path);

            try
            {
                var s1 = State(100);
                _dataType.GetField("worldId").SetValue(s1, "world_alpha");
                _dataType.GetField("saveName").SetValue(s1, "Alpha World");

                var s2 = State(250);
                _dataType.GetField("worldId").SetValue(s2, "world_beta");
                _dataType.GetField("saveName").SetValue(s2, "Beta World");

                var saveMethod = _system.GetMethod("Save", new[] { _dataType, typeof(string).MakeByRefType(), typeof(string) });
                object[] args1 = { s1, null, null };
                Assert.That((bool)saveMethod.Invoke(null, args1), Is.True);

                object[] args2 = { s2, null, null };
                Assert.That((bool)saveMethod.Invoke(null, args2), Is.True);

                var getAllMethod = _system.GetMethod("GetAllSaves");
                var list = (System.Collections.IList)getAllMethod.Invoke(null, null);
                Assert.That(list.Count, Is.GreaterThanOrEqualTo(2));

                // Verify Delete
                var deleteMethod = _system.GetMethod("DeleteWorld");
                object[] delArgs = { "world_alpha", null };
                Assert.That((bool)deleteMethod.Invoke(null, delArgs), Is.True);

                var listAfter = (System.Collections.IList)getAllMethod.Invoke(null, null);
                Assert.That(listAfter.Cast<object>().Any(x => (string)_dataType.GetField("worldId").GetValue(x) == "world_alpha"), Is.False);
            }
            finally
            {
                propEditor.SetValue(null, null);
            }
        }

        [Test]
        public void MultiWorld_RenameWorld_UpdatesSaveNameOnDisk()
        {
            var propEditor = _system.GetProperty("EditorTestSavePath");
            propEditor.SetValue(null, _path);

            try
            {
                var s = State(500);
                _dataType.GetField("worldId").SetValue(s, "world_gamma");
                _dataType.GetField("saveName").SetValue(s, "Old Name");

                var saveMethod = _system.GetMethod("Save", new[] { _dataType, typeof(string).MakeByRefType(), typeof(string) });
                object[] args = { s, null, null };
                Assert.That((bool)saveMethod.Invoke(null, args), Is.True);

                var renameMethod = _system.GetMethod("RenameWorld");
                object[] renArgs = { "world_gamma", "Brand New World", null };
                Assert.That((bool)renameMethod.Invoke(null, renArgs), Is.True);

                var tryLoadMethod = _system.GetMethod("TryLoadWorld");
                object[] loadArgs = { "world_gamma", null };
                Assert.That((bool)tryLoadMethod.Invoke(null, loadArgs), Is.True);

                var reloaded = loadArgs[1];
                Assert.That((string)_dataType.GetField("saveName").GetValue(reloaded), Is.EqualTo("Brand New World"));
            }
            finally
            {
                propEditor.SetValue(null, null);
            }
        }
    }
}
