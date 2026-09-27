using System;
using System.Collections.Generic;
using UnityEngine;

namespace GK2EndlessCrafting
{
    // Раз в Mod.PollSeconds проходит по станциям в режиме «∞» и, если очередь
    // пуста, ставит сохранённый рецепт снова. Ошибки не выключают режим.
    internal sealed class EndlessRunner : MonoBehaviour
    {
        private static EndlessRunner _instance;
        private static bool _running = true;
        private float _timer;

        // Станции, по которым уже предупредили об отсутствии CraftComponent
        // (один warning на станцию, без спама каждый опрос).
        private static readonly HashSet<string> _resolveWarned = new HashSet<string>();
        // Последнее время warning'а по ключу (станция или "__tick__") — троттлинг
        // повторяющихся исключений, чтобы сломанная станция не заливала лог.
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
            if (Plugin.Registry == null || Plugin.Registry.Count == 0) return;

            float step = Plugin.Mod.PollSeconds != null ? Mathf.Max(0.25f, Plugin.Mod.PollSeconds.Value) : 1f;
            _timer += Time.unscaledDeltaTime;
            if (_timer < step) return;
            _timer = 0f;
            Tick();
        }

        private static void Tick()
        {
            try
            {
                foreach (var e in Plugin.Registry.All())
                {
                    CraftComponent craft;
                    try
                    {
                        craft = StationKey.Resolve(e.Key);
                    }
                    catch (Exception ex)
                    {
                        WarnThrottled(e.Key, "resolve " + e.Key + ": " + ex.Message);
                        continue;
                    }

                    if (craft == null)
                    {
                        WarnResolveOnce(e.Key);
                        continue;
                    }
                    _resolveWarned.Remove(e.Key); // станция снова доступна

                    try
                    {
                        if (craft.HasCraftsInQueue) continue;

                        var def = FindRecipe(craft, e.Value);
                        if (def == null) { Trace("station " + e.Key + ": recipe " + e.Value + " not found"); continue; }

                        craft.AddToQueue(new CraftElement(def), false, -1);
                        Trace("station " + e.Key + ": re-queued " + e.Value);
                    }
                    catch (Exception ex)
                    {
                        WarnThrottled(e.Key, "tick " + e.Key + ": " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                WarnThrottled("__tick__", "tick: " + ex.Message);
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

        private static void Trace(string msg)
        {
            if (Plugin.Mod != null && Plugin.Mod.DebugLog != null && Plugin.Mod.DebugLog.Value)
                Plugin.Log?.LogInfo("endless: " + msg);
        }
    }
}
