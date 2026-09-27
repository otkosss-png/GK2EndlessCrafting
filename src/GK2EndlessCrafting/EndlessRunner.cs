using System;
using System.Collections.Generic;
using UnityEngine;

namespace GK2EndlessCrafting
{
    // Держит очередь станций в режиме «∞» заполненной: подписывается на игровые
    // OnCraftFinish/OnCraftRemovedFromQueue (чтобы долить крафт в тот же момент,
    // как очередь опустела) и дополнительно опрашивает станции по таймеру.
    internal sealed class EndlessRunner : MonoBehaviour
    {
        // Сколько крафтов держим в очереди станции. 1 = «доливаем, как только
        // очередь опустела». Жёсткий потолок — 2 (MAX_QUEUE_CRAFTS 999 не трогаем).
        // Если в игре всё ещё виден зазор между крафтами — поднять до 2.
        private const int QueueBuffer = 1;

        private static EndlessRunner _instance;
        private static bool _running = true;
        private float _timer;

        // Станции, по событиям которых надо немедленно долить крафт (следующий кадр).
        private static readonly HashSet<string> _dirty = new HashSet<string>();
        // Активные подписки на CraftComponent по stationId.
        private static readonly Dictionary<string, Subscription> _subs = new Dictionary<string, Subscription>();
        // Станции, по которым уже предупредили об отсутствии CraftComponent.
        private static readonly HashSet<string> _resolveWarned = new HashSet<string>();
        // Последнее время warning'а по ключу — троттлинг повторяющихся исключений.
        private static readonly Dictionary<string, float> _errorWarnedAt = new Dictionary<string, float>();
        private const float ErrorWarnCooldown = 60f;

        private sealed class Subscription
        {
            internal CraftComponent Component;
            internal Action OnFinish;
            internal CraftComponent.DelCraftRemovedFromQueue OnRemoved;
        }

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

        private void OnDestroy()
        {
            UnsubscribeAll();
        }

        private void Update()
        {
            if (_instance == null) _instance = this;
            if (!_running) return;
            if (Plugin.Mod == null || Plugin.Mod.Enabled == null || !Plugin.Mod.Enabled.Value) return;

            if (Plugin.Registry == null || Plugin.Registry.Count == 0)
            {
                if (_subs.Count > 0) UnsubscribeAll();
                return;
            }

            // Событийные доливы — каждый кадр (мгновенно после OnCraftFinish/OnCraftRemovedFromQueue).
            if (_dirty.Count > 0) ProcessDirty();

            float step = Plugin.Mod.PollMs != null
                ? Mathf.Clamp(Plugin.Mod.PollMs.Value, 50, 5000) / 1000f
                : 0.25f;
            _timer += Time.unscaledDeltaTime;
            if (_timer < step) return;
            _timer = 0f;

            // Резервная сетка: синхронизировать подписки и долить все станции.
            SyncSubscriptions();
            TopUpAll();
        }

        private static void ProcessDirty()
        {
            var ids = new List<string>(_dirty);
            _dirty.Clear();
            foreach (var id in ids) TopUp(id);
        }

        private static void SyncSubscriptions()
        {
            // Станции, которых больше нет в реестре (∞ выключен) — отписаться.
            foreach (var id in new List<string>(_subs.Keys))
                if (Plugin.Registry.RecipeFor(id) == null) Unsubscribe(id);

            foreach (var e in Plugin.Registry.All())
            {
                var wgo = StationKey.ResolveWgo(e.Key);
                var comp = wgo != null ? wgo.CraftComponent : null;
                if (comp == null) { Unsubscribe(e.Key); continue; } // резолв не удался — не держим подписку
                if (_subs.TryGetValue(e.Key, out var s) && s.Component == comp) continue; // уже подписаны
                Unsubscribe(e.Key);
                Subscribe(e.Key, comp);
            }
        }

        private static void Subscribe(string id, CraftComponent comp)
        {
            try
            {
                var onFinish = new Action(() => MarkDirty(id));
                CraftComponent.DelCraftRemovedFromQueue onRemoved = element => MarkDirty(id);
                comp.OnCraftFinish += onFinish;
                comp.OnCraftRemovedFromQueue += onRemoved;
                _subs[id] = new Subscription { Component = comp, OnFinish = onFinish, OnRemoved = onRemoved };
                Trace("station " + id + ": subscribed");
            }
            catch (Exception ex)
            {
                WarnThrottled("sub|" + id, "subscribe " + id + ": " + Describe(ex));
            }
        }

        private static void Unsubscribe(string id)
        {
            if (!_subs.TryGetValue(id, out var s)) return;
            _subs.Remove(id);
            _dirty.Remove(id);
            if (s.Component == null) return;
            try
            {
                if (s.OnFinish != null) s.Component.OnCraftFinish -= s.OnFinish;
                if (s.OnRemoved != null) s.Component.OnCraftRemovedFromQueue -= s.OnRemoved;
                Trace("station " + id + ": unsubscribed");
            }
            catch (Exception ex)
            {
                Trace("unsubscribe " + id + ": " + ex.Message);
            }
        }

        private static void UnsubscribeAll()
        {
            foreach (var id in new List<string>(_subs.Keys)) Unsubscribe(id);
        }

        private static void MarkDirty(string id)
        {
            if (!string.IsNullOrEmpty(id)) _dirty.Add(id);
        }

        private static void TopUpAll()
        {
            foreach (var e in Plugin.Registry.All()) TopUp(e.Key);
        }

        // Доливает ОДИН крафт, если очередь короче буфера. Режим не выключается при ошибках.
        private static void TopUp(string id)
        {
            if (Plugin.Registry == null || !Plugin.Registry.IsOn(id)) return;
            try
            {
                var wgo = StationKey.ResolveWgo(id);
                if (wgo == null) { WarnResolveOnce(id); return; }

                var craft = wgo.CraftComponent;
                if (craft == null) { WarnResolveOnce(id); return; }
                _resolveWarned.Remove(id); // станция снова доступна

                var queue = craft.CraftElementsQueue;
                int count = queue != null ? queue.Count : 0;
                if (count >= QueueBuffer) return;

                var recipe = Plugin.Registry.RecipeFor(id);
                var def = FindRecipe(craft, recipe);
                if (def == null) { Trace("station " + id + ": recipe " + recipe + " not found"); return; }

                // AddToQueue -> GetStartCraftStatus -> CheckWorkerDependentValues дерефает
                // paramsData.customRes, поэтому paramsData обязателен (см. fix round 2).
                var paramsData = StationKey.ParamsFor(id) ?? StationKey.BuildParams(wgo, def);
                if (paramsData == null)
                {
                    WarnThrottled(id + "|params", "station " + id + " cannot build CraftParamsData for " + recipe);
                    return;
                }

                craft.AddToQueue(new CraftElement(def, paramsData), false, -1);
                Trace("station " + id + ": re-queued " + recipe + " (queue was " + count + ")");
            }
            catch (Exception ex)
            {
                WarnThrottled(id + "|" + ex.GetType().Name + "|" + ex.Message, "tick " + id + ": " + Describe(ex));
            }
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
