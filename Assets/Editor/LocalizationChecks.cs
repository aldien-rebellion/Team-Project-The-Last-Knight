using System;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheLastKnight.Core;
using TheLastKnight.Inventory;
using TheLastKnight.UI;

public static class LocalizationChecks
{
    [MenuItem("The Last Knight/Localization/Toggle Language")]
    private static void ToggleLanguage() => LocalizationManager.ToggleLanguage();
    [Serializable] private sealed class Catalog { public Entry[] entries; }
    [Serializable] private sealed class Entry { public string en, th, source; }
    private static GameLanguage _previewOriginal;
    private static bool _previewing;
    private static GameObject _preview;

    [MenuItem("The Last Knight/Localization/Verify")]
    public static void Verify()
    {
        var original = LocalizationManager.Current;
        GameObject holder = null;
        int checks = 0;
        try
        {
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("Localization/GameTexts").text);
            foreach (var language in new[] { GameLanguage.Thai, GameLanguage.English })
            {
                LocalizationManager.Current = language;
                foreach (string stat in new[] { "STR", "AGI", "VIT", "DEX" })
                {
                    Equal(LocalizationManager.Translate(stat), stat);
                    checks++;
                }
                foreach (var entry in catalog.entries)
                {
                    if (Regex.IsMatch(entry.en, @"\{\d+\}")) continue;
                    string expected = language == GameLanguage.English ? entry.en : entry.th;
                    Equal(LocalizationManager.Translate(entry.source ?? entry.en), expected);
                    checks++;
                }
                foreach (var definition in Resources.LoadAll<ItemDefinition>("Items/Definitions"))
                {
                    string text = LocalizationManager.Translate(definition.displayName) + LocalizationManager.Translate(definition.description);
                    if (language == GameLanguage.English && Regex.IsMatch(text, @"[ก-๙]"))
                        throw new Exception("Mixed English item: " + definition.name);
                    if (language == GameLanguage.Thai && Regex.IsMatch(text, @"[A-Za-z]{3}"))
                        throw new Exception("Untranslated Thai item: " + definition.name);
                    checks++;
                }
            }
            LocalizationManager.Current = GameLanguage.Thai;
            Equal(LocalizationManager.Translate("Sold 3x Healing Potion for 150 Gold!"), "ขาย ยาฟื้นฟูพลังชีวิต 3 ชิ้น ได้รับ 150 ทอง!");
            Equal(LocalizationManager.Translate("<color=#FFD56B>Current Stack: 3 / 64</color>"), "<color=#FFD56B>จำนวนในกอง: 3 / 64</color>");
            Equal(LocalizationManager.Translate("ATK: 25\nIncreases physical attack power."), "พลังโจมตี: 25\nเพิ่มพลังโจมตีกายภาพ");
            Equal(LocalizationManager.Translate("Master Volume  75%"), "ระดับเสียงหลัก  75%");
            Equal(LocalizationManager.Translate("DemonBoss   Lv. 50"), "ราชาปีศาจ   Lv. 50");
            holder = new GameObject("Localization binding check", typeof(RectTransform), typeof(TextMeshProUGUI));
            var textLabel = holder.GetComponent<TextMeshProUGUI>();
            LocalizedText.Set(textLabel, "Healing Potion");
            Equal(textLabel.text, "ยาฟื้นฟูพลังชีวิต");
            LocalizationManager.Current = GameLanguage.English;
            Equal(textLabel.text, "Healing Potion");
            holder.SetActive(false);
            LocalizationManager.Current = GameLanguage.Thai;
            holder.SetActive(true);
            Equal(textLabel.text, "ยาฟื้นฟูพลังชีวิต");
            if (!textLabel.font.HasCharacter('ก', true, true)) throw new Exception("Missing Thai glyph.");
            var prompt = new GameObject("Inactive door prompt", typeof(RectTransform), typeof(Text));
            prompt.transform.SetParent(holder.transform, false);
            var promptText = prompt.GetComponent<Text>();
            promptText.text = "กด F เพื่อไปต่อ";
            prompt.SetActive(false);
            LocalizedText.BindPrompt(prompt);
            LocalizationManager.Current = GameLanguage.English;
            prompt.SetActive(true);
            Equal(promptText.text, "Press F to continue");
            LocalizedText.BindPrompt(prompt);
            LocalizationManager.Current = GameLanguage.Thai;
            Equal(promptText.text, "กด F เพื่อไปต่อ");
            Equal(LocalizationManager.Translate("[ F ] เพื่อเข้าประตู"), "[ F ] เพื่อเข้าประตู");
            LocalizationManager.Current = GameLanguage.English;
            Equal(LocalizationManager.Translate("[ F ] เพื่อเข้าประตู"), "[ F ] Enter door");
            Debug.Log("[LocalizationChecks] PASS: " + (checks + 13) + " catalog, item, format, font and live-switch checks.");
        }
        finally
        {
            if (holder != null) UnityEngine.Object.DestroyImmediate(holder);
            LocalizationManager.Current = original;
        }
    }

    private static void Equal(string actual, string expected)
    {
        if (actual != expected) throw new Exception("Expected '" + expected + "', got '" + actual + "'.");
    }

    [MenuItem("The Last Knight/Localization/Preview Thai")]
    private static void PreviewThai() => Preview(GameLanguage.Thai);
    [MenuItem("The Last Knight/Localization/Preview English")]
    private static void PreviewEnglish() => Preview(GameLanguage.English);

    [MenuItem("The Last Knight/Localization/Preview Large Status Values")]
    private static void PreviewLargeStatusValues()
    {
        Preview(GameLanguage.Thai);
        var status = _preview.GetComponent<CharacterStatusUI>();
        string[] fields = { "_txtStrValue", "_txtAgiValue", "_txtVitValue", "_txtDexValue", "_txtStatusPoints" };
        string[] values = { "1010", "1110", "1040", "200", "10000" };
        for (int i = 0; i < fields.Length; i++)
        {
            var text = (TMP_Text)typeof(CharacterStatusUI).GetField(fields[i], BindingFlags.Instance | BindingFlags.NonPublic).GetValue(status);
            LocalizedText.Set(text, values[i]);
        }
        Canvas.ForceUpdateCanvases();
    }

    private static void Preview(GameLanguage language)
    {
        if (!_previewing) { _previewOriginal = LocalizationManager.Current; _previewing = true; }
        CleanupPreview(false);
        LocalizationManager.Current = language;
        _preview = new GameObject("Localization Preview");
        var status = _preview.AddComponent<CharacterStatusUI>();
        typeof(CharacterStatusUI).GetMethod("BuildUI", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(status, null);
        status.CanvasObject.transform.SetParent(_preview.transform, true);
        status.CanvasObject.SetActive(true);
        status.Canvas.renderMode = RenderMode.ScreenSpaceCamera;
        status.Canvas.worldCamera = Camera.main;
        status.Canvas.planeDistance = 1f;
        status.ShowTooltip("Healing Potion", "Potion", ItemRegistry.CreateItem("potion_heal").description);
        UnityEditor.EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Repaint();
        Canvas.ForceUpdateCanvases();
    }

    [MenuItem("The Last Knight/Localization/Close Preview")]
    private static void ClosePreview() => CleanupPreview(true);

    [MenuItem("The Last Knight/Localization/Preview Save Cards")]
    private static void PreviewSaveCards()
    {
        CleanupPreview(true);
        _preview = new GameObject("Localization Preview", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = _preview.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = Camera.main;
        canvas.planeDistance = 1f;
        canvas.sortingOrder = 1000;
        var scaler = _preview.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 800);
        var column = new GameObject("Save Cards", typeof(RectTransform), typeof(VerticalLayoutGroup));
        column.transform.SetParent(_preview.transform, false);
        var rect = column.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(750, 180);
        column.GetComponent<VerticalLayoutGroup>().spacing = 8;
        foreach (var name in new[] { "โลกของอาเธอร์", "Arthur's World" })
            RuntimeUI.WorldCard(column.transform, new PlayerSaveData { worldId = name, saveName = name, level = 23, lastSavedDate = "2026-10-06 22:30" }, null, null, null);
        Canvas.ForceUpdateCanvases();
        foreach (var text in column.GetComponentsInChildren<Text>())
            if (text.transform.parent.name == "Info")
                Debug.Log($"[SaveCardCheck] {text.text}: rect={text.rectTransform.rect}, vertices={text.cachedTextGenerator.vertexCount}");
    }

    private static void CleanupPreview(bool restore)
    {
        if (_preview != null) UnityEngine.Object.DestroyImmediate(_preview);
        if (restore && _previewing) { LocalizationManager.Current = _previewOriginal; _previewing = false; }
    }
}
