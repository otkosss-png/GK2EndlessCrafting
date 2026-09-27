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
                LogButtonCreated(srcSprite, cloneImg, label, removed, rt);
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
            var victims = new List<Transform>(parent.childCount);
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c == null) continue;
                if (keepLabel != null && (c == keepLabel || keepLabel.IsChildOf(c))) continue;
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
            if (h > 1f)
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
            int removed, RectTransform btnRect)
        {
            try
            {
                var lrt = label != null ? label.rectTransform : null;
                var fontName = label != null && label.font != null ? label.font.name : "(none)";
                var rectSize = btnRect != null
                    ? (Mathf.RoundToInt(btnRect.rect.width) + "x" + Mathf.RoundToInt(btnRect.rect.height))
                    : "-";
                Plugin.Log?.LogInfo("endless: button created srcSprite=" + SpriteName(srcSprite)
                    + " usedSprite=" + SpriteName(cloneImg != null ? cloneImg.sprite : null)
                    + " removedChildren=" + removed
                    + " buttonRect=" + rectSize
                    + " label(text='" + (label != null ? label.text : "") + "'"
                    + " font=" + fontName
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
