# GK2 Endless Crafting — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Кнопка «∞» в окне крафта: станция сама бесконечно повторяет выбранный рецепт, режим помнится по станции и переживает перезапуск игры.

**Architecture:** BepInEx-плагин + Harmony. Чистое состояние («какие станции в режиме ∞ и какой рецепт») и его файловая сериализация живут в `GK2EndlessCrafting.Core` и покрыты тестами; плагин добавляет кнопку в окно крафта и в фоне ставит рецепт заново, когда очередь станции пустеет.

**Tech Stack:** C# (netstandard2.0 ядро, netstandard2.1 плагин, net8.0 xUnit), BepInEx 5.4.23.5, 0Harmony, GK2 Mod Framework, Unity 6 (`Assembly-CSharp.dll`), TMP.

**Spec:** `docs/superpowers/specs/2026-09-27-gk2-endless-crafting-design.md`

## Global Constraints

- SDK: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe"`; сборка/тесты из корня репо: `build -c Release`, `test -c Release`.
- Игра: `E:\SteamLibrary\steamapps\common\Graveyard Keeper 2`.
- Деплой: закрыть `GraveyardKeeper2` → DLL в `<GAME>\BepInEx\plugins\GK2EndlessCrafting\` → удалить `<GAME>\BepInEx\cache` → `Start-Process "steam://rungameid/4358690"`.
- НИКОГДА не писать кириллицу через PowerShell — только инструменты write/edit.
- Коммиты: `git -c user.name="otkosss-png" -c user.email="otkosss-png@users.noreply.github.com"`.
- Core — чистый netstandard2.0 без Unity/BepInEx.
- Лимит очереди игры (999) не трогаем: ставим по одному крафту по мере освобождения очереди.
- Риск-факты из спеки: `UIBaseCraftSelectionWindowData.SetCraftCount` без клампа; `AddCraftsCount` клампится 1..999; `CraftComponent.TryStartCraft(CraftElementBase)` / `AddCraftNoStart` / `HasCraftsInQueue`.

---

### Task 1: Ядро — реестр и хранилище (TDD)

**Files:**
- Create: `src/GK2EndlessCrafting.Core/GK2EndlessCrafting.Core.csproj`
- Create: `src/GK2EndlessCrafting.Core/EndlessRegistry.cs`
- Create: `src/GK2EndlessCrafting.Core/EndlessStore.cs`
- Create: `src/GK2EndlessCrafting.Core/EndlessText.cs`
- Create: `tests/GK2EndlessCrafting.Core.Tests/GK2EndlessCrafting.Core.Tests.csproj`
- Create: `tests/GK2EndlessCrafting.Core.Tests/RegistryTests.cs`
- Create: `tests/GK2EndlessCrafting.Core.Tests/StoreTests.cs`
- Create: `GK2EndlessCrafting.sln`, `.gitignore`

**Interfaces:**
- Produces: `EndlessRegistry` (`Set(stationId, recipeId)`, `Clear(stationId)`, `IsOn(stationId)`, `RecipeFor(stationId)`, `Count`, `All()`), `EndlessStore.Serialize(reg)` / `EndlessStore.Load(reg, text)`, `EndlessText.Infinity`, `EndlessText.HintOn/Off`.

- [ ] **Step 1: Create the Core project**

`src/GK2EndlessCrafting.Core/GK2EndlessCrafting.Core.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <AssemblyName>GK2EndlessCrafting.Core</AssemblyName>
    <RootNamespace>GK2EndlessCrafting.Core</RootNamespace>
    <Version>1.0.0</Version>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Create the test project**

`tests/GK2EndlessCrafting.Core.Tests/GK2EndlessCrafting.Core.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\GK2EndlessCrafting.Core\GK2EndlessCrafting.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Write the failing tests**

`tests/GK2EndlessCrafting.Core.Tests/RegistryTests.cs`:
```csharp
using GK2EndlessCrafting.Core;
using Xunit;

public class RegistryTests
{
    [Fact]
    public void Set_then_IsOn_and_RecipeFor()
    {
        var r = new EndlessRegistry();
        r.Set("chest_1", "recipe_nail");
        Assert.True(r.IsOn("chest_1"));
        Assert.Equal("recipe_nail", r.RecipeFor("chest_1"));
    }

