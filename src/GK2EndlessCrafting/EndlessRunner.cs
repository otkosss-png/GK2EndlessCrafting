using System;
using System.Collections.Generic;
using UnityEngine;

namespace GK2EndlessCrafting
{
    // Управляет флагом «∞» НА САМОЙ ОЧЕРЕДИ станции — игра уже умеет бесконечный
    // крафт нативно (CraftElementBase.IsInfinite; CraftComponent.Start/Finish пропускают
    // UpdateCountOnFinish для бесконечного элемента). Раннер лишь «дотягивает» этот
    // флаг для станций из реестра: если в очереди есть элементы — пометить их
    // бесконечными; если очередь пуста — поставить один крафт и пометить его.
    // Собственного повтора по таймеру/событиям больше нет.
    internal sealed class EndlessRunner : MonoBehaviour
    {
        private static EndlessRunner _instance;
        private static bool _running = true;
        private float _timer;

        // Проверка смены слота сейва (загрузили другое сохранение) — не чаще раза в секунду.
        private const float SlotCheckSeconds = 1f;
        private float _slotTimer;

        // Станции, по которым уже предупредили об отсутствии CraftComponent.
        private static readonly HashSet<string> _resolveWarned = new HashSet<string>();
        // Последнее время warning'а по ключу — троттлинг повторяющихся исключений.
        private static readonly Dictionary<string, float> _errorWarnedAt = new Dictionary<string, float>();
        private const float ErrorWarnCooldown = 60f;

        internal static void Start()
        {
            if (_instance == null)
            {
                var go = new GameObject("GK2EndlessCrafting");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<EndlessRunner>();
            }
            _running = true;
        }

        internal static void StopIfRunning()
        {
            _running = false;
        }

        private void Awake()
        {
            if (_instance == null) _instance = this;
        }

        private void Update()
        {
            if (_instance == null) _instance = this;
            if (!_running) return;
            if (Plugin.Mod == null || Plugin.Mod.Enabled == null || !Plugin.Mod.Enabled.Value) return;

            // Кнопка «∞»: поздняя уборка чужих глифов (игра создаёт свой «+» лениво,
            // уже после нашей кнопки) и пересчёт размера подписи. Внутри — дешёвый
            // guard (работа только при смене состава детей/высоты), поэтому можно звать
            // каждый кадр, пока открыто окно крафта.
            EndlessButton.SyncAll();

            // Слот сейва проверяем ДО раннего выхода по пустому реестру: именно сюда
            // приходит первичная загрузка файла, когда реестр ещё пуст.
            _slotTimer += Time.unscaledDeltaTime;
            if (_slotTimer >= SlotCheckSeconds)
            {
                _slotTimer = 0f;
                CheckSlot();
            }

            if (Plugin.Registry == null || Plugin.Registry.Count == 0) return;

            float step = Plugin.Mod.PollMs != null
                ? Mathf.Clamp(Plugin.Mod.PollMs.Value, 50, 5000) / 1000f
                : 0.25f;
            _timer += Time.unscaledDeltaTime;
            if (_timer < step) return;
            _timer = 0f;

            // Ensure-проход: для каждой станции реестра включить нативную «∞»
            // (очередь не пуста → пометить элементы; пуста → поставить один крафт).
            foreach (var e in Plugin.Registry.All()) EnsureNative(e.Key);

            // Станции, где «∞» больше не поддерживается (поленницы), но режим был включён
            // раньше: выключаем после обхода — TurnOff меняет реестр.
            if (_toDisable.Count > 0)
            {
                foreach (var id in _toDisable)
                {
                    Plugin.Log?.LogInfo("station " + id + ": endless mode is not supported here, turning it off");
                    EndlessMode.TurnOff(id);
                }
                _toDisable.Clear();
            }
        }

        private static readonly List<string> _toDisable = new List<string>();

        // Смена слота сейва (загрузили другое сохранение) → перечитать файл; нативный
        // флаг для загруженных станций дотягивается обычным Ensure-проходом выше.
        // Plugin.SlotName — источник правды: его же выставляют Plugin.LoadForCurrentSlot/
        // SaveRegistry.
        private static void CheckSlot()
        {
            if (Plugin.Registry == null) return;
            var slot = Plugin.ResolveSlot();
            if (string.IsNullOrEmpty(slot)) return; // MainGame ещё не создан — ждём
            if (string.Equals(slot, Plugin.SlotName, StringComparison.Ordinal)) return;

            Plugin.SlotName = slot;
            SaveSlotStore.Load(Plugin.Registry, slot);
        }

        // Немедленно применить нативный флаг (вызывается из кнопки при включении «∞»).
        internal static void ApplyNow(string stationId) => EnsureNative(stationId);

        // Снять нативный флаг (вызывается из кнопки при выключении «∞»).
        internal static void ClearNative(string stationId)
        {
            try
            {
                var craft = StationKey.Resolve(stationId);
                if (craft == null) return;
                var queue = craft.CraftElementsQueue;
                if (queue == null) return;
                // Снапшот: set_IsInfinite дёргает OnCountChanged, подписчик может изменить очередь.
                foreach (var el in new List<CraftElementBase>(queue))
                    if (el != null) el.IsInfinite = false;
                Trace("station " + stationId + ": infinite off");
            }
            catch (Exception ex)
            {
                WarnThrottled(stationId + "|clear|" + ex.GetType().Name, "clear " + stationId + ": " + Describe(ex));
            }
        }

