using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using TheLastKnight.Player;
using TheLastKnight.Input;

namespace TheLastKnight.UI
{
    /// <summary>
    /// In-game HUD overlay displayed at the bottom-left corner of the screen.
    /// Shows the 3 player skill icons (Carnage Burst, Berserk Buff, Excalibur)
    /// and their cooldown status with a radial 360 clock-style shadow sweep.
    /// </summary>
    [DefaultExecutionOrder(-400)]
    public class SkillCooldownHUD : MonoBehaviour
    {
        public static SkillCooldownHUD Instance { get; private set; }

        [Header("State")]
        private bool _isVisible = true;
        public bool IsVisible => _isVisible;

        // UI Canvas Components
        private GameObject _canvasObject;
        public GameObject CanvasObject => _canvasObject;
        private Canvas _canvas;
        public Canvas Canvas => _canvas;
        private CanvasScaler _scaler;
        private GraphicRaycaster _raycaster;
        private RectTransform _rootPanelRect;

        // Skill Slots
        private readonly List<SkillSlotHUD> _slots = new List<SkillSlotHUD>();
        public IReadOnlyList<SkillSlotHUD> Slots => _slots;

        private PlayerController _cachedPlayer;
        private Sprite _whiteFillSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Application.isPlaying && Instance == null)
            {
                var go = new GameObject("SkillCooldownHUD_Manager");
                go.AddComponent<SkillCooldownHUD>();
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (Application.isPlaying && transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            CreateWhiteFillSprite();
            BuildUI();
            UpdateVisibilityForCurrentScene();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UpdateVisibilityForCurrentScene();
            _cachedPlayer = null; // Re-acquire player on map transition
        }

        private void UpdateVisibilityForCurrentScene()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            bool isMainMenu = string.Equals(sceneName, "MainMenu", StringComparison.OrdinalIgnoreCase);
            SetVisible(!isMainMenu);
        }

        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_canvasObject != null)
            {
                _canvasObject.SetActive(visible);
            }
        }

        public void BindPlayer(PlayerController player)
        {
            _cachedPlayer = player;
        }

        public SkillSlotHUD GetSlot(int index)
        {
            return (index >= 0 && index < _slots.Count) ? _slots[index] : null;
        }

        private void CreateWhiteFillSprite()
        {
            if (_whiteFillSprite == null)
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                Color[] pixels = new Color[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                tex.SetPixels(pixels);
                tex.Apply();
                _whiteFillSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            }
        }

        private void BuildUI()
        {
            _canvasObject = new GameObject("SkillCooldown_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasObject.transform.SetParent(transform, false);

            _canvas = _canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 20;

            _scaler = _canvasObject.GetComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1920f, 1080f);
            _scaler.matchWidthOrHeight = 0.5f;

            _raycaster = _canvasObject.GetComponent<GraphicRaycaster>();

            // Root panel anchored at bottom-left corner
            var panelGo = new GameObject("SkillCooldown_Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(_canvasObject.transform, false);

            _rootPanelRect = panelGo.GetComponent<RectTransform>();
            _rootPanelRect.anchorMin = new Vector2(0f, 0f);
            _rootPanelRect.anchorMax = new Vector2(0f, 0f);
            _rootPanelRect.pivot = new Vector2(0f, 0f);
            _rootPanelRect.anchoredPosition = new Vector2(24f, 24f);
            _rootPanelRect.sizeDelta = new Vector2(226f, 76f);

            var panelBg = panelGo.GetComponent<Image>();
            panelBg.color = new Color(0.05f, 0.06f, 0.09f, 0.85f);
            panelBg.raycastTarget = false;

            // Skill definitions: (Name, HotkeyAction, DefaultKey, SpritePath)
            var skillDefs = new (string name, string action, string defaultKey, string spritePath)[]
            {
                ("Carnage Burst", "UseSkill", "E", "CharacterStatus/Skill_CarnageBurst"),
                ("Berserk Buff", "UseBuff", "R", "CharacterStatus/Skill_BerserkBuff"),
                ("Excalibur", "UseExcalibur", "T", "CharacterStatus/Skill_Excalibur")
            };

            float[] slotXs = { 43f, 113f, 183f };

            _slots.Clear();
            for (int i = 0; i < 3; i++)
            {
                var def = skillDefs[i];
                var slot = CreateSkillSlot(panelGo.transform, i, slotXs[i], def.name, def.action, def.defaultKey, def.spritePath);
                _slots.Add(slot);
            }
        }

        private static void CreateBevelStrip(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var stripGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            stripGo.transform.SetParent(parent, false);
            var rt = stripGo.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var img = stripGo.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private SkillSlotHUD CreateSkillSlot(Transform parent, int index, float posX, string skillName,
            string actionName, string defaultKey, string spritePath)
        {
            var slotGo = new GameObject($"SkillSlot_{index + 1}_{skillName.Replace(" ", "")}", typeof(RectTransform));
            slotGo.transform.SetParent(parent, false);

            var slotRt = slotGo.GetComponent<RectTransform>();
            slotRt.anchorMin = new Vector2(0f, 0.5f);
            slotRt.anchorMax = new Vector2(0f, 0.5f);
            slotRt.pivot = new Vector2(0.5f, 0.5f);
            slotRt.anchoredPosition = new Vector2(posX, 0f);
            slotRt.sizeDelta = new Vector2(56f, 56f);

            // 1. Slot Frame Outer Border (Crisp separate frame for each individual skill slot)
            var frameGo = new GameObject("Slot_Frame", typeof(RectTransform), typeof(Image));
            frameGo.transform.SetParent(slotGo.transform, false);
            var frameRt = frameGo.GetComponent<RectTransform>();
            frameRt.anchorMin = Vector2.zero; frameRt.anchorMax = Vector2.one;
            frameRt.offsetMin = frameRt.offsetMax = Vector2.zero;

            var frameImg = frameGo.GetComponent<Image>();
            frameImg.color = new Color(0.18f, 0.22f, 0.28f, 1f); // Metallic slate-steel base
            frameImg.raycastTarget = false;

            var frameOutline = frameGo.AddComponent<Outline>();
            frameOutline.effectColor = new Color(0.42f, 0.48f, 0.58f, 0.95f); // Distinct steel outline separating slots
            frameOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Bevel highlights on slot frame
            CreateBevelStrip(frameGo.transform, "TopEdge", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -2f), new Vector2(0f, 0f), new Color(0.60f, 0.68f, 0.80f, 0.9f));
            CreateBevelStrip(frameGo.transform, "BottomEdge", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 2f), new Color(0.08f, 0.10f, 0.14f, 0.9f));
            CreateBevelStrip(frameGo.transform, "LeftEdge", new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(2f, 0f), new Color(0.45f, 0.52f, 0.62f, 0.9f));
            CreateBevelStrip(frameGo.transform, "RightEdge", new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-2f, 0f), new Vector2(0f, 0f), new Color(0.12f, 0.15f, 0.20f, 0.9f));

            // 2. Inner Recessed BG
            var bgGo = new GameObject("Slot_InnerBG", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(slotGo.transform, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0.5f, 0.5f);
            bgRt.anchorMax = new Vector2(0.5f, 0.5f);
            bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.sizeDelta = new Vector2(48f, 48f);
            bgRt.anchoredPosition = Vector2.zero;
            var bgImg = bgGo.GetComponent<Image>();
            bgImg.color = new Color(0.04f, 0.05f, 0.07f, 1f);
            bgImg.raycastTarget = false;

            // 3. Skill Icon (Correct, high-contrast, fully visible skill sprite)
            var iconGo = new GameObject("Skill_Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(slotGo.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(48f, 48f);
            iconRt.anchoredPosition = Vector2.zero;

            var iconImg = iconGo.GetComponent<Image>();
            var skillSprite = Resources.Load<Sprite>(spritePath);
            iconImg.sprite = skillSprite;
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // 4. Radial Shadow Overlay (Rotates like a clock, deep black shadow)
            var shadowGo = new GameObject("Cooldown_ShadowOverlay", typeof(RectTransform), typeof(Image));
            shadowGo.transform.SetParent(slotGo.transform, false);
            var shadowRt = shadowGo.GetComponent<RectTransform>();
            shadowRt.anchorMin = new Vector2(0.5f, 0.5f);
            shadowRt.anchorMax = new Vector2(0.5f, 0.5f);
            shadowRt.pivot = new Vector2(0.5f, 0.5f);
            shadowRt.sizeDelta = new Vector2(48f, 48f);
            shadowRt.anchoredPosition = Vector2.zero;

            var shadowImg = shadowGo.GetComponent<Image>();
            shadowImg.sprite = _whiteFillSprite;
            shadowImg.type = Image.Type.Filled;
            shadowImg.fillMethod = Image.FillMethod.Radial360;
            shadowImg.fillOrigin = (int)Image.Origin360.Top;
            shadowImg.fillClockwise = false; // Clockwise reveal/sweep as fillAmount goes 1 -> 0
            shadowImg.fillAmount = 0f;
            shadowImg.color = new Color(0f, 0f, 0f, 0.90f); // Deep, intense black cooldown shadow
            shadowImg.raycastTarget = false;
            shadowGo.SetActive(false);

            // 5. Ready Flash Highlight
            var flashGo = new GameObject("Ready_Flash", typeof(RectTransform), typeof(Image));
            flashGo.transform.SetParent(slotGo.transform, false);
            var flashRt = flashGo.GetComponent<RectTransform>();
            flashRt.anchorMin = new Vector2(0.5f, 0.5f);
            flashRt.anchorMax = new Vector2(0.5f, 0.5f);
            flashRt.pivot = new Vector2(0.5f, 0.5f);
            flashRt.sizeDelta = new Vector2(48f, 48f);
            flashRt.anchoredPosition = Vector2.zero;
            var flashImg = flashGo.GetComponent<Image>();
            flashImg.sprite = _whiteFillSprite;
            flashImg.color = new Color(1f, 1f, 1f, 0f);
            flashImg.raycastTarget = false;

            // 6. Cooldown Text in center
            var txtGo = new GameObject("Txt_Cooldown", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(slotGo.transform, false);
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;

            var cdText = txtGo.GetComponent<TextMeshProUGUI>();
            TheLastKnight.UI.LocalizedText.Set(cdText, "");
            cdText.fontSize = 18f;
            cdText.alignment = TextAlignmentOptions.Center;
            cdText.color = Color.white;
            cdText.fontStyle = FontStyles.Bold;
            cdText.raycastTarget = false;

            // 7. Hotkey Badge at top-left
            var badgeGo = new GameObject("Key_Badge", typeof(RectTransform), typeof(Image));
            badgeGo.transform.SetParent(slotGo.transform, false);
            var badgeRt = badgeGo.GetComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(0f, 1f);
            badgeRt.anchorMax = new Vector2(0f, 1f);
            badgeRt.pivot = new Vector2(0f, 1f);
            badgeRt.anchoredPosition = new Vector2(-4f, 4f);
            badgeRt.sizeDelta = new Vector2(20f, 18f);

            var badgeBg = badgeGo.GetComponent<Image>();
            badgeBg.color = new Color(0.08f, 0.1f, 0.15f, 0.95f);
            badgeBg.raycastTarget = false;

            var badgeOutline = badgeGo.AddComponent<Outline>();
            badgeOutline.effectColor = new Color(0.35f, 0.4f, 0.5f, 0.85f); // Neutral steel trim
            badgeOutline.effectDistance = new Vector2(1f, 1f);

            var badgeTxtGo = new GameObject("Txt_Key", typeof(RectTransform), typeof(TextMeshProUGUI));
            badgeTxtGo.transform.SetParent(badgeGo.transform, false);
            var badgeTxtRt = badgeTxtGo.GetComponent<RectTransform>();
            badgeTxtRt.anchorMin = Vector2.zero; badgeTxtRt.anchorMax = Vector2.one;
            badgeTxtRt.offsetMin = badgeTxtRt.offsetMax = Vector2.zero;

            var badgeText = badgeTxtGo.GetComponent<TextMeshProUGUI>();
            TheLastKnight.UI.LocalizedText.Set(badgeText, defaultKey);
            badgeText.fontSize = 11f;
            badgeText.alignment = TextAlignmentOptions.Center;
            badgeText.color = new Color(1f, 0.9f, 0.45f);
            badgeText.fontStyle = FontStyles.Bold;
            badgeText.raycastTarget = false;

            var lockOverlay = CreateLockOverlay(slotGo.transform, index == 0 ? PlayerController.Skill1UnlockLevel
                : index == 1 ? PlayerController.Skill2UnlockLevel : PlayerController.Skill3UnlockLevel);
            lockOverlay.transform.SetSiblingIndex(badgeGo.transform.GetSiblingIndex());
            lockOverlay.SetActive(false);

            return new SkillSlotHUD
            {
                SlotRoot = slotGo,
                IconImage = iconImg,
                ShadowOverlay = shadowImg,
                CooldownText = cdText,
                KeyBadgeText = badgeText,
                FlashImage = flashImg,
                LockOverlay = lockOverlay,
                SkillName = skillName,
                ActionName = actionName,
                DefaultKey = defaultKey
            };
        }

        internal static GameObject CreateLockOverlay(Transform parent, int requiredLevel)
        {
            var shade = CreateLockPart(parent, "Skill_Lock", Vector2.zero, new Vector2(48f, 48f),
                new Color(0f, 0f, 0f, 0.65f));
            Color steel = new Color(0.72f, 0.77f, 0.84f);
            // Two crossing chains; each hollow link has four metal edges.
            foreach (float angle in new[] { -35f, 35f })
            {
                var chain = new GameObject("Chain", typeof(RectTransform));
                chain.transform.SetParent(shade.transform, false);
                chain.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                for (int i = -2; i <= 2; i++)
                {
                    float x = i * 10f;
                    CreateLockPart(chain.transform, "Link_Top", new Vector2(x, 3f), new Vector2(11f, 2f), steel);
                    CreateLockPart(chain.transform, "Link_Bottom", new Vector2(x, -3f), new Vector2(11f, 2f), steel);
                    CreateLockPart(chain.transform, "Link_Left", new Vector2(x - 4.5f, 0f), new Vector2(2f, 6f), steel);
                    CreateLockPart(chain.transform, "Link_Right", new Vector2(x + 4.5f, 0f), new Vector2(2f, 6f), steel);
                }
            }
            CreateLockPart(shade.transform, "Shackle_Top", new Vector2(0f, 9f), new Vector2(12f, 3f), steel);
            CreateLockPart(shade.transform, "Shackle_Left", new Vector2(-5f, 5f), new Vector2(3f, 9f), steel);
            CreateLockPart(shade.transform, "Shackle_Right", new Vector2(5f, 5f), new Vector2(3f, 9f), steel);
            CreateLockPart(shade.transform, "Lock_Border", new Vector2(0f, -3f), new Vector2(22f, 18f), new Color(0.15f, 0.12f, 0.06f));
            CreateLockPart(shade.transform, "Lock_Body", new Vector2(0f, -3f), new Vector2(18f, 14f), new Color(0.88f, 0.70f, 0.30f));
            CreateLockPart(shade.transform, "Keyhole", new Vector2(0f, -3f), new Vector2(3f, 6f), new Color(0.15f, 0.12f, 0.06f));

            var labelGo = new GameObject("Unlock_Level", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(shade.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.sizeDelta = new Vector2(48f, 12f);
            labelRt.anchoredPosition = new Vector2(0f, -19f);
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            LocalizedText.Set(label, $"Lv.{requiredLevel}");
            label.fontSize = 10f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            return shade.gameObject;
        }

        private static Image CreateLockPart(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private void LateUpdate()
        {
            if (!_isVisible) return;

            if (_cachedPlayer == null)
            {
                _cachedPlayer = FindAnyObjectByType<PlayerController>();
            }

            if (_cachedPlayer == null)
            {
                // No active player: hide cooldowns, show full icons
                for (int i = 0; i < _slots.Count; i++)
                {
                    _slots[i].UpdateLockState(false);
                    _slots[i].UpdateCooldown(0f, 1f);
                    _slots[i].UpdateKeyBinding();
                }
                return;
            }

            // Slot 0: Skill 1 (Carnage Burst)
            if (_slots.Count > 0)
            {
                _slots[0].UpdateLockState(!_cachedPlayer.IsSkill1Unlocked);
                _slots[0].UpdateCooldown(_cachedPlayer.SkillCooldownTimer, _cachedPlayer.SkillMaxCooldown);
                _slots[0].UpdateKeyBinding();
            }

            // Slot 1: Skill 2 (Buff / Iron Will)
            if (_slots.Count > 1)
            {
                _slots[1].UpdateLockState(!_cachedPlayer.IsSkill2Unlocked);
                _slots[1].UpdateCooldown(_cachedPlayer.BuffCooldownTimer, _cachedPlayer.BuffMaxCooldown);
                _slots[1].UpdateKeyBinding();
            }

            // Slot 2: Skill 3 (Excalibur)
            if (_slots.Count > 2)
            {
                _slots[2].UpdateLockState(!_cachedPlayer.IsSkill3Unlocked);
                _slots[2].UpdateCooldown(_cachedPlayer.ExcaliburCooldownTimer, _cachedPlayer.ExcaliburMaxCooldown);
                _slots[2].UpdateKeyBinding();
            }
        }

        #region Helper Classes
        [Serializable]
        public class SkillSlotHUD
        {
            public GameObject SlotRoot;
            public Image IconImage;
            public Image ShadowOverlay;
            public TextMeshProUGUI CooldownText;
            public TextMeshProUGUI KeyBadgeText;
            public Image FlashImage;
            public GameObject LockOverlay;
            public string SkillName;
            public string ActionName;
            public string DefaultKey;

            private bool _wasOnCooldown;
            private Coroutine _flashRoutine;
            private bool _isLocked;

            public void UpdateLockState(bool locked)
            {
                _isLocked = locked;
                if (LockOverlay != null) LockOverlay.SetActive(locked);
                if (!locked) return;
                _wasOnCooldown = false;
                if (_flashRoutine != null && Instance != null)
                    Instance.StopCoroutine(_flashRoutine);
                _flashRoutine = null;
                if (FlashImage != null) FlashImage.color = Color.clear;
                if (ShadowOverlay != null) ShadowOverlay.gameObject.SetActive(false);
                if (CooldownText != null) LocalizedText.Set(CooldownText, "");
                if (IconImage != null) IconImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            }

            public void UpdateCooldown(float currentTimer, float maxTimer)
            {
                if (_isLocked) return;
                bool onCooldown = currentTimer > 0.01f;
                float normalized = maxTimer > 0.001f ? Mathf.Clamp01(currentTimer / maxTimer) : 0f;

                if (onCooldown)
                {
                    if (ShadowOverlay != null)
                    {
                        if (!ShadowOverlay.gameObject.activeSelf)
                            ShadowOverlay.gameObject.SetActive(true);

                        ShadowOverlay.fillAmount = normalized;
                    }

                    if (CooldownText != null)
                    {
                        TheLastKnight.UI.LocalizedText.Set(CooldownText, currentTimer >= 10f
                            ? Mathf.CeilToInt(currentTimer).ToString()
                            : currentTimer.ToString("0.0"));
                    }

                    if (IconImage != null)
                    {
                        IconImage.color = new Color(0.30f, 0.30f, 0.30f, 1f);
                    }

                    _wasOnCooldown = true;
                }
                else
                {
                    if (ShadowOverlay != null)
                    {
                        ShadowOverlay.fillAmount = 0f;
                        if (ShadowOverlay.gameObject.activeSelf)
                            ShadowOverlay.gameObject.SetActive(false);
                    }

                    if (CooldownText != null)
                    {
                        TheLastKnight.UI.LocalizedText.Set(CooldownText, "");
                    }

                    if (IconImage != null)
                    {
                        IconImage.color = Color.white;
                    }

                    // Trigger ready flash when transitioning from on-cooldown to ready
                    if (_wasOnCooldown)
                    {
                        _wasOnCooldown = false;
                        TriggerReadyFlash();
                    }
                }
            }

            public void UpdateKeyBinding()
            {
                if (KeyBadgeText != null && !string.IsNullOrEmpty(ActionName))
                {
                    string key = KeyRebindManager.GetCurrentBindingDisplay(ActionName, 0);
                    TheLastKnight.UI.LocalizedText.Set(KeyBadgeText, (!string.IsNullOrEmpty(key) && key != "Unknown" && key != "N/A") ? key : DefaultKey);
                }
            }

            private void TriggerReadyFlash()
            {
                if (FlashImage != null && SlotRoot != null && SlotRoot.activeInHierarchy)
                {
                    var runner = Instance;
                    if (runner != null)
                    {
                        if (_flashRoutine != null) runner.StopCoroutine(_flashRoutine);
                        _flashRoutine = runner.StartCoroutine(FlashRoutine());
                    }
                }
            }

            private IEnumerator FlashRoutine()
            {
                if (FlashImage == null) yield break;

                FlashImage.color = new Color(1f, 1f, 1f, 0.7f);
                float duration = 0.25f;
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float alpha = Mathf.Lerp(0.7f, 0f, elapsed / duration);
                    FlashImage.color = new Color(1f, 1f, 1f, alpha);
                    yield return null;
                }

                FlashImage.color = new Color(1f, 1f, 1f, 0f);
                _flashRoutine = null;
            }
        }
        #endregion
    }
}
