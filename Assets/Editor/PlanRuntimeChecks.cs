using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;
using static UnityEngine.Object;
using TheLastKnight.Core;
using TheLastKnight.Combat;
using TheLastKnight.Environment;
using TheLastKnight.Player;
using TheLastKnight.UI;

// Component-driven integration checks. These do not replace traversal/input playtesting.
public class PlanRuntimeChecks
{
    private sealed class Pause { public double Seconds; public Pause(double seconds) { Seconds = seconds; } }
    private static PlanRuntimeChecks _active;
    private readonly Stack<IEnumerator> _stack = new Stack<IEnumerator>();
    private double _resumeAt;
    private int _lastFrame = -1;
    public static string Status { get; private set; } = "Not started";
    private readonly List<string> _results = new List<string>();
    private readonly List<string> _errors = new List<string>();
    private string _realSave, _realBackup, _saveContents, _backupContents, _reportPath;
    private bool _ownsOverride;
    private GameManager Manager => GameManager.Instance;

    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        if (_active != null) return Status;
        _active = new PlanRuntimeChecks();
        // Synthetic gameplay input is routed to the focused Game View by the
        // project's existing Input System editor policy.
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        _active._stack.Push(_active.GuardedRun());
        Status = "Starting";
        EditorApplication.update += _active.Tick;
        return Status;
    }

    private void Tick()
    {
        if (!Application.isPlaying)
        {
            Status = "Aborted: exited Play Mode";
            if (_reportPath != null) Finish(); else Cleanup();
            return;
        }
        if (EditorApplication.timeSinceStartup < _resumeAt || Time.frameCount == _lastFrame) return;
        _lastFrame = Time.frameCount;
        for (int steps = 0; steps < 20 && _stack.Count > 0; steps++)
        {
            var routine = _stack.Peek();
            bool more;
            try { more = routine.MoveNext(); }
            catch (Exception exception) { Status = "FAILED: " + exception.Message; Finish(); return; }
            if (!more) { _stack.Pop(); continue; }
            if (routine.Current is IEnumerator nested) { _stack.Push(nested); continue; }
            if (routine.Current is Pause pause) _resumeAt = EditorApplication.timeSinceStartup + pause.Seconds;
            return;
        }
    }

    private IEnumerator GuardedRun()
    {
        Status = "Running";
        _realSave = SaveSystem.SavePath; _realBackup = _realSave + ".bak";
        _saveContents = File.Exists(_realSave) ? File.ReadAllText(_realSave) : null;
        _backupContents = File.Exists(_realBackup) ? File.ReadAllText(_realBackup) : null;
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Captures/RuntimeQA/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(directory);
        _reportPath = Path.Combine(directory, "report.txt");
        SaveSystem.EditorTestSavePath = Path.Combine(directory, "save.json");
        _ownsOverride = true;
        Application.logMessageReceived += OnLog;
        var suite = Checks();
        while (true)
        {
            bool more; object next = null;
            try { more = suite.MoveNext(); if (more) next = suite.Current; }
            catch (Exception exception)
            {
                Status = "FAILED: " + exception.Message;
                Finish(); yield break;
            }
            if (!more) break;
            yield return next;
        }
        Status = _errors.Count == 0 ? "PASSED " + _results.Count + " checks" : "FAILED: runtime console errors";
        Finish();
    }

    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _results.Add("PASS " + message);
        Status = "Running: " + message;
    }
    private void Click(string name)
    {
        var go = GameObject.Find(name);
        if (go == null) throw new InvalidOperationException("Missing UI button: " + name);
        var button = go.GetComponent<Button>();
        if (!button.interactable) throw new InvalidOperationException("Disabled UI button: " + name);
        button.onClick.Invoke();
    }
    private IEnumerator Settled() { yield return null; yield return null; yield return null; }

    private IEnumerator PressKey(Key key)
    {
        var original = Keyboard.current;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return Settled();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Settled();
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            if (original != null && original.added) original.MakeCurrent();
        }
    }

    private IEnumerator Checks()
    {
        Manager.NewGame(GameDifficulty.Easy);
        yield return Settled();
        Check(Manager.Player != null && Manager.InputBlocked && GameObject.Find("Skip") != null, "New game opens intro and blocks input");
        Click("Skip");
        Check(!Manager.InputBlocked && Manager.Player.CurrentHP == Manager.Player.MaxHP, "Skip restores player controls");
        yield return LocomotionChecks();
        Manager.Player.TakeDamage(30);
        yield return new Pause(0.5);
        yield return PressKey(Key.Q);
        yield return new Pause(2.7f);
        Check(Manager.Player.CurrentHP == Manager.Player.MaxHP && Manager.Player.HealingPotions == 2, "Completed drink heals and consumes exactly one potion");
        Manager.Player.TakeDamage(20);
        Manager.Player.SendMessage("StartDrink");
        Manager.Player.TakeDamage(5);
        float interruptedHP = Manager.Player.CurrentHP;
        yield return new Pause(2.7f);
        Check(Manager.Player.CurrentHP == interruptedHP && Manager.Player.HealingPotions == 2, "Damage interrupts drink without healing or consuming a potion");
        Manager.Player.TrySpendStamina(Manager.Player.CurrentStamina);
        foreach (string action in new[] { "StartDash", "StartSkill", "StartExcalibur" }) Manager.Player.SendMessage(action);
        Check(Manager.Player.GetComponent<PlayerController>().CurrentState == PlayerState.Idle, "Insufficient stamina rejects dash and both skills");
        var firstStatue = FindAnyObjectByType<MedusaSavePoint>();
        Manager.Player.transform.position = new Vector3(firstStatue.transform.position.x + 1, Manager.Player.transform.position.y, 0);
        Manager.Player.GetComponent<Rigidbody2D>().position = Manager.Player.transform.position;
        yield return Settled();
        yield return PressKey(Key.F);
        Check(Manager.State.runes[1] && SaveSystem.HasSave && Manager.Player.CurrentHP == Manager.Player.MaxHP && Manager.Player.CurrentStamina == 100, "Medusa heals, restores stamina, grants rune 2 and saves");
        yield return CombatChecks();

        Manager.Travel("Church"); yield return Settled();
        Check(Manager.State.runes[1] && Manager.Player.HealingPotions == 2, "Scene transition retains rune and potion state");
        FindAnyObjectByType<RuneChest>().Interact();
        Check(!Manager.State.runes[0], "Church chest rejects missing key");
        var churchArena = FindAnyObjectByType<BossArena>();
        Manager.Player.transform.position = new Vector3(churchArena.transform.position.x - 4, -3.4f, 0);
        Manager.Player.GetComponent<Rigidbody2D>().position = Manager.Player.transform.position;
        Physics2D.SyncTransforms();
        double arenaDeadline = EditorApplication.timeSinceStartup + 1;
        while (!Manager.ArenaLocked && EditorApplication.timeSinceStartup < arenaDeadline) yield return null;
        Check(Manager.ArenaLocked && churchArena.barriers.All(b => b.activeSelf), "Entering boss arena enables red barriers");
        Manager.Travel("CityCenter");
        Check(SceneManager.GetActiveScene().name == "Church", "Locked boss arena prevents scene escape");
        FindAnyObjectByType<EnemyStats>().TakeDamage(100000f);
        Check(!Manager.ArenaLocked && churchArena.barriers.All(b => !b.activeSelf), "Boss death releases arena barriers");
        Check(Manager.State.churchKey && Manager.Player.Gold == 100 && Manager.Player.Level > 1, "Church boss awards key, gold and EXP");
        FindAnyObjectByType<RuneChest>().Interact();
        Check(Manager.State.runes[0], "Church key unlocks rune 1");

        Manager.Travel("OutdoorMarket"); yield return Settled();
        var trader = FindAnyObjectByType<ShadowMarketNPC>(); trader.Interact();
        var shop = trader.GetComponent<ShopUI>();
        Check(Manager.InputBlocked && Time.timeScale == 0, "Shop modal freezes gameplay");
        Check(!shop.TryPurchase("RUNE") && Manager.Player.Gold == 100, "Insufficient gold cannot buy rune");
        Click("Close");
        yield return Settled();
        FindObjectsByType<EnemyStats>(FindObjectsSortMode.None).First(e => e.name.Contains("DemonBoss")).TakeDamage(100000f);
        trader.Interact();
        Click("Rune of the Trident — 150 Gold");
        Check(Manager.State.runes[3] && Manager.Player.Gold == 50, "Market rune purchase charges exactly 150 gold");
        Check(!shop.TryPurchase("RUNE") && Manager.Player.Gold == 50, "Duplicate rune purchase is rejected without a charge");
        Click("Medium Healing Potion — 50 Gold");
        Check(Manager.Player.HealingPotions == 3 && Manager.Player.Gold == 0, "Healing potion purchase charges 50 gold");
        int str = Manager.Player.STR, vit = Manager.Player.VIT, dex = Manager.Player.DEX, agi = Manager.Player.AGI;
        Manager.Player.AddGold(400); // Isolated fixture funding for catalog coverage.
        foreach (string stat in new[] { "STR", "VIT", "DEX", "AGI" }) Check(shop.TryPurchase(stat), stat + " potion is purchasable");
        Check(Manager.Player.Gold == 0 && Manager.Player.STR == str + 1 && Manager.Player.VIT == vit + 1 && Manager.Player.DEX == dex + 1 && Manager.Player.AGI == agi + 1, "Stat potions apply once and each charge 100 gold");
        Click("Close");
        Check(!Manager.InputBlocked && Time.timeScale == 1, "Shop close restores gameplay");

        Manager.Travel("SuburbToForest"); yield return Settled();
        var fox = FindObjectsByType<EnemyProgressionReward>(FindObjectsSortMode.None).First(e => e.runeIndex == 2);
        fox.GetComponent<EnemyStats>().TakeDamage(100000f);
        Check(Manager.State.runes.All(r => r), "Fox death completes all four acquired runes");
        FindAnyObjectByType<MedusaSavePoint>().Interact();
        var checkpoint = Manager.State.Copy();
        Manager.Player.TakeDamage(100000f);
        Check(Manager.InputBlocked && GameObject.Find("YOU DIED") != null, "Player death blocks controls and opens death UI");
        Click("Respawn"); yield return Settled();
        Check(SceneManager.GetActiveScene().name == "SuburbToForest" && Manager.Player.CurrentHP == checkpoint.hp && Manager.Player.Gold == checkpoint.gold && Manager.Player.HealingPotions == checkpoint.potions && Manager.State.runes.All(r => r), "Respawn restores checkpoint health, inventory and runes");
        Check(Vector2.Distance(Manager.Player.transform.position, checkpoint.position) < 0.5f, "Respawn restores checkpoint position");
        Manager.Load("MainMenu", false); yield return Settled();
        Click("Continue"); yield return Settled();
        Check(SceneManager.GetActiveScene().name == "SuburbToForest" && Manager.State.runes.All(r => r) && Manager.Player.Gold == checkpoint.gold, "Main Menu Continue loads saved progression");

        Manager.Travel("DemonCastleEntrance"); yield return Settled();
        var gate = FindAnyObjectByType<DemonCastleGateVN>();
        Check(Manager.InputBlocked && !Manager.Player.GetComponent<PlayerController>().enabled, "VN gate disables player controls");
        Check(gate.InsertRune(0) && !gate.InsertRune(0), "Gate accepts owned rune once");
        for (int i = 1; i < 4; i++) Check(gate.InsertRune(i), "Gate accepts owned rune " + (i + 1));
        // The gate animation uses game time, which may advance slower than editor
        // wall time during a busy frame. Wait for the observable transition.
        double gateDeadline = EditorApplication.timeSinceStartup + 15;
        while (SceneManager.GetActiveScene().name != "DemonCastle" && EditorApplication.timeSinceStartup < gateDeadline)
            yield return null;
        yield return Settled();
        Check(SceneManager.GetActiveScene().name == "DemonCastle", "Four socketed runes open the castle");
        var finalBoss = FindObjectsByType<EnemyProgressionReward>(FindObjectsSortMode.None).First(e => e.finalBoss);
        finalBoss.GetComponent<EnemyStats>().TakeDamage(100000f);
        Check(Manager.State.victory && GameObject.Find("A KINGDOM REBORN") != null && Manager.InputBlocked, "Final boss death opens victory story");
        Click("Skip"); yield return Settled();
        Check(SceneManager.GetActiveScene().name == "MainMenu", "Ending returns to Main Menu");
        yield return AudioChecks();
    }

    private IEnumerator LocomotionChecks()
    {
        var original = Keyboard.current;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            keyboard.MakeCurrent();
            var player = Manager.Player;
            var start = player.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.LeftShift));
            yield return new Pause(0.9);
            Check(player.transform.position.x > start.x + 2, "D movement advances player across CityCenter floor");
            Check(player.GetComponent<PlayerController>().CurrentState == PlayerState.Running, "Held sprint reaches Running after dash");
            float floor = player.transform.position.y;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.LeftShift, Key.Space));
            yield return new Pause(0.18);
            var animator = player.GetComponent<Animator>();
            Check(player.transform.position.y > floor + 0.2f, "Space physically lifts player while sprinting");
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Jump") || animator.GetNextAnimatorStateInfo(0).IsName("Jump"), "Sprint jump enters takeoff animation");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new Pause(1.6);
            Check(Mathf.Abs(player.transform.position.y - floor) < 0.2f, "Jump lands back on CityCenter floor");
            player.Rest();
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            if (original != null && original.added) original.MakeCurrent();
        }
    }

    private IEnumerator CombatChecks()
    {
        var player = Manager.Player;
        float[] enemyMultipliers = { 1f, 1.3f, 1.6f };
        float[] playerMultipliers = { 1f, 0.7f, 0.4f };
        for (int mode = 0; mode < 3; mode++)
        {
            GameDifficultyManager.Current = (GameDifficulty)mode;
            player.Rest(); player.TakeDamage(10);
            Check(Mathf.Abs(player.MaxHP - player.CurrentHP - 10 * enemyMultipliers[mode]) < 0.01f, "Incoming damage multiplier for " + (GameDifficulty)mode);
            yield return new Pause(0.5);
            var enemy = new GameObject("QA damage target", typeof(EnemyStats), typeof(BoxCollider2D));
            try
            {
                enemy.transform.position = player.transform.position + Vector3.right;
                Physics2D.SyncTransforms();
                var originalRandom = UnityEngine.Random.state;
                UnityEngine.Random.InitState(823);
                bool crit = UnityEngine.Random.value * 100f < player.CriticalChance;
                UnityEngine.Random.InitState(823);
                player.SendMessage("StartAttack"); player.SendMessage("ApplyAttackHits");
                UnityEngine.Random.state = originalRandom;
                Check(Mathf.Abs(50 - enemy.GetComponent<EnemyStats>().CurrentHealth - player.AttackPower * playerMultipliers[mode] * (crit ? 2 : 1)) < 0.01f, "Outgoing attack damage multiplier for " + (GameDifficulty)mode);
            }
            finally { Destroy(enemy); }
            yield return new Pause(0.4);
            var canvas = FindAnyObjectByType<FloatingHealthBar>().GetComponent<Canvas>();
            Check(canvas.enabled == (mode != 2), "Enemy health helper visibility for " + (GameDifficulty)mode);
            var level = canvas.GetComponentsInChildren<Text>().First(t => t.name == "Enemy Level");
            Check(level.enabled == (mode == 0), "Enemy level helper visibility for " + (GameDifficulty)mode);
        }
        GameDifficultyManager.Current = GameDifficulty.Easy;
        player.Rest();
        var statue = FindAnyObjectByType<MedusaSavePoint>();
        float groundPlayerY = player.transform.position.y;
        for (int sample = 0; sample < 3; sample++)
        {
            bool near = sample != 0;
            GameDifficultyManager.Current = sample == 2 ? GameDifficulty.Hard : GameDifficulty.Easy;
            player.transform.position = new Vector3(statue.transform.position.x + (near ? 1 : 7), groundPlayerY, 0);
            player.GetComponent<Rigidbody2D>().position = player.transform.position;
            player.GetComponent<PlayerController>().ResetVelocity();
            Physics2D.SyncTransforms();
            yield return new Pause(0.15);
            player.Rest(); player.TrySpendStamina(80);
            float stamina = player.CurrentStamina, start = Time.time;
            yield return new Pause(0.25);
            float rate = (player.CurrentStamina - stamina) / (Time.time - start);
            Check(Mathf.Abs(rate - (sample == 1 ? 40 : 20)) < 3, "Measured stamina regeneration " + sample + " at " + rate.ToString("F1") + "/s");
        }
        GameDifficultyManager.Current = GameDifficulty.Easy;
        player.Rest();
        var slimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/BlueSlime.prefab");
        var slime = Instantiate(slimePrefab, player.transform.position + Vector3.right * 1.2f, Quaternion.identity);
        try
        {
            var parry = slime.GetComponent<ParryReceiver>();
            float timeout = Time.time + 5;
            while ((!parry.IsWindingUp || parry.Progress < 0.75f) && Time.time < timeout) yield return null;
            Check(parry.IsWindingUp, "Enemy AI begins a real attack windup");
            player.SendMessage("StartAttack");
            Check(parry.IsStaggered && !slime.GetComponent<TheLastKnight.AI.EnemyController>().CanDealMeleeDamage && player.GetComponent<PlayerController>().IsInvincible, "Timed player attack cancels enemy strike and grants parry invulnerability");
            float health = slime.GetComponent<EnemyStats>().CurrentHealth;
            player.SendMessage("ApplyAttackHits");
            Check(Mathf.Abs(health - slime.GetComponent<EnemyStats>().CurrentHealth - player.AttackPower * 2) < 0.01f, "Staggered enemy takes guaranteed critical damage");
            yield return new Pause(1.55);
            Check(!parry.IsStaggered, "Parry stagger expires after 1.5 seconds");
        }
        finally { Destroy(slime); }
        player.Rest();
    }

    private IEnumerator AudioChecks()
    {
        var audio = TheLastKnight.Audio.AudioManager.Instance;
        var field = typeof(TheLastKnight.Audio.AudioManager).GetField("_catalog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var originalCatalog = field.GetValue(audio);
        float master = audio.Master, music = audio.Music, effects = audio.Effects;
        var catalog = ScriptableObject.CreateInstance<TheLastKnight.Audio.AudioCatalog>();
        var first = AudioClip.Create("QA first", 88200, 1, 44100, false);
        var second = AudioClip.Create("QA second", 88200, 1, 44100, false);
        catalog.clips = new[] {
            new TheLastKnight.Audio.AudioCatalog.Entry { id = "QA1", clip = first },
            new TheLastKnight.Audio.AudioCatalog.Entry { id = "QA2", clip = second },
            new TheLastKnight.Audio.AudioCatalog.Entry { id = "QA-SFX", clip = first } };
        try
        {
            field.SetValue(audio, catalog);
            audio.SetVolumes(0.5f, 0.6f, 0.4f);
            audio.PlayMusic("QA1"); yield return new Pause(1.15);
            var sources = audio.GetComponents<AudioSource>();
            Check(sources.Any(s => s.clip == first && s.isPlaying && Mathf.Abs(s.volume - 0.3f) < 0.01f), "Music channel starts assigned clip with master gain");
            audio.PlaySfx("QA-SFX");
            Check(sources.Any(s => !s.loop && s.isPlaying && Mathf.Abs(s.volume - 0.2f) < 0.01f), "SFX channel plays assigned clip with independent gain");
            audio.PlayMusic("QA2"); yield return new Pause(0.25);
            Check(sources.Count(s => s.loop && s.isPlaying && s.volume > 0 && s.volume < 0.3f) == 2, "BGM transition crossfades both active channels");
            yield return new Pause(1);
            Check(sources.Any(s => s.clip == second && s.isPlaying && Mathf.Abs(s.volume - 0.3f) < 0.01f) && !sources.Any(s => s.clip == first && s.loop && s.isPlaying), "BGM crossfade completes and stops previous track");
            Check(FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 1, "Main Menu has exactly one audio listener");
        }
        finally
        {
            field.SetValue(audio, originalCatalog);
            audio.SetVolumes(master, music, effects);
            audio.PlaySceneMusic(SceneManager.GetActiveScene().name);
            Destroy(catalog); Destroy(first); Destroy(second);
        }
    }

    private void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors.Add(message + "\n" + stack);
    }
    private void Finish()
    {
        Application.logMessageReceived -= OnLog;
        bool unchanged = (File.Exists(_realSave) ? File.ReadAllText(_realSave) : null) == _saveContents
            && (File.Exists(_realBackup) ? File.ReadAllText(_realBackup) : null) == _backupContents;
        _results.Add((unchanged ? "PASS" : "FAIL") + " Existing user save and backup unchanged");
        if (!unchanged) Status = "FAILED: user save changed";
        File.WriteAllText(_reportPath, Status + "\nComponent-driven integration only; physical traversal not covered.\n" + string.Join("\n", _results) + "\n" + string.Join("\n", _errors));
        Status += " | " + _reportPath;
        Cleanup();
    }
    private void Cleanup()
    {
        EditorApplication.update -= Tick;
        _active = null;
        Application.logMessageReceived -= OnLog;
        if (_ownsOverride) SaveSystem.EditorTestSavePath = null;
        Time.timeScale = 1;
    }
}