        // «Ensure»-проход: очередь станции должна быть бесконечной.
        private static void EnsureNative(string stationId)
        {
            if (Plugin.Registry == null || !Plugin.Registry.IsOn(stationId)) return;
            try
            {
                var wgo = StationKey.ResolveWgo(stationId);
                if (wgo == null) { WarnResolveOnce(stationId); return; }
                if (StationKey.IsExcluded(wgo))
                {
                    if (!_toDisable.Contains(stationId)) _toDisable.Add(stationId);
                    return;
                }

                var craft = wgo.CraftComponent;
                if (craft == null) { WarnResolveOnce(stationId); return; }
                _resolveWarned.Remove(stationId); // станция снова доступна

                var queue = craft.CraftElementsQueue;
                if (queue == null) return;

                var recipeId = Plugin.Registry.RecipeFor(stationId);

                if (queue.Count == 0)
                {
                    var recipe = recipeId;
                    var def = FindRecipe(craft, recipe);
                    if (def == null) { Trace("station " + stationId + ": recipe " + recipe + " not found"); return; }

                    // AddToQueue -> GetStartCraftStatus -> CheckWorkerDependentValues дерефает
                    // paramsData.customRes, поэтому paramsData обязателен (fix round 2).
                    var paramsData = StationKey.ParamsFor(stationId) ?? StationKey.BuildParams(wgo, def);
                    if (paramsData == null)
                    {
                        WarnThrottled(stationId + "|params", "station " + stationId + " cannot build CraftParamsData for " + recipe);
                        return;
                    }

                    craft.AddToQueue(new CraftElement(def, paramsData), false, -1);
                    Trace("station " + stationId + ": enqueued " + recipe);
                }

                // Бесконечными помечаем ТОЛЬКО элементы выбранного рецепта — иначе
                // «∞» вешался на все записи очереди станции (жалоба игрока).
                // Снапшот: set_IsInfinite дёргает OnCountChanged, подписчик может изменить очередь.
                foreach (var el in new List<CraftElementBase>(queue))
                    if (el != null && !el.IsInfinite && MatchesRecipe(el, recipeId)) el.IsInfinite = true;
            }
            catch (Exception ex)
            {
                WarnThrottled(stationId + "|" + ex.GetType().Name + "|" + ex.Message,
                    "tick " + stationId + ": " + Describe(ex));
            }
        }

        // Элемент очереди принадлежит выбранному рецепту? Сравниваем по Def.id (и по
        // CraftId на случай, если def ещё не привязан). Пустой recipeId — старое
        // поведение (пометить всё), чтобы режим не «замолчал» на старых записях.
        private static bool MatchesRecipe(CraftElementBase el, string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId)) return true;
            try
            {
                var def = el.Def;
                if (def != null && !string.IsNullOrEmpty(def.id)) return def.id == recipeId;
            }
            catch { }
            try { return el.CraftId == recipeId; }
            catch { return false; }
        }

        private static CraftDefBase FindRecipe(CraftComponent craft, string recipeId)
        {
            if (craft == null || string.IsNullOrEmpty(recipeId)) return null;
            var found = FindIn(craft.AvailableCrafts, recipeId);
            if (found != null) return found;
            return FindIn(craft.CraftsIn, recipeId);
        }

        private static CraftDefBase FindIn(List<CraftDefBase> list, string id)
        {
            if (list == null) return null;
            foreach (var d in list)
                if (d != null && d.id == id) return d;
            return null;
        }

        // Один безусловный warning на станцию: молчаливо сломанная связь (нет
        // CraftComponent) не должна оставлять фичу «просто ничего не делать».
        private static void WarnResolveOnce(string stationId)
        {
            if (string.IsNullOrEmpty(stationId)) return;
            if (!_resolveWarned.Add(stationId)) return;
            Plugin.Log?.LogWarning("endless: station " + stationId
                + " not resolved (no CraftComponent); will keep trying");
        }

        private static void WarnThrottled(string key, string msg)
        {
            float now = Time.unscaledTime;
            if (_errorWarnedAt.TryGetValue(key, out var t) && now - t < ErrorWarnCooldown) return;
            _errorWarnedAt[key] = now;
            Plugin.Log?.LogWarning("endless: " + msg + " (repeats suppressed for " + (int)ErrorWarnCooldown + "s)");
        }

        // Полный текст исключения: тип, message, стек; плюс InnerException.
        private static string Describe(Exception ex)
        {
            if (ex == null) return "(null)";
            var text = ex.ToString();
            if (ex.InnerException != null)
                text += " | INNER: " + ex.InnerException.ToString();
            return text;
        }

        private static void Trace(string msg)
        {
            if (Plugin.Mod != null && Plugin.Mod.DebugLog != null && Plugin.Mod.DebugLog.Value)
                Plugin.Log?.LogInfo("endless: " + msg);
        }
    }
}
