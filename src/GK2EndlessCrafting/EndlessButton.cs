using System;
using System.Collections.Generic;
using System.Reflection;
using GK2EndlessCrafting.Core;
using HarmonyLib;
using LazyBearTechnology;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GK2EndlessCrafting
{
    // Кнопка «∞» в окне выбора крафта. Клонируется с игровой plusCraftButton,
    // поэтому остаётся LazyButton (сохраняется sprite и gamepad-навигация).
    internal static class EndlessButton
    {
        private static readonly Dictionary<int, LazyButton> _buttons = new Dictionary<int, LazyButton>();
        private static readonly Dictionary<int, TextMeshProUGUI> _labels = new Dictionary<int, TextMeshProUGUI>();
        // Родной цвет глифа «+» (для выкл-состояния), чтобы Sync мог вернуть его.
        private static readonly Dictionary<int, Color> _labelColors = new Dictionary<int, Color>();
        private const char InfinityChar = '\u221E';
        // Открытые окна крафта — чтобы пересинхронизировать кнопку «∞» при смене
        // режима из другого места (маркер строки очереди), а не только при открытии.
        private static readonly Dictionary<int, UIBaseCraftSelectionWindow> _windows =
            new Dictionary<int, UIBaseCraftSelectionWindow>();
        // Подпись состояния кнопки (число детей : высота rect) — дешёвый guard для
        // Refresh, чтобы поздняя уборка чужих глифов не выполнялась каждый кадр.
        private static readonly Dictionary<int, string> _lastSig = new Dictionary<int, string>();

        private static readonly FieldInfo _plusField =
            AccessTools.Field(typeof(UIBaseCraftSelectionWindow), "plusCraftButton");

        // Игровая кнопка «∞» на строке очереди (скрыта игрой) — источник родного глифа ∞.
        private static readonly FieldInfo _infButtonField =
            AccessTools.Field(typeof(UICraftQueueElementWidget), "infCraftButton");
        private static readonly FieldInfo _iconTransitionsField =
            AccessTools.Field(typeof(LazyButton), "iconTransitions");

        // Кэш родного глифа ∞ (TMP-стиль ИЛИ спрайт-иконка) + описание для лога.
        private static GlyphInfo _glyph;
        private static float _glyphNextSearch;
        private static string _glyphDesc = "(none)";
        private static bool _infDumped;
        // Иконка-картинка «∞» на нашей кнопке (когда глиф — спрайт).
        private static readonly Dictionary<int, Image> _icons = new Dictionary<int, Image>();

        private sealed class GlyphInfo
        {
            internal Sprite Sprite;      // глиф-иконка (не рамка)
            internal TMP_FontAsset Font; // либо TMP-стиль родной подписи ∞
            internal Material FontMat;
            internal float FontSize;
            internal Color Color = Color.white;
            internal string Source = "(none)";
            internal bool IsSprite => Sprite != null;
            internal bool IsTmp => Sprite == null && Font != null;
        }

        // LazyUIEvent-поля копируются Instantiate вместе с компонентом; их тоже надо
        // обнулить, иначе клик по «∞» дёрнет «+». onClick (Button) чистим отдельно.
        private static readonly FieldInfo[] _lazyEventFields =
        {
            AccessTools.Field(typeof(LazyButton), "onDown"),
            AccessTools.Field(typeof(LazyButton), "onNotInteractableDown"),
            AccessTools.Field(typeof(LazyButton), "onUp"),
            AccessTools.Field(typeof(LazyButton), "onNotInteractableUp"),
            AccessTools.Field(typeof(LazyButton), "onEnter"),
            AccessTools.Field(typeof(LazyButton), "onNotInteractableEnter"),
            AccessTools.Field(typeof(LazyButton), "onExit"),
            AccessTools.Field(typeof(LazyButton), "onNotInteractableExit"),
            AccessTools.Field(typeof(LazyButton), "onNotInteractableClick"),
        };

        private static bool ModOn
            => Plugin.Mod != null && (Plugin.Mod.Enabled == null || Plugin.Mod.Enabled.Value);

        internal static void Ensure(UIBaseCraftSelectionWindow window)
        {
            if (window == null) return;
            try
            {
                int key = window.GetInstanceID();
                _windows[key] = window;
                if (_buttons.TryGetValue(key, out var existing) && existing != null)
                {
                    existing.gameObject.SetActive(ModOn);
                    if (ModOn) Sync(window);
                    return;
                }

                if (!ModOn) return;

                var plus = _plusField != null ? _plusField.GetValue(window) as LazyButton : null;
                if (plus == null) return; // у окна нет выбора рецепта — кнопки нет
                var parent = plus.transform.parent;
                if (parent == null) return;

                var go = UnityEngine.Object.Instantiate(plus.gameObject, parent);
                go.name = "EndlessCraftBtn";
                var btn = go.GetComponent<LazyButton>();
                if (btn == null) { UnityEngine.Object.Destroy(go); return; }

                // У клона могли скопироваться сериализованные слушатели «+».
                btn.onClick.RemoveAllListeners();
                ClearLazyEvents(btn);

                var prt = plus.transform as RectTransform;
                var rt = btn.transform as RectTransform;
                var srcImg = plus.targetGraphic as Image;
                var srcSprite = srcImg != null ? srcImg.sprite : null;
                if (prt != null && rt != null)
                {
                    rt.anchorMin = prt.anchorMin;
                    rt.anchorMax = prt.anchorMax;
                    rt.pivot = prt.pivot;
                    rt.sizeDelta = prt.sizeDelta;
                    rt.anchoredPosition = prt.anchoredPosition + new Vector2(prt.sizeDelta.x + 6f, 0f);
                }

                // Клон сохраняет СВОЙ спрайт — это гарантированно вид соседней игровой
                // кнопки «+» (правильный размер/скругления/цвета). Глобальный GameStyle
                // применяем ТОЛЬКО если у источника спрайта нет (иначе нестабильный
                // хевристический поиск подменял кнопку чужой иконкой).
                var cloneImg = btn.targetGraphic as Image;
                if (cloneImg != null && cloneImg.sprite == null)
                {
                    var fallback = GameStyle.ButtonSprite;
                    if (fallback != null)
                    {
                        cloneImg.sprite = fallback;
                        cloneImg.type = fallback.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
                    }
                }

                // Считываем стиль РОДНОГО глифа «+» (TMP) ДО удаления детей — чтобы наш
                // «∞» выглядел так же (шрифт/материал/размер/цвет).
                var srcGlyph = btn.GetComponentInChildren<TextMeshProUGUI>(true);
                var srcFont = srcGlyph != null ? srcGlyph.font : null;
                var srcMat = srcGlyph != null ? srcGlyph.fontSharedMaterial : null;
                var srcSize = srcGlyph != null ? srcGlyph.fontSize : 0f;
                var srcColor = srcGlyph != null ? srcGlyph.color : Color.white;

                // Убираем ВСЕ дочерние визуалы клона (родной «+» — TMP и/или Image),
                // иначе на кнопке окажутся два глифа. Фон (targetGraphic) сохраняем.
                int removed = RemoveChildVisuals(btn);

                // Отключаем цветовые переходы: иначе DoStateTransition сбрасывает подсветку.
                var selectable = btn as Selectable;
                if (selectable != null) selectable.transition = Selectable.Transition.None;

                var label = BuildLabel(btn, rt, srcFont, srcMat, srcSize, srcColor);
                btn.onClick.AddListener(() => Toggle(window));

                _buttons[key] = btn;
                _labels[key] = label;
                _labelColors[key] = label.color;
                Sync(window);

                // Одноразовая диагностика внешнего вида кнопки (на каждую созданную кнопку).
                LogButtonCreated(srcSprite, cloneImg, label, removed, rt, key);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("endless: ensure button: " + ex.Message);
            }
        }

        internal static void Sync(UIBaseCraftSelectionWindow window)
        {
            if (window == null) return;
            try
            {
                int key = window.GetInstanceID();
                if (!_buttons.TryGetValue(key, out var btn) || btn == null) return;

                // 1) Поздняя уборка чужих глифов + поддержка размера «∞». Игра создаёт
                //    свой «+» ЛЕНИВО (после нашей кнопки), поэтому перепроверяем на каждом
                //    sync/redraw. Дёшево: работа только при смене состава детей/высоты.
                var rt = btn.transform as RectTransform;
                int height = rt != null ? Mathf.RoundToInt(Mathf.Abs(rt.rect.height)) : 0;
                string sig = btn.transform.childCount + ":" + height;
                if (!_lastSig.TryGetValue(key, out var prev) || prev != sig)
                {
                    _lastSig[key] = sig;
                    int hidden = CleanForeignChildren(btn, key);
                    ReassertLabel(btn, key, rt);
                    if (hidden > 0)
                        Plugin.Log?.LogInfo("endless: button " + key + " late cleanup hidden=" + hidden);
                }

                // 1b) Глиф: как только появилась игровая строка очереди — берём её родной
                //     спрайт ∞ и заменяем TMP-подпись картинкой (дёшево: спрайт кэшируется).
                ApplyGlyph(btn, key);

                // 2) Цвет. Тинтуем ТОЛЬКО фон; подпись — родной цвет глифа (выкл) или
                //    белый на акцентном фоне (вкл), чтобы оставалась читаемой.
                bool on = Plugin.Registry != null && Plugin.Registry.IsOn(StationKey.Of(window));
                var img = btn.targetGraphic as Image;
                if (img != null) img.color = on ? GameStyle.Accent : Color.white;
                if (_labels.TryGetValue(key, out var label) && label != null)
                    label.color = on ? Color.white
                        : (_labelColors.TryGetValue(key, out var c) ? c : Color.white);
            }
            catch (Exception ex)
            {
                Trace("sync: " + ex.Message);
            }
        }

        // Пересинхронизировать кнопки «∞» во всех открытых окнах крафта. Вызывается
        // из EndlessMode.TurnOn/TurnOff, чтобы подсветка не отставала при смене
        // режима из строки очереди.
        internal static void SyncAll()
        {
            if (_windows.Count == 0) return;
            foreach (var key in new List<int>(_windows.Keys))
            {
                var w = _windows[key];
                if (w == null) { _windows.Remove(key); continue; }
                Sync(w);
            }
        }

        // Вызывается postfix'ами Hide: базового UIBaseCraftSelectionWindow и конкретных
        // UIFuelCraftWindow/UISingleCraftWindow. После Hide окно лишь деактивируется,
        // поэтому созданные объекты надо реально уничтожить (не только очистить
        // словари) — иначе повторное открытие накопит клоны. Метод идемпотентен,
        // повторный вызов (базовый + производный postfix) безопасен.
        internal static void Clear()
        {
            foreach (var kv in _buttons)
                if (kv.Value != null) UnityEngine.Object.Destroy(kv.Value.gameObject);
            _buttons.Clear();
            _labels.Clear();
            _labelColors.Clear();
            _icons.Clear();
            _windows.Clear();
            _lastSig.Clear();
        }

        private static void Toggle(UIBaseCraftSelectionWindow window)
        {
            try
            {
                var id = StationKey.Of(window);
                var recipe = StationKey.RecipeIdOf(window);
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(recipe)) return;

                // Обе ветки идут через EndlessMode — общую точку с маркером строки
                // очереди (реестр + нативный флаг + сохранение всегда согласованы).
                if (Plugin.Registry.IsOn(id)) EndlessMode.TurnOff(id);
                else EndlessMode.TurnOn(id, recipe, StationKey.DataOf(window)?.ParamsData);
                Sync(window);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("endless: toggle: " + ex.Message);
            }
        }

        private static void ClearLazyEvents(LazyButton btn)
        {
            if (btn == null) return;
            foreach (var f in _lazyEventFields)
            {
                if (f == null) continue;
                try
                {
                    var evt = f.GetValue(btn) as UnityEngine.Events.UnityEvent;
                    if (evt != null) evt.RemoveAllListeners();
                }
                catch { }
            }
        }

        private static void Trace(string msg)
        {
            if (Plugin.Mod != null && Plugin.Mod.DebugLog != null && Plugin.Mod.DebugLog.Value)
                Plugin.Log?.LogInfo("endless: " + msg);
        }

        // Убирает все дочерние визуалы клона (родной глиф «+» — TMP и/или Image).
        // Фон (targetGraphic и его ветку) сохраняем. Возвращает число удалённых.
        private static int RemoveChildVisuals(LazyButton btn)
        {
            var parent = btn != null ? btn.transform : null;
            if (parent == null) return 0;
            var keep = btn.targetGraphic != null ? btn.targetGraphic.transform : null;
            var children = new List<Transform>(parent.childCount);
            for (int i = 0; i < parent.childCount; i++) children.Add(parent.GetChild(i));
            int removed = 0;
            foreach (var child in children)
            {
                if (child == null) continue;
                // Пропускаем ветку фона (сам targetGraphic или его предок).
                if (keep != null && (child == keep || keep.IsChildOf(child))) continue;
                UnityEngine.Object.Destroy(child.gameObject);
                removed++;
            }
            return removed;
        }

        // Шрифт, который реально умеет рисовать «∞». Лучший — родной шрифт глифа «+»
        // (тогда «∞» выглядит как соседний «+»); иначе GameStyle (им же рисуется
        // маркер строки очереди, где «∞» отображается корректно); иначе — любой
        // TMP-шрифт сцены с этим глифом.
        private static TMP_FontAsset PickInfinityFont(TMP_FontAsset source)
        {
            // Родной шрифт глифа «+» — только если РЕАЛЬНО умеет ∞ (тогда «∞» выглядит
            // как соседний крисповый «+»).
            if (CanRenderInfinity(source)) return source;
            // Иначе — ровно та пара шрифт+материал, которой ∞ корректно рисуется в маркере
            // строки очереди (GameStyle). Сцену НЕ сканируем: прошлый скан выбирал
            // tiny_font, и получался блоб.
            var gs = GameStyle.Font;
            if (!CanRenderInfinity(gs))
            {
                try { Plugin.Log?.LogWarning("endless: GameStyle font reports no U+221E (even via fallbacks); using it anyway (marker combo)"); }
                catch { }
            }
            return gs;
        }

        // HasCharacter СО searchFallbacks=true: у GameStyle-шрифта ∞ может приходить из
        // fallback-шрифта, а без флага метод ложно возвращал false → выбирался блоб-шрифт.
        private static bool CanRenderInfinity(TMP_FontAsset f)
        {
            if (f == null) return false;
            try { return f.HasCharacter(InfinityChar, true, false); }
            catch { return false; }
        }

        // Разрешает родной глиф ∞: (a) дочерний TMP — родная подпись ∞; (b) дочерний
        // Image со спрайтом-НЕ-рамкой; (b2) iconTransitions-спрайт-не-рамка. Рамку
        // (targetGraphic, button_*/comm-btn*/*_left/*_right/*_cell) НЕ берём.
        // Ищем не чаще раза в секунду, пока не найдём; результат кэшируем.
        private static GlyphInfo ResolveGlyph()
        {
            if (_glyph != null) return _glyph;
            if (Time.unscaledTime < _glyphNextSearch) return null;
            _glyphNextSearch = Time.unscaledTime + 1f;
            try
            {
                foreach (var w in Resources.FindObjectsOfTypeAll<UICraftQueueElementWidget>())
                {
                    if (w == null) continue;
                    var lb = _infButtonField != null ? _infButtonField.GetValue(w) as LazyButton : null;
                    if (lb == null) continue;
                    DumpInfinityButton(lb); // один раз за сессию
                    var g = ChooseGlyph(lb);
                    if (g != null)
                    {
                        _glyph = g;
                        _glyphDesc = g.Source;
                        Plugin.Log?.LogInfo("endless: chosen infinity glyph: " + g.Source);
                        return g;
                    }
                }
            }
            catch (Exception ex) { Trace("glyph search: " + ex.Message); }
            return null;
        }

        private static GlyphInfo ChooseGlyph(LazyButton lb)
        {
            // (a) дочерний TMP — приоритет: это родная подпись «∞».
            var tmps = lb.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < tmps.Length; i++)
            {
                var t = tmps[i];
                if (t == null || t.font == null) continue;
                return new GlyphInfo
                {
                    Font = t.font,
                    FontMat = t.fontSharedMaterial,
                    FontSize = t.fontSize,
                    Color = t.color,
                    Source = "tmp font=" + t.font.name + " size=" + t.fontSize
                        + " from=" + PathOf(t.transform, lb.transform)
                };
            }

            // (b) дочерний Image со спрайтом-НЕ-рамкой.
            var imgs = lb.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < imgs.Length; i++)
            {
                var im = imgs[i];
                if (im == null || im.sprite == null) continue;
                if (im == lb.targetGraphic) continue;
                if (IsFrameSprite(im.sprite.name)) continue;
                return new GlyphInfo
                {
                    Sprite = im.sprite,
                    Source = "sprite " + SpriteName(im.sprite) + " from=" + PathOf(im.transform, lb.transform)
                };
            }

            // (b2) iconTransitions[].{default,highlighted,pressed,selected} — если не рамка.
            if (_iconTransitionsField != null)
            {
                var list = _iconTransitionsField.GetValue(lb) as System.Collections.IEnumerable;
                if (list != null)
                {
                    int i = 0;
                    foreach (var it in list)
                    {
                        if (it == null) { i++; continue; }
                        var type = it.GetType();
                        foreach (var fn in new[] { "defaultSprite", "highlightedSprite", "pressedSprite", "selectedSprite" })
                        {
                            var f = type.GetField(fn);
                            var spv = f != null ? f.GetValue(it) as Sprite : null;
                            if (spv != null && !IsFrameSprite(spv.name))
                                return new GlyphInfo
                                {
                                    Sprite = spv,
                                    Source = "iconTransitions#" + i + "." + fn + " sprite=" + SpriteName(spv)
                                };
                        }
                        i++;
                    }
                }
            }
            return null; // (c) фолбэк — маркерный шрифт (наша подпись уже такая)
        }

        // Спрайт-рамка (кнопка/ячейка) — это НЕ глиф ∞.
        private static bool IsFrameSprite(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            var n = name.ToLowerInvariant();
            return n.StartsWith("button") || n.Contains("comm-btn") || n.StartsWith("comm_btn")
                || n.EndsWith("_left") || n.EndsWith("_right") || n.EndsWith("_cell")
                || n.Contains("frame");
        }

        private static string PathOf(Transform t, Transform root)
        {
            if (t == null) return "?";
            var parts = new List<string>();
            var cur = t;
            while (cur != null && cur != root)
            {
                parts.Add(cur.name);
                cur = cur.parent;
            }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        // Один раз за сессию: полный дамп иерархии infCraftButton (Image/TMP + LazyButton
        // поля иконок) — доказательство для выбора глифа.
        private static void DumpInfinityButton(LazyButton lb)
        {
            if (_infDumped || lb == null) return;
            _infDumped = true;
            try
            {
                Plugin.Log?.LogInfo("endless: --- infCraftButton dump (root=" + lb.gameObject.name + ") ---");
                DumpNode(lb.gameObject, lb.gameObject.name);
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                foreach (var f in typeof(LazyButton).GetFields(flags))
                {
                    object v;
                    try { v = f.GetValue(lb); } catch { continue; }
                    if (v == null) continue;
                    var sp = v as Sprite;
                    if (sp != null) { Plugin.Log?.LogInfo("endless:   LazyButton." + f.Name + " sprite=" + SpriteName(sp)); continue; }
                    var im = v as Image;
                    if (im != null) { Plugin.Log?.LogInfo("endless:   LazyButton." + f.Name + " image.sprite=" + SpriteName(im.sprite)); continue; }
                    var list = v as System.Collections.IEnumerable;
                    if (list != null && !(v is string))
                    {
                        int i = 0;
                        foreach (var it in list)
                        {
                            if (it == null) { i++; continue; }
                            foreach (var fn in new[] { "defaultSprite", "highlightedSprite", "pressedSprite", "selectedSprite", "disabledSprite" })
                            {
                                var ff = it.GetType().GetField(fn);
                                var sv = ff != null ? ff.GetValue(it) as Sprite : null;
                                if (sv != null)
                                    Plugin.Log?.LogInfo("endless:   LazyButton." + f.Name + "[" + i + "]." + fn + "=" + SpriteName(sv));
                            }
                            i++;
                        }
                    }
                }
                Plugin.Log?.LogInfo("endless: --- end infCraftButton dump ---");
            }
            catch (Exception ex) { Trace("dump: " + ex.Message); }
        }

        private static void DumpNode(GameObject go, string path)
        {
            if (go == null) return;
            var comps = go.GetComponents<Component>();
            var names = new List<string>(comps.Length);
            foreach (var c in comps) if (c != null) names.Add(c.GetType().Name);
            Plugin.Log?.LogInfo("endless: node " + path + " [" + string.Join(",", names.ToArray()) + "]");
            var img = go.GetComponent<Image>();
            if (img != null) Plugin.Log?.LogInfo("endless:   Image.sprite=" + SpriteName(img.sprite) + " enabled=" + img.enabled);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
                Plugin.Log?.LogInfo("endless:   TMP text='" + tmp.text + "' font=" + (tmp.font != null ? tmp.font.name : "(none)")
                    + " mat=" + (tmp.fontSharedMaterial != null ? tmp.fontSharedMaterial.name : "(none)")
                    + " size=" + tmp.fontSize + " color=" + tmp.color + " enabled=" + tmp.enabled);
            var t = go.transform;
            for (int i = 0; i < t.childCount; i++) DumpNode(t.GetChild(i).gameObject, path + "/" + t.GetChild(i).name);
        }

        // Применяет выбранный глиф: спрайт → картинкой (TMP-подпись прячем); TMP-стиль →
        // переносим на нашу подпись; ничего не нашли → остаётся фолбэк-подпись.
        private static void ApplyGlyph(LazyButton btn, int key)
        {
            var g = ResolveGlyph();
            if (g == null) return;

            if (g.IsSprite)
            {
                var icon = EnsureIcon(btn, key, g.Sprite);
                if (icon != null && _labels.TryGetValue(key, out var l) && l != null && l.gameObject.activeSelf)
                    l.gameObject.SetActive(false);
                return;
            }

            if (g.IsTmp && _labels.TryGetValue(key, out var label) && label != null)
            {
                label.font = g.Font;
                if (g.FontMat != null) label.fontSharedMaterial = g.FontMat;
                if (g.FontSize > 1f) label.fontSize = Mathf.RoundToInt(g.FontSize);
                label.color = g.Color;
                _labelColors[key] = g.Color;
                if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
                if (_icons.TryGetValue(key, out var old) && old != null)
                {
                    UnityEngine.Object.Destroy(old.gameObject);
                    _icons.Remove(key);
                }
                StretchLabel(label);
            }
        }

        private static Image EnsureIcon(LazyButton btn, int key, Sprite sprite)
        {
            if (_icons.TryGetValue(key, out var cached) && cached != null && cached.transform.parent == btn.transform)
            {
                if (cached.sprite != sprite) cached.sprite = sprite;
                if (!cached.gameObject.activeSelf) cached.gameObject.SetActive(true);
                SizeIcon(cached, btn);
                return cached;
            }

            var rt = UiFactory.Rect("EndlessIcon", btn.transform);
            var icon = rt.gameObject.AddComponent<Image>();
            icon.sprite = sprite;
            icon.color = Color.white;      // фон тинтуется отдельно; иконка всегда светлая
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            SizeIcon(icon, btn);
            _icons[key] = icon;
            return icon;
        }

        // Квадрат по высоте кнопки (~70%), сохраняя пропорции спрайта (preserveAspect).
        private static void SizeIcon(Image icon, LazyButton btn)
        {
            if (icon == null) return;
            var rt = icon.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            var brt = btn != null ? btn.transform as RectTransform : null;
            float h = brt != null ? Mathf.Abs(brt.rect.height) : 0f;
            float s = Mathf.Clamp(h * 0.7f, 12f, 96f);
            rt.sizeDelta = new Vector2(s, s);
        }

        private static TextMeshProUGUI BuildLabel(LazyButton btn, RectTransform srcRect,
            TMP_FontAsset srcFont, Material srcMat, float srcSize, Color srcColor)
        {
            int size = SizeFromRect(srcRect, srcSize);

            var label = UiFactory.Label("Label", btn.transform, EndlessText.Infinity(Plugin.Lang), size,
                TextAlignmentOptions.Center, srcColor);

            // Шрифт+материал — ровно та пара, что рисует ∞ в маркере строки очереди
            // (GameStyle.Font/FontMaterial). Родной шрифт глифа «+» предпочитаем, только
            // если он РЕАЛЬНО умеет U+221E (иначе был блоб от tiny_font).
            var font = PickInfinityFont(srcFont);
            if (font != null)
            {
                label.font = font; // TMP сам подставит материал этого шрифта
                if (font == srcFont && srcMat != null) label.fontSharedMaterial = srcMat;
                else if (font == GameStyle.Font && GameStyle.FontMaterial != null)
                    label.fontSharedMaterial = GameStyle.FontMaterial;
            }

            StretchLabel(label);
            return label;
        }

        // Размер подписи из rect кнопки (как рекомендовано): 0.8·высота, кламп 16..48.
        // Если удалось найти родной глиф «+» и у него осмысленный размер — берём его.
        private static int SizeFromRect(RectTransform rect, float srcSize)
        {
            float h = rect != null ? Mathf.Abs(rect.rect.height) : 0f;
            int byRect = h > 1f ? Mathf.Clamp(Mathf.RoundToInt(h * 0.8f), 16, 48) : 24;
            return srcSize > 1f ? Mathf.RoundToInt(srcSize) : byRect;
        }

        private static void StretchLabel(TextMeshProUGUI label)
        {
            if (label == null) return;
            var rt = label.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(1f, 1f);
            rt.offsetMax = new Vector2(-1f, -1f);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
            label.alignment = TextAlignmentOptions.Center;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
        }

        // Поздняя уборка: удаляет любого ребёнка кнопки, который НЕ наша подпись и НЕ
        // фон (targetGraphic). Возвращает число удалённых.
        private static int CleanForeignChildren(LazyButton btn, int key)
        {
            var parent = btn != null ? btn.transform : null;
            if (parent == null) return 0;
            var keepBg = btn.targetGraphic != null ? btn.targetGraphic.transform : null;
            var keepLabel = _labels.TryGetValue(key, out var l) && l != null ? l.transform : null;
            var keepIcon = _icons.TryGetValue(key, out var ic) && ic != null ? ic.transform : null;
            var victims = new List<Transform>(parent.childCount);
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c == null) continue;
                if (keepLabel != null && (c == keepLabel || keepLabel.IsChildOf(c))) continue;
                if (keepIcon != null && (c == keepIcon || keepIcon.IsChildOf(c))) continue;
                if (keepBg != null && (c == keepBg || keepBg.IsChildOf(c))) continue;
                victims.Add(c);
            }
            foreach (var v in victims) UnityEngine.Object.Destroy(v.gameObject);
            return victims.Count;
        }

        // Поддерживает подпись: размер по актуальной высоте кнопки, растяжение/центр.
        private static void ReassertLabel(LazyButton btn, int key, RectTransform btnRect)
        {
            if (!_labels.TryGetValue(key, out var label) || label == null) return;
            float h = btnRect != null ? Mathf.Abs(btnRect.rect.height) : 0f;
            // Размер из rect — только для фолбэк-подписи; у TMP-глифа берём родной размер.
            if (h > 1f && !(_glyph != null && _glyph.IsTmp))
            {
                int size = Mathf.Clamp(Mathf.RoundToInt(h * 0.8f), 16, 48);
                if (!Mathf.Approximately(label.fontSize, size)) label.fontSize = size;
            }
            StretchLabel(label);
        }

        // Одноразовый диагностический лог (Info) на каждую созданную кнопку: какой
        // спрайт был у источника, какой применили, сколько дочерних визуалов убрали,
        // и итоговый текст/шрифт/геометрия подписи. Чтобы регрессия вида «два глифа»,
        // «чужая иконка» или «блоб вместо ∞» ловилась из лога.
        private static void LogButtonCreated(Sprite srcSprite, Image cloneImg, TextMeshProUGUI label,
            int removed, RectTransform btnRect, int key)
        {
            try
            {
                var lrt = label != null ? label.rectTransform : null;
                var fontName = label != null && label.font != null ? label.font.name : "(none)";
                var rectSize = btnRect != null
                    ? (Mathf.RoundToInt(btnRect.rect.width) + "x" + Mathf.RoundToInt(btnRect.rect.height))
                    : "-";

                string iconSize = "(none)";
                if (_icons.TryGetValue(key, out var icon) && icon != null)
                {
                    var irt = icon.rectTransform;
                    iconSize = Mathf.RoundToInt(irt.rect.width) + "x" + Mathf.RoundToInt(irt.rect.height);
                }

                Plugin.Log?.LogInfo("endless: button created srcSprite=" + SpriteName(srcSprite)
                    + " usedSprite=" + SpriteName(cloneImg != null ? cloneImg.sprite : null)
                    + " removedChildren=" + removed
                    + " buttonRect=" + rectSize
                    + " glyph=" + (_glyphDesc ?? "(none)")
                    + " iconSize=" + iconSize
                    + " font=" + fontName
                    + " label(text='" + (label != null ? label.text : "") + "'"
                    + " size=" + (label != null ? label.fontSize : 0f)
                    + " anchorMin=" + (lrt != null ? lrt.anchorMin.ToString() : "-")
                    + " anchorMax=" + (lrt != null ? lrt.anchorMax.ToString() : "-")
                    + " sizeDelta=" + (lrt != null ? lrt.sizeDelta.ToString() : "-")
                    + " anchoredPos=" + (lrt != null ? lrt.anchoredPosition.ToString() : "-")
                    + " color=" + (label != null ? label.color.ToString() : "-") + ")");
            }
            catch { }
        }

        private static string SpriteName(Sprite s)
            => s == null || string.IsNullOrEmpty(s.name) ? "(none)" : s.name;
    }
}
