using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine.TextCore;
using TheLastKnight.Core;

namespace TheLastKnight.UI
{
    // Retain the original text so repeated language switches never translate a translation.
    [ExecuteAlways]
    public sealed class LocalizedText : MonoBehaviour
    {
        private Component _target;
        private string _source;
        private string _displayed;
        private GameLanguage _language;
        private static TMP_FontAsset _font;
        private static Font _legacyFont;

        public static Font Font => _legacyFont != null ? _legacyFont :
            (_legacyFont = Resources.Load<Font>("Localization/Sarabun-Regular"));

        private static TMP_FontAsset FontAsset
        {
            get
            {
                if (_font != null) return _font;
                _font = TMP_FontAsset.CreateFontAsset(Font);
                _font.getFontFeatures = true;
                _font.name = "Game Thai and English";
                _font.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (TMP_Settings.defaultFontAsset != null)
                    _font.fallbackFontAssetTable.Add(TMP_Settings.defaultFontAsset);
                return _font;
            }
        }

        public static void Set(Component target, string source)
        {
            if (target == null) return;
            var binding = target.GetComponent<LocalizedText>();
            if (binding == null) binding = target.gameObject.AddComponent<LocalizedText>();
            if (binding._target == target && binding._source == source &&
                binding._language == LocalizationManager.Current && Read(target) == binding._displayed) return;
            binding._target = target;
            binding._source = source;
            binding.Refresh(LocalizationManager.Current);
        }

        public static void BindPrompt(GameObject root)
        {
            if (root == null) return;
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                if (text.GetComponent<LocalizedText>() == null) Set(text, text.text);
                text.verticalOverflow = VerticalWrapMode.Overflow;
            }
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                if (text.GetComponent<LocalizedText>() == null) Set(text, text.text);
            foreach (var text in root.GetComponentsInChildren<TextMesh>(true))
                if (text.GetComponent<LocalizedText>() == null) Set(text, text.text);
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += Refresh;
            TheLastKnight.Input.KeyRebindManager.OnBindingsChanged += RefreshBindings;
            Refresh(LocalizationManager.Current);
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= Refresh;
            TheLastKnight.Input.KeyRebindManager.OnBindingsChanged -= RefreshBindings;
        }

        private void RefreshBindings() => Refresh(LocalizationManager.Current);

        private void Refresh(GameLanguage language)
        {
            if (_target == null) return;
            _language = language;
            _displayed = TheLastKnight.Input.KeyRebindManager.UpdateBindingHints(LocalizationManager.Translate(_source));
            if (_target is TMP_Text tmp)
            {
                tmp.font = FontAsset;
                // Position Thai vowels on consonants, and tone marks on vowels.
                if (!tmp.fontFeatures.Contains(OTL_FeatureTag.mark)) tmp.fontFeatures.Add(OTL_FeatureTag.mark);
                if (!tmp.fontFeatures.Contains(OTL_FeatureTag.mkmk)) tmp.fontFeatures.Add(OTL_FeatureTag.mkmk);
                tmp.text = _displayed;
            }
            else if (_target is Text text)
            {
                text.font = Font;
                if (text.GetComponent<ThaiTextMarks>() == null) text.gameObject.AddComponent<ThaiTextMarks>();
                text.text = _displayed;
            }
            else if (_target is TextMesh mesh)
            {
                mesh.font = Font;
                mesh.GetComponent<MeshRenderer>().sharedMaterial = Font.material;
                mesh.text = _displayed;
            }
        }

        private static string Read(Component target)
        {
            if (target is TMP_Text tmp) return tmp.text;
            if (target is Text text) return text.text;
            if (target is TextMesh mesh) return mesh.text;
            return null;
        }
    }
}