    [Fact]
    public void Set_again_replaces_recipe()
    {
        var r = new EndlessRegistry();
        r.Set("w1", "a");
        r.Set("w1", "b");
        Assert.Equal("b", r.RecipeFor("w1"));
        Assert.Equal(1, r.Count);
    }

    [Fact]
    public void Clear_turns_off()
    {
        var r = new EndlessRegistry();
        r.Set("w1", "a");
        r.Clear("w1");
        Assert.False(r.IsOn("w1"));
        Assert.Null(r.RecipeFor("w1"));
        Assert.Equal(0, r.Count);
    }

    [Fact]
    public void Empty_ids_are_ignored()
    {
        var r = new EndlessRegistry();
        r.Set("", "a");
        r.Set("w1", "");
        Assert.Equal(0, r.Count);
        Assert.False(r.IsOn(""));
        Assert.Null(r.RecipeFor("w1"));
    }

    [Fact]
    public void All_lists_entries()
    {
        var r = new EndlessRegistry();
        r.Set("w1", "a");
        r.Set("w2", "b");
        Assert.Equal(2, System.Linq.Enumerable.Count(r.All()));
    }
}
```

`tests/GK2EndlessCrafting.Core.Tests/StoreTests.cs`:
```csharp
using GK2EndlessCrafting.Core;
using Xunit;

public class StoreTests
{
    [Fact]
    public void Round_trip_keeps_entries()
    {
        var a = new EndlessRegistry();
        a.Set("w1", "recipe_a");
        a.Set("w2", "recipe_b");

        var text = EndlessStore.Serialize(a);
        var b = new EndlessRegistry();
        EndlessStore.Load(b, text);

        Assert.True(b.IsOn("w1"));
        Assert.Equal("recipe_b", b.RecipeFor("w2"));
        Assert.Equal(2, b.Count);
    }

    [Fact]
    public void Load_of_empty_text_clears()
    {
        var r = new EndlessRegistry();
        r.Set("w1", "a");
        EndlessStore.Load(r, "");
        Assert.Equal(0, r.Count);
    }

    [Fact]
    public void Bad_lines_are_ignored()
    {
        var r = new EndlessRegistry();
        EndlessStore.Load(r, "# comment\nno-separator\nw1|ok\n\nw2|\n|recipe");
        Assert.True(r.IsOn("w1"));
        Assert.Equal("ok", r.RecipeFor("w1"));
        Assert.Equal(1, r.Count);
    }

    [Fact]
    public void Serialize_is_stable_for_same_content()
    {
        var r = new EndlessRegistry();
        r.Set("w1", "a");
        Assert.Equal(EndlessStore.Serialize(r), EndlessStore.Serialize(r));
    }
}
```

- [ ] **Step 4: Create the solution, add projects, run tests → they fail**

```powershell
& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" new sln -n GK2EndlessCrafting
& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" sln GK2EndlessCrafting.sln add src\GK2EndlessCrafting.Core\GK2EndlessCrafting.Core.csproj
& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" sln GK2EndlessCrafting.sln add tests\GK2EndlessCrafting.Core.Tests\GK2EndlessCrafting.Core.Tests.csproj
& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release
```
Expected: FAIL — `CS0246` (EndlessRegistry/EndlessStore не найдены).

`.gitignore`:
```
bin/
obj/
dist/
*.user
.superpowers/
```

- [ ] **Step 5: Implement the Core types**

`src/GK2EndlessCrafting.Core/EndlessRegistry.cs`:
```csharp
using System.Collections.Generic;

namespace GK2EndlessCrafting.Core
{
    // Какие станции в режиме «∞» и какой рецепт повторять. Без Unity.
    public sealed class EndlessRegistry
    {
        private readonly Dictionary<string, string> _map = new Dictionary<string, string>();

        public int Count => _map.Count;

        public void Set(string stationId, string recipeId)
        {
            if (string.IsNullOrEmpty(stationId) || string.IsNullOrEmpty(recipeId)) return;
            _map[stationId] = recipeId;
        }

        public void Clear(string stationId)
        {
            if (!string.IsNullOrEmpty(stationId)) _map.Remove(stationId);
        }

        public bool IsOn(string stationId)
            => !string.IsNullOrEmpty(stationId) && _map.ContainsKey(stationId);

        public string RecipeFor(string stationId)
            => !string.IsNullOrEmpty(stationId) && _map.TryGetValue(stationId, out var r) ? r : null;

