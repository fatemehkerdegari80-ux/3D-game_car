using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace NeonHighway
{
    // Whole UI is built from code (no prefabs / imported art). Rounded panels, the radial speed gauge and the
    // hearts are generated as small procedural sprites at start-up.
    //
    // Required screens from the assignment are all here: Main Menu (Start / Exit), Pause (Resume / Restart /
    // Main Menu), Game Over (score, high score, restart) and the in-game HUD (speed, fuel bar, lives, score, best).
    public sealed class NeonInterface
    {
        readonly GameManager game;
        RectTransform canvasRect;
        GameObject hud, menu, pause, over;
        Text speedText, neonText, scoreText, recordText, distanceText, fuelPercent, lowFuelText;
        Text endScore, endBest, menuBest, newRecordText;
        Image fuelFill, speedRing, damageFlash, titleGlow;
        RectTransform fuelRect;
        readonly Image[] hearts = new Image[3];
        readonly Text[] popupText = new Text[8];
        readonly float[] popupAge = new float[8];
        readonly Vector3[] popupWorld = new Vector3[8];
        readonly bool[] popupLive = new bool[8];
        int lastLives = 3, popupCursor;
        float heartPunch, flash, speedNorm;
        Camera cam;

        static Font font;
        static Sprite roundFill, roundLine, ringSprite, discSprite, heartSprite, glowSprite, vignetteSprite, fadeDownSprite, fadeRightSprite;

        static readonly Color Ink = new Color(.03f, .05f, .10f, 1f);
        static readonly Color Glass = new Color(.025f, .035f, .070f, .66f);
        static readonly Color Pale = new Color(.82f, .88f, .95f, 1f);
        static readonly Color Dim = new Color(.55f, .66f, .80f, .92f);

        // ------------------------------------------------------------------ fonts

        static Font GetFont()
        {
            if (font) return font;
            // Prefer a "racing HUD" style system font when present (Bahnschrift ships with Windows 10/11),
            // otherwise fall back to Unity's built-in font so the game still runs everywhere.
            try
            {
                string[] installed = Font.GetOSInstalledFontNames();
                string[] wanted = { "Bahnschrift", "Segoe UI", "Trebuchet MS", "Helvetica Neue", "Arial" };
                string chosen = null;
                for (int w = 0; w < wanted.Length && chosen == null; w++)
                    for (int i = 0; i < installed.Length; i++)
                        if (string.Equals(installed[i], wanted[w], System.StringComparison.OrdinalIgnoreCase)) { chosen = installed[i]; break; }
                if (chosen != null) font = Font.CreateDynamicFontFromOSFont(chosen, 32);
            }
            catch { }
            if (!font) { try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { } }
            if (!font) { try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
            if (!font) font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Liberation Sans", "DejaVu Sans" }, 32);
            return font;
        }

        // ------------------------------------------------------------------ procedural sprites

        static Sprite MakeSprite(int w, int h, System.Func<int, int, Color> pixel, Vector4 border)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = pixel(x, y);
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        static float RoundedDistance(float x, float y, float w, float h, float r)
        {
            float qx = Mathf.Abs(x - w * .5f) - (w * .5f - r);
            float qy = Mathf.Abs(y - h * .5f) - (h * .5f - r);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static void BuildSprites()
        {
            if (roundFill) return;
            const int S = 64; const float R = 24f;
            roundFill = MakeSprite(S, S, (x, y) =>
                new Color(1, 1, 1, Mathf.Clamp01(.5f - RoundedDistance(x + .5f, y + .5f, S, S, R))), new Vector4(R + 1, R + 1, R + 1, R + 1));
            roundLine = MakeSprite(S, S, (x, y) =>
            {
                float d = RoundedDistance(x + .5f, y + .5f, S, S, R);
                float a = Mathf.Clamp01(.5f - d) - Mathf.Clamp01(.5f - (d + 2.6f));
                return new Color(1, 1, 1, Mathf.Clamp01(a));
            }, new Vector4(R + 1, R + 1, R + 1, R + 1));
            ringSprite = MakeSprite(256, 256, (x, y) =>
            {
                float dx = x + .5f - 128f, dy = y + .5f - 128f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float outer = Mathf.Clamp01(126f - d + .5f);
                float inner = Mathf.Clamp01(d - 108f + .5f);
                return new Color(1, 1, 1, Mathf.Min(outer, inner));
            }, Vector4.zero);
            discSprite = MakeSprite(128, 128, (x, y) =>
            {
                float dx = x + .5f - 64f, dy = y + .5f - 64f;
                return new Color(1, 1, 1, Mathf.Clamp01(63.5f - Mathf.Sqrt(dx * dx + dy * dy)));
            }, Vector4.zero);
            glowSprite = MakeSprite(128, 128, (x, y) =>
            {
                float dx = (x + .5f - 64f) / 64f, dy = (y + .5f - 64f) / 64f;
                float t = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                return new Color(1, 1, 1, t * t);
            }, Vector4.zero);
            vignetteSprite = MakeSprite(128, 128, (x, y) =>
            {
                float dx = (x + .5f - 64f) / 64f, dy = (y + .5f - 64f) / 64f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                return new Color(1, 1, 1, Mathf.SmoothStep(0f, 1f, (d - .50f) / .95f));
            }, Vector4.zero);
            fadeDownSprite = MakeSprite(4, 64, (x, y) => new Color(1, 1, 1, Mathf.Pow(y / 63f, 1.6f)), Vector4.zero);
            fadeRightSprite = MakeSprite(64, 4, (x, y) => new Color(1, 1, 1, Mathf.Pow(1f - x / 63f, 1.35f)), Vector4.zero);
            heartSprite = MakeSprite(64, 64, (x, y) =>
            {
                // 3x3 super-sampled implicit heart curve (x^2+y^2-1)^3 - x^2*y^3 <= 0
                int hit = 0;
                for (int sy = 0; sy < 3; sy++)
                    for (int sx = 0; sx < 3; sx++)
                    {
                        float u = (x + (sx + .5f) / 3f - 32f) / 24f;
                        float v = (y + (sy + .5f) / 3f - 34f) / 24f;
                        float a = u * u + v * v - 1f;
                        if (a * a * a - u * u * v * v * v <= 0f) hit++;
                    }
                return new Color(1, 1, 1, hit / 9f);
            }, Vector4.zero);
        }

        // ------------------------------------------------------------------ small UI builders

        static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static void Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = anchor; r.anchorMax = anchor; r.pivot = pivot;
            r.anchoredPosition = pos; r.sizeDelta = size;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            r.pivot = new Vector2(.5f, .5f);
        }

        static Image Img(Transform parent, string name, Sprite sprite, Color color, Image.Type type = Image.Type.Simple)
        {
            var rt = NewRect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.color = color; img.type = type; img.raycastTarget = false;
            return img;
        }

        static Text Txt(Transform parent, string value, int size, Color color, TextAnchor align, FontStyle style,
                        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box, bool shadow = false)
        {
            var rt = NewRect(parent, "Text: " + value);
            Place(rt, anchor, pivot, pos, box);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = GetFont(); t.fontSize = size; t.fontStyle = style; t.text = value; t.color = color; t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false; t.supportRichText = true;
            if (shadow)
            {
                var sh = rt.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0, 0, 0, .70f); sh.effectDistance = new Vector2(2f, -3f);
            }
            return t;
        }

        // Glass panel with a thin neon outline.
        static RectTransform Card(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color fill, Color line)
        {
            var rt = NewRect(parent, name);
            Place(rt, anchor, pivot, pos, size);
            var f = rt.gameObject.AddComponent<Image>();
            f.sprite = roundFill; f.type = Image.Type.Sliced; f.color = fill; f.raycastTarget = false;
            var l = Img(rt, "Outline", roundLine, line, Image.Type.Sliced);
            Stretch(l.rectTransform);
            return rt;
        }

        static Color Scaled(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, 1f); }

        Button MakeButton(Transform parent, string label, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
                          bool primary, Color accent, System.Action action)
        {
            var rt = NewRect(parent, "Button: " + label);
            Place(rt, anchor, pivot, pos, size);
            var fill = rt.gameObject.AddComponent<Image>();
            fill.sprite = roundFill; fill.type = Image.Type.Sliced; fill.color = Color.white;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            var nav = new Navigation(); nav.mode = Navigation.Mode.None; button.navigation = nav;
            var cb = button.colors;
            cb.colorMultiplier = 1f; cb.fadeDuration = .08f;
            if (primary)
            {
                cb.normalColor = Scaled(accent, .92f);
                cb.highlightedColor = Color.Lerp(accent, Color.white, .38f);
                cb.pressedColor = Scaled(accent, .68f);
                cb.selectedColor = cb.normalColor;
            }
            else
            {
                cb.normalColor = new Color(.08f, .11f, .17f, .94f);
                cb.highlightedColor = new Color(.17f, .22f, .33f, 1f);
                cb.pressedColor = new Color(.05f, .07f, .11f, 1f);
                cb.selectedColor = cb.normalColor;
            }
            button.colors = cb;
            var line = Img(rt, "Outline", roundLine, new Color(accent.r, accent.g, accent.b, primary ? .9f : .6f), Image.Type.Sliced);
            Stretch(line.rectTransform);
            var t = Txt(rt, label, 30, primary ? Ink : Pale, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, size);
            Stretch(t.rectTransform);
            if (action != null) button.onClick.AddListener(() => action());
            return button;
        }

        static Image Ring(Transform parent, string name, Color color, float fill)
        {
            var img = Img(parent, name, ringSprite, color);
            Stretch(img.rectTransform);
            img.rectTransform.localRotation = Quaternion.Euler(0, 0, -45f);
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Bottom;
            img.fillClockwise = true;
            img.fillAmount = fill;
            return img;
        }

        // ------------------------------------------------------------------ construction

        public NeonInterface(GameManager game)
        {
            this.game = game;
            cam = Camera.main;
            BuildSprites();
            var eventSystem = new GameObject("Input System UI EventSystem");
            eventSystem.AddComponent<EventSystem>();
            // InputSystemUIInputModule initializes its default actions in OnEnable.
            eventSystem.AddComponent<InputSystemUIInputModule>();
            var canvasGO = new GameObject("Neon Highway | UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .50f;
            canvasRect = canvasGO.GetComponent<RectTransform>();

            BuildHud(canvasGO.transform);
            damageFlash = Img(canvasGO.transform, "Damage flash", vignetteSprite, new Color(1f, .08f, .10f, 0f));
            Stretch(damageFlash.rectTransform);
            BuildMenu(canvasGO.transform);
            BuildPause(canvasGO.transform);
            BuildGameOver(canvasGO.transform);
            ShowMenu(game.Best);
        }

        void BuildHud(Transform root)
        {
            var hudRT = NewRect(root, "Gameplay HUD"); Stretch(hudRT); hud = hudRT.gameObject;
            var cyanHex = "#" + ColorUtility.ToHtmlStringRGB(NeonVisuals.Cyan);

            var top = Img(hud.transform, "Top fade", fadeDownSprite, new Color(.01f, .01f, .04f, .62f));
            top.rectTransform.anchorMin = new Vector2(0, 1); top.rectTransform.anchorMax = new Vector2(1, 1);
            top.rectTransform.pivot = new Vector2(.5f, 1); top.rectTransform.anchoredPosition = Vector2.zero; top.rectTransform.sizeDelta = new Vector2(0, 230);
            var bottom = Img(hud.transform, "Bottom fade", fadeDownSprite, new Color(.01f, .01f, .04f, .62f));
            bottom.rectTransform.anchorMin = new Vector2(0, 0); bottom.rectTransform.anchorMax = new Vector2(1, 0);
            bottom.rectTransform.pivot = new Vector2(.5f, .5f); bottom.rectTransform.anchoredPosition = new Vector2(0, 120);
            bottom.rectTransform.sizeDelta = new Vector2(0, 240); bottom.rectTransform.localScale = new Vector3(1, -1, 1);

            // Logo
            Txt(hud.transform, "<color=" + cyanHex + ">NEON</color> HIGHWAY", 36, Color.white, TextAnchor.UpperLeft, FontStyle.BoldAndItalic,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -32), new Vector2(520, 56), true);
            var bar = Img(hud.transform, "Logo accent", null, NeonVisuals.Pink);
            Place(bar.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(58, -84), new Vector2(210, 4));

            // Score card
            var score = Card(hud.transform, "Score card", new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -26), new Vector2(470, 132), Glass,
                new Color(NeonVisuals.Cyan.r, NeonVisuals.Cyan.g, NeonVisuals.Cyan.b, .38f));
            Txt(score, "SCORE", 20, Dim, TextAnchor.UpperCenter, FontStyle.Bold, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -12), new Vector2(300, 28));
            scoreText = Txt(score, "000000", 62, Color.white, TextAnchor.UpperCenter, FontStyle.BoldAndItalic, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -30), new Vector2(420, 76), true);
            recordText = Txt(score, "BEST  000000", 22, NeonVisuals.Gold, TextAnchor.UpperCenter, FontStyle.Bold, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -102), new Vector2(420, 28));

            // Lives
            var lives = Card(hud.transform, "Lives card", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-56, -30), new Vector2(262, 88), Glass,
                new Color(NeonVisuals.Pink.r, NeonVisuals.Pink.g, NeonVisuals.Pink.b, .42f));
            for (int i = 0; i < hearts.Length; i++)
            {
                hearts[i] = Img(lives, "Life " + (i + 1), heartSprite, NeonVisuals.Pink);
                Place(hearts[i].rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2((i - 1) * 76f, 0), new Vector2(58, 58));
            }

            // Speedometer: 270 degree radial gauge
            var gauge = NewRect(hud.transform, "Speedometer");
            Place(gauge, new Vector2(0, 0), new Vector2(0, 0), new Vector2(48, 46), new Vector2(330, 330));
            var plate = Img(gauge, "Gauge plate", discSprite, new Color(.02f, .03f, .06f, .70f)); Stretch(plate.rectTransform);
            var track = Ring(gauge, "Gauge track", new Color(.20f, .28f, .40f, .55f), .75f);
            speedRing = Ring(gauge, "Gauge fill", NeonVisuals.Cyan, 0f);
            speedText = Txt(gauge, "0", 104, Color.white, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 30), new Vector2(260, 120), true);
            Txt(gauge, "KM/H", 24, NeonVisuals.Cyan, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -46), new Vector2(200, 34));
            neonText = Txt(gauge, "NEON POWER  0%", 19, Dim, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -124), new Vector2(260, 28));

            // Fuel
            var fuel = Card(hud.transform, "Fuel card", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-48, 46), new Vector2(530, 116), Glass,
                new Color(NeonVisuals.Mint.r, NeonVisuals.Mint.g, NeonVisuals.Mint.b, .38f));
            Txt(fuel, "FUEL", 22, NeonVisuals.Mint, TextAnchor.UpperLeft, FontStyle.Bold, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -16), new Vector2(200, 30));
            fuelPercent = Txt(fuel, "100%", 28, Color.white, TextAnchor.UpperRight, FontStyle.BoldAndItalic, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -12), new Vector2(200, 36));
            var track2 = Img(fuel, "Fuel track", roundFill, new Color(.07f, .12f, .17f, .95f), Image.Type.Sliced);
            Place(track2.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 24), new Vector2(474, 30));
            fuelFill = Img(track2.transform, "Fuel fill", roundFill, NeonVisuals.Mint, Image.Type.Sliced);
            fuelRect = fuelFill.rectTransform;
            fuelRect.anchorMin = Vector2.zero; fuelRect.anchorMax = Vector2.one; fuelRect.offsetMin = Vector2.zero; fuelRect.offsetMax = Vector2.zero;
            for (int k = 1; k <= 3; k++)
            {
                var tick = Img(track2.transform, "Fuel tick", null, new Color(.02f, .03f, .06f, .55f));
                tick.rectTransform.anchorMin = new Vector2(k * .25f, 0); tick.rectTransform.anchorMax = new Vector2(k * .25f, 1);
                tick.rectTransform.pivot = new Vector2(.5f, .5f); tick.rectTransform.anchoredPosition = Vector2.zero; tick.rectTransform.sizeDelta = new Vector2(3, 0);
            }
            lowFuelText = Txt(hud.transform, "LOW FUEL", 34, NeonVisuals.Pink, TextAnchor.LowerRight, FontStyle.BoldAndItalic,
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(-52, 172), new Vector2(360, 44), true);
            lowFuelText.gameObject.SetActive(false);

            distanceText = Txt(hud.transform, "DISTANCE  0 M", 24, Pale, TextAnchor.LowerCenter, FontStyle.Bold,
                new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 54), new Vector2(520, 34), true);
            Txt(hud.transform, "A / D  or  LEFT / RIGHT  :  change lane      ESC  :  pause      F  :  fireworks test", 17, new Color(.55f, .70f, .85f, .75f),
                TextAnchor.LowerCenter, FontStyle.Normal, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 22), new Vector2(900, 26));

            // Floating "+50" / "+30 FUEL" pickups
            for (int i = 0; i < popupText.Length; i++)
            {
                popupText[i] = Txt(hud.transform, "+", 40, Color.white, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic,
                    new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(260, 60), true);
                popupText[i].gameObject.SetActive(false);
            }
        }

        void BuildMenu(Transform root)
        {
            var rt = NewRect(root, "Main menu"); Stretch(rt); menu = rt.gameObject;
            var vig = Img(menu.transform, "Vignette", vignetteSprite, new Color(0, 0, .02f, .85f)); Stretch(vig.rectTransform);
            var left = Img(menu.transform, "Left shade", fadeRightSprite, new Color(.01f, .015f, .04f, .86f));
            left.rectTransform.anchorMin = new Vector2(0, 0); left.rectTransform.anchorMax = new Vector2(0, 1);
            left.rectTransform.pivot = new Vector2(0, .5f); left.rectTransform.anchoredPosition = Vector2.zero; left.rectTransform.sizeDelta = new Vector2(1150, 0);
            var topFade = Img(menu.transform, "Top fade", fadeDownSprite, new Color(0, 0, .02f, .55f));
            topFade.rectTransform.anchorMin = new Vector2(0, 1); topFade.rectTransform.anchorMax = new Vector2(1, 1);
            topFade.rectTransform.pivot = new Vector2(.5f, 1); topFade.rectTransform.anchoredPosition = Vector2.zero; topFade.rectTransform.sizeDelta = new Vector2(0, 260);

            titleGlow = Img(menu.transform, "Title glow", glowSprite, new Color(NeonVisuals.Cyan.r, NeonVisuals.Cyan.g, NeonVisuals.Cyan.b, .2f));
            Place(titleGlow.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(60, 170), new Vector2(980, 420));

            Txt(menu.transform, "NEON", 176, NeonVisuals.Cyan, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic,
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(128, 178), new Vector2(900, 200), true);
            Txt(menu.transform, "HIGHWAY", 100, Color.white, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic,
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(136, 56), new Vector2(900, 120), true);
            var accent = Img(menu.transform, "Title accent", null, NeonVisuals.Pink);
            Place(accent.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(140, -10), new Vector2(430, 5));
            Txt(menu.transform, "MIDNIGHT EXPRESSWAY  /  DRIVE INTO THE NIGHT", 24, new Color(.78f, .70f, 1f, .95f), TextAnchor.MiddleLeft, FontStyle.Bold,
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(140, -46), new Vector2(900, 36));

            var chip = Card(menu.transform, "Best score chip", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(140, -112), new Vector2(330, 56), Glass,
                new Color(NeonVisuals.Gold.r, NeonVisuals.Gold.g, NeonVisuals.Gold.b, .5f));
            menuBest = Txt(chip, "BEST  0", 26, NeonVisuals.Gold, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(320, 50));

            MakeButton(menu.transform, "START ENGINE", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(140, -204), new Vector2(480, 88), true, NeonVisuals.Cyan, game.Begin);
            MakeButton(menu.transform, "EXIT GAME", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(140, -308), new Vector2(480, 80), false, NeonVisuals.Violet, game.Quit);
            Txt(menu.transform, "3 LANES  /  COINS  /  FUEL  /  NO END", 20, Dim, TextAnchor.MiddleLeft, FontStyle.Bold,
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(142, -386), new Vector2(700, 30));
            Txt(menu.transform, "A / D  or  LEFT / RIGHT  :  change lane      ESC  :  pause      ENTER / SPACE  :  start", 17, new Color(.55f, .70f, .85f, .75f),
                TextAnchor.LowerLeft, FontStyle.Normal, new Vector2(0, 0), new Vector2(0, 0), new Vector2(142, 36), new Vector2(900, 26));
        }

        void BuildPause(Transform root)
        {
            var rt = NewRect(root, "Pause menu"); Stretch(rt); pause = rt.gameObject;
            var dim = Img(pause.transform, "Dim", null, new Color(.01f, .015f, .035f, .78f)); Stretch(dim.rectTransform);
            var vig = Img(pause.transform, "Vignette", vignetteSprite, new Color(0, 0, .02f, .6f)); Stretch(vig.rectTransform);
            var card = Card(pause.transform, "Pause card", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(640, 640),
                new Color(.025f, .035f, .07f, .95f), new Color(NeonVisuals.Cyan.r, NeonVisuals.Cyan.g, NeonVisuals.Cyan.b, .55f));
            Txt(card, "PAUSED", 84, Color.white, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 218), new Vector2(560, 110), true);
            Txt(card, "The road will wait for you.", 22, Dim, TextAnchor.MiddleCenter, FontStyle.Normal, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 150), new Vector2(500, 34));
            MakeButton(card, "RESUME", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 56), new Vector2(470, 82), true, NeonVisuals.Cyan, game.TogglePause);
            MakeButton(card, "RESTART", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -44), new Vector2(470, 78), false, NeonVisuals.Amber, game.Restart);
            MakeButton(card, "MAIN MENU", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -140), new Vector2(470, 78), false, NeonVisuals.Violet, game.ReturnToMenu);
        }

        void BuildGameOver(Transform root)
        {
            var rt = NewRect(root, "Game over"); Stretch(rt); over = rt.gameObject;
            var dim = Img(over.transform, "Dim", null, new Color(.02f, .01f, .03f, .80f)); Stretch(dim.rectTransform);
            var vig = Img(over.transform, "Red vignette", vignetteSprite, new Color(.8f, .05f, .12f, .55f)); Stretch(vig.rectTransform);
            var card = Card(over.transform, "Game over card", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(720, 760),
                new Color(.03f, .03f, .06f, .95f), new Color(NeonVisuals.Pink.r, NeonVisuals.Pink.g, NeonVisuals.Pink.b, .6f));
            Txt(card, "GAME OVER", 90, NeonVisuals.Pink, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 280), new Vector2(660, 120), true);
            newRecordText = Txt(card, "NEW RECORD!", 34, NeonVisuals.Gold, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 212), new Vector2(500, 48), true);
            Txt(card, "SCORE", 22, Dim, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 150), new Vector2(300, 30));
            endScore = Txt(card, "0", 96, Color.white, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 80), new Vector2(660, 120), true);
            var rule = Img(card, "Divider", null, new Color(1, 1, 1, .12f));
            Place(rule.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 10), new Vector2(480, 2));
            endBest = Txt(card, "HIGH SCORE  0", 34, NeonVisuals.Cyan, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -42), new Vector2(640, 50));
            MakeButton(card, "DRIVE AGAIN", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -150), new Vector2(480, 86), true, NeonVisuals.Cyan, game.Restart);
            MakeButton(card, "MAIN MENU", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -252), new Vector2(480, 78), false, NeonVisuals.Violet, game.ReturnToMenu);
        }

        // ------------------------------------------------------------------ state changes

        void Visible(bool h, bool m, bool p, bool o)
        {
            hud.SetActive(h); menu.SetActive(m); pause.SetActive(p); over.SetActive(o);
        }
        public void ShowMenu(int best)
        {
            menuBest.text = "BEST  " + best.ToString("N0");
            Visible(false, true, false, false);
        }
        public void ShowPlay()
        {
            lastLives = 3; heartPunch = 0f;
            for (int i = 0; i < popupLive.Length; i++) { popupLive[i] = false; popupText[i].gameObject.SetActive(false); }
            Visible(true, false, false, false);
        }
        public void ShowPause() { Visible(false, false, true, false); }
        public void ShowEnd(int score, int best, bool newRecord)
        {
            endScore.text = score.ToString("N0");
            endBest.text = "HIGH SCORE   " + best.ToString("N0");
            newRecordText.gameObject.SetActive(newRecord);
            Visible(false, false, false, true);
        }

        public void FlashDamage() { flash = 1f; }

        // Floating world-anchored text ("+50", "+30 FUEL").
        public void Popup(string text, Vector3 world, Color color)
        {
            int index = -1;
            for (int i = 0; i < popupLive.Length; i++) if (!popupLive[i]) { index = i; break; }
            if (index < 0) { index = popupCursor; popupCursor = (popupCursor + 1) % popupLive.Length; }
            popupLive[index] = true; popupAge[index] = 0f; popupWorld[index] = world;
            popupText[index].text = text; popupText[index].color = color;
            popupText[index].gameObject.SetActive(true);
        }

        public void UpdateHUD(float speed, float fuel, int lives, int score, int best, float distance)
        {
            speedNorm = Mathf.InverseLerp(27f, 61f, speed);
            speedText.text = Mathf.RoundToInt(speed * 3.6f).ToString();
            neonText.text = "NEON POWER  " + Mathf.RoundToInt(speedNorm * 100f) + "%";
            speedRing.fillAmount = .75f * Mathf.Lerp(.08f, 1f, speedNorm);
            speedRing.color = Color.Lerp(NeonVisuals.Cyan, NeonVisuals.Pink, speedNorm * speedNorm);

            float f = Mathf.Clamp01(fuel / 100f);
            fuelRect.anchorMax = new Vector2(f, 1f);
            fuelFill.color = fuel < 24f ? NeonVisuals.Pink : (fuel < 50f ? NeonVisuals.Amber : NeonVisuals.Mint);
            fuelPercent.text = Mathf.CeilToInt(fuel) + "%";
            lowFuelText.gameObject.SetActive(fuel < 24f);

            if (lives < lastLives) heartPunch = 1f;
            lastLives = lives;
            for (int i = 0; i < hearts.Length; i++)
                hearts[i].color = i < lives ? NeonVisuals.Pink : new Color(.22f, .20f, .28f, .85f);

            scoreText.text = score.ToString("000000");
            recordText.text = "BEST  " + best.ToString("000000");
            distanceText.text = "DISTANCE  " + Mathf.RoundToInt(distance).ToString("N0") + " M";
        }

        // Called every frame (unscaled time) for the little animations.
        public void Tick(float dt)
        {
            float t = Time.unscaledTime;
            if (flash > 0f)
            {
                flash = Mathf.Max(0f, flash - dt * 1.6f);
                damageFlash.color = new Color(1f, .08f, .10f, flash * .75f);
            }
            if (menu.activeSelf)
            {
                float k = .5f + .5f * Mathf.Sin(t * 1.5f);
                titleGlow.color = new Color(NeonVisuals.Cyan.r, NeonVisuals.Cyan.g, NeonVisuals.Cyan.b, .12f + .13f * k);
            }
            if (over.activeSelf && newRecordText.gameObject.activeSelf)
                newRecordText.rectTransform.localScale = Vector3.one * (1f + .07f * Mathf.Sin(t * 6f));
            if (!hud.activeSelf) return;

            if (lowFuelText.gameObject.activeSelf)
            {
                var c = NeonVisuals.Pink; c.a = Mathf.Lerp(.25f, 1f, .5f + .5f * Mathf.Sin(t * 9f));
                lowFuelText.color = c;
            }
            heartPunch = Mathf.Max(0f, heartPunch - dt * 2.2f);
            for (int i = 0; i < hearts.Length; i++)
            {
                float s = 1f;
                if (i == lastLives) s += heartPunch * .55f;                              // the heart that was just lost
                else if (lastLives == 1 && i == 0) s += .09f * Mathf.Sin(t * 8f);       // last heart beats
                hearts[i].rectTransform.localScale = new Vector3(s, s, 1f);
            }
            speedText.rectTransform.localScale = Vector3.one * (1f + .035f * speedNorm * Mathf.Sin(t * 22f));

            if (cam == null) cam = Camera.main;
            for (int i = 0; i < popupText.Length; i++)
            {
                if (!popupLive[i]) continue;
                popupAge[i] += dt;
                float u = popupAge[i] / .95f;
                if (u >= 1f || cam == null) { popupLive[i] = false; popupText[i].gameObject.SetActive(false); continue; }
                Vector3 sp = cam.WorldToScreenPoint(popupWorld[i] + Vector3.up * (1.3f + u * 2.6f));
                if (sp.z <= 0f) { popupText[i].gameObject.SetActive(false); continue; }
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, new Vector2(sp.x, sp.y), null, out local);
                popupText[i].rectTransform.anchoredPosition = local;
                var col = popupText[i].color; col.a = 1f - u * u; popupText[i].color = col;
                popupText[i].rectTransform.localScale = Vector3.one * (1.25f - .25f * Mathf.Min(1f, u * 4f));
            }
        }
    }
}