        public IEnumerable<KeyValuePair<string, string>> All() => _map;
    }
}
```

`src/GK2EndlessCrafting.Core/EndlessStore.cs`:
```csharp
using System.Collections.Generic;
using System.Text;

namespace GK2EndlessCrafting.Core
{
    // Простой построчный формат: "stationId|recipeId" (см. TrustStore в GK2ModInstaller).
    public static class EndlessStore
    {
        public static string Serialize(EndlessRegistry registry)
        {
            var sb = new StringBuilder();
            if (registry == null) return string.Empty;
            var keys = new List<string>();
            var map = new Dictionary<string, string>();
            foreach (var e in registry.All()) { keys.Add(e.Key); map[e.Key] = e.Value; }
            keys.Sort(System.StringComparer.Ordinal);
            foreach (var k in keys)
            {
                if (string.IsNullOrEmpty(k) || string.IsNullOrEmpty(map[k])) continue;
                sb.Append(k).Append('|').Append(map[k]).Append('\n');
            }
            return sb.ToString();
        }

        public static void Load(EndlessRegistry registry, string text)
        {
            if (registry == null) return;
            Clear(registry);
            if (string.IsNullOrEmpty(text)) return;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var i = line.IndexOf('|');
                if (i <= 0 || i >= line.Length - 1) continue;
                registry.Set(line.Substring(0, i).Trim(), line.Substring(i + 1).Trim());
            }
        }

        private static void Clear(EndlessRegistry registry)
        {
            var keys = new List<string>();
            foreach (var e in registry.All()) keys.Add(e.Key);
            foreach (var k in keys) registry.Clear(k);
        }
    }
}
```

`src/GK2EndlessCrafting.Core/EndlessText.cs`:
```csharp
namespace GK2EndlessCrafting.Core
{
    public enum Lang { En, Ru }

    public static class EndlessText
    {
        public static string Infinity(Lang lang) => "∞";

        public static string HintOn(Lang lang)
            => lang == Lang.Ru ? "Бесконечный крафт: включён" : "Endless crafting: on";

        public static string HintOff(Lang lang)
            => lang == Lang.Ru ? "Бесконечный крафт: выключен" : "Endless crafting: off";
    }
}
```

- [ ] **Step 6: Run tests → pass, commit**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release`
Expected: PASS — 9 tests.
```bash
git add . && git -c user.name="otkosss-png" -c user.email="otkosss-png@users.noreply.github.com" commit -m "feat(core): endless registry + store with tests"
```

---

### Task 2: Плагин-каркас + разведка окна крафта

**Files:**
- Create: `src/GK2EndlessCrafting/GK2EndlessCrafting.csproj`, `Plugin.cs`, `Mod.cs`, `tools/deploy.ps1`
- Modify: `GK2EndlessCrafting.sln`

**Interfaces:**
- Produces: `Plugin.Log`, `Plugin.Mod`, `Plugin.Lang`; `Mod.Enabled`, `Mod.PollSeconds`, `Mod.Language`, `Mod.DebugLog`.

- [ ] **Step 1: Create the plugin project** (как в GK2ExtractAll, заменить имена на `GK2EndlessCrafting`; `netstandard2.1`; `<Compile Include="..\GK2EndlessCrafting.Core\*.cs" LinkBase="Core" />`; ссылки на BepInEx/0Harmony/GK2.Framework/Assembly-CSharp/LazyBearTechnology/UnityEngine*/Unity.TextMeshPro из `$(GameDir)`).

- [ ] **Step 2: Add to the solution**

```powershell
& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" sln GK2EndlessCrafting.sln add src\GK2EndlessCrafting\GK2EndlessCrafting.csproj
```

- [ ] **Step 3: Implement `Mod` + `Plugin`** (как в GK2ExtractAll): GUID `otkosss.gk2.endlesscrafting`, версия 1.0.0, `[BepInDependency("ru.superman4eg.gk2.framework")]`, try/catch вокруг `FrameworkApi.RegisterMod`, `EnsureSettings` с фолбэком `Config.Bind`, `PatchAll`, лог `GK2 Endless Crafting <ver> loaded.`.
  Настройки: `Enabled` (bool, true), `PollSeconds` (int 1..10, дефолт 1), `Language` (auto/en/ru), `DebugLog` (bool, false).

- [ ] **Step 4: Recon — где окно крафта и как поставить крафт** (обязательный шаг, результат — в отчёт)

```powershell
# дамп IL: Init/OnEnable/Redraw окна выбора крафта + OnPressPlusQueue, чтобы понять
# точку вставки кнопки и поле кнопки "+"
```
Cecil-дамп `UIBaseCraftSelectionWindow` (методы `Init`, `OnEnable`, `Redraw`, `OnPressPlusQueue`,
`UpdateCraftCountElements`) и `UICraftSelectionWindow.OnAddToQueuePressed` — выписать в отчёт:
имена полей кнопок «+»/«−», методы, вызываемые при построении UI, и точный вызов, которым
`OnAddToQueuePressed` кладёт крафт в очередь (какой метод `CraftComponent` он дёргает).
Expected: в отчёте есть конкретные имена (например, поле `plusButton`, вызов `craftComponent.AddCraftNoStart(...)`).

- [ ] **Step 5: Build + deploy + verify load**

Run: `& "<dotnet>" build -c Release` → 0 ошибок; `powershell -ExecutionPolicy Bypass -File tools\deploy.ps1`;
в `<GAME>\BepInEx\LogOutput.log` есть `GK2 Endless Crafting 1.0.0.0 loaded.`

- [ ] **Step 6: Commit** `feat(plugin): bootstrap endless crafting plugin`

---

### Task 3: Кнопка «∞» в окне крафта

**Files:**
- Create: `src/GK2EndlessCrafting/CraftWindowPatch.cs`, `EndlessButton.cs`
- (копия UI-хелперов `UiFactory.cs`/`GameStyle.cs` из GK2ExtractAll, namespace → `GK2EndlessCrafting`)

**Interfaces:**
- Consumes: имена из Step 4 Task 2.
- Produces: `EndlessButton.Ensure(UIBaseCraftSelectionWindow window)`, `EndlessButton.Clear()`, `EndlessButton.Sync(window)`.

- [ ] **Step 1: Copy UI helpers** из `C:\Users\Проньки\Documents\OpenCode\GK2ExtractAll\src\GK2ExtractAll\{UiFactory.cs,GameStyle.cs}` (заменить namespace).

- [ ] **Step 2: Patch** — `[HarmonyPostfix]` на найденном в Step 4 методе построения окна → `EndlessButton.Ensure(__instance)`; `[HarmonyPatch(typeof(UIBaseCraftSelectionWindow), "Hide")]` postfix → `EndlessButton.Clear()`.

- [ ] **Step 3: Button** — клон игровой кнопки «+» (`LazyButton`), текст заменяем на `EndlessText.Infinity(Plugin.Lang)`, якорь — справа от «+»; клик → `CraftWindowPatch.Toggle(window)`:
  `stationId = StationKey.Of(window)`; если реестр уже включён — `Clear`, иначе `Set(stationId, RecipeKey.Current(window))`; сохранить в файл слота; визуально подсветить состояние (цвет кнопки).

- [ ] **Step 4: Verify in game** — кнопка видна, клик переключает состояние и пишет файл `BepInEx\config\GK2EndlessCrafting\<слот>.json` (проверить содержимое).

- [ ] **Step 5: Commit** `feat(ui): endless toggle button in the craft window`

---

### Task 4: Раннер — очередь пустеет → ставим рецепт снова

**Files:**
- Create: `src/GK2EndlessCrafting/EndlessRunner.cs`, `StationKey.cs`

**Interfaces:**
- Consumes: `EndlessRegistry`, `EndlessStore`, имя метода постановки в очередь (Step 4 Task 2).
- Produces: `EndlessRunner.Start/StopIfRunning`; `StationKey.Of(window)` → string; `StationKey.Resolve(stationId)` → `CraftComponent`.

- [ ] **Step 1: `StationKey`** — id станции из `WgoData` окна (`uniqueId`/`id`), резолв обратно в `CraftComponent` по тому же id через `Components`/`WorldZone` игры; если id нет — использовать координаты/индекс как fallback (записать в отчёт, что взяли).

- [ ] **Step 2: `EndlessRunner`** (MonoBehaviour, опрос раз в `PollSeconds`):
```
для каждой записи реестра:
    craft = StationKey.Resolve(stationId)
    если craft != null и !craft.HasCraftsInQueue и рецепт ещё существует:
        поставить крафт (метод из Step 4 Task 2; при неудаче — просто пропустить, режим не выключать)
```
все вызовы в try/catch + лог под `Mod.DebugLog`.

- [ ] **Step 3: Verify in game** — включить «∞» на станции, дождаться конца очереди: крафт продолжается сам; выключить ингредиенты — станция стоит; вернуть — продолжает.

- [ ] **Step 4: Commit** `feat: endless runner re-queues the recipe when the station is idle`

---

### Task 5: Память по станции + настройки

**Files:**
- Modify: `src/GK2EndlessCrafting/Plugin.cs`, `EndlessRunner.cs`, `EndlessButton.cs`
- Create: `src/GK2EndlessCrafting/SaveSlotStore.cs`

- [ ] **Step 1: Слот сейва** — определить текущий слот (как `GK2 Shopping List`: `C:/Users/<user>/AppData/LocalLow/Lazy Bear Games/Graveyard Keeper 2/ShoppingList/<slot>.json`) — если получить слот не выйдет, использовать один файл `default.json`. Путь: `BepInEx\config\GK2EndlessCrafting\<slot>.json`, кодировка UTF-8 без BOM, запись после каждого переключения.
- [ ] **Step 2: Загрузка** — при старте раннера (и при смене слота) читать файл в `EndlessRegistry`; битый файл → пустой реестр + warning, файл не затирать.
- [ ] **Step 3: Verify in game** — включить «∞», выйти в меню/перезапустить игру, загрузить сейв: режим восстановлен и станция снова крафтит.
- [ ] **Step 4: Commit** `feat: persist endless stations per save slot`

---

### Task 6: README, описание, публикация

- [ ] **Step 1: README.md** (EN) + `E:\GK2Upload\nexus\GK2EndlessCrafting_bbcode.txt` (EN) — по шаблону Extract All/Zombie HQ: что делает, «включил и забыл», настройки, требования (BepInEx + GK2 Mod Framework), GitHub-ссылка; без Steam-ссылок в Нексус-версии.
- [ ] **Step 2: Превью** 512×512 в стиле остальных (`System.Drawing`-скрипт, ASCII-текст) + реальный скрин окна станции с кнопкой «∞» (≤2 МБ!).
- [ ] **Step 3: Steam Workshop** — `--create --folder E:\GK2Upload\GK2EndlessCrafting --title "GK2 Endless Crafting - stations repeat the recipe forever" --desc-file … --public`, затем два отдельных `--update` (превью, скрин).
- [ ] **Step 4: GitHub** — `git remote add origin https://github.com/otkosss-png/GK2EndlessCrafting.git`, push, zip `BepInEx/plugins/GK2EndlessCrafting/{dll,README.txt}`, `gh release create v1.0.0 …`.
- [ ] **Step 5: Nexus-комплект** — `E:\GK2Upload\nexus\` (bbcode, `_fields` строка, zip) + ручная заливка владельцем.
- [ ] **Step 6: Commit** `docs: README + release assets`

---

## Self-Review

- **Spec coverage:** Р1 (кнопка «∞» в окне) → Task 3; Р2 (автоповтор) → Task 4; Р3 (ждать и продолжать)
  → Task 4 Step 2 (нет крафта/ошибка → пропуск, режим не выключаем); Р4 (память по станции) → Task 5;
  Р5 (упаковка/публикация) → Task 6; Р6 (геймпад) → Task 3 (клон игровой `LazyButton`).
- **Placeholder scan:** все шаги с кодом или точной командой; неизвестные имена игры закрыты
  обязательным recon-шагом (Task 2 Step 4) с конкретным списком типов/методов для дампа.
- **Type consistency:** `EndlessRegistry` API (Set/Clear/IsOn/RecipeFor/Count/All) одинаков в Task 1
  и Task 3-5; `EndlessStore.Serialize/Load`, `EndlessText.*`, `StationKey.Of/Resolve`,
  `EndlessButton.Ensure/Clear/Sync`, `EndlessRunner.Start/StopIfRunning` согласованы по задачам.
- **Открытый риск:** конкретные имена игры (`plusButton`, метод постановки в очередь, поле станции
  у окна) выясняются в Task 2 Step 4 и переносятся в Task 3-4; если рекон покажет, что кнопку
  вставить некуда — правка дизайна (например, горячая клавиша) согласуется с владельцем.
