# GK2 Endless Crafting — дизайн

Дата: 2026-09-27
Игра: Graveyard Keeper 2 (Steam, appid 4358690)
Статус: утверждён к реализации

## 1. Цель

Дать «включил и забыл»: в окне крафта станции кнопка **«∞»** — станция сама бесконечно
повторяет выбранный рецепт, пока игрок не выключит режим. Не нужно ни тыкать «+» сотни раз,
ни возвращаться к станции.

## 2. Область

В области:
- кнопка-тумблер «∞» в окне крафта (любая станция/конвейер, где есть выбор рецепта);
- автоповтор: очередь пустеет → мод ставит тот же рецепт снова;
- если ингредиентов/условий нет — молча ждём, при появлении продолжаем;
- память по станции: режим и рецепт переживают выход из игры и загрузку сейва;
- настройки в меню Mods, локализация en/ru/auto;
- публикация: Workshop-айтем + GitHub Release (+ комплект для Nexus).

Вне области:
- изменение лимита очереди (`MAX_QUEUE_CRAFTS` = 999 не трогаем);
- автоматическая добыча/логистика ингредиентов;
- «бесконечность» для станций без выбора рецепта (если таких не окажется — кнопки там нет);
- синхронизация режима между сохранениями разных слотов.

## 3. Требования

- **Р1.** Включение — **кнопка «∞» в окне станции** (не глобальный тумблер, не число).
- **Р2.** Пока режим включён, станция **повторяет выбранный рецепт** сама, без участия игрока.
- **Р3.** Нет ресурсов/условий — **не выключать режим**, ждать и продолжить автоматически.
- **Р4.** Состояние **запоминается по станции** и переживает перезапуск игры.
- **Р5.** Оформление как у остальных модов: отдельный репо, настройки в меню Mods,
  публикация в Workshop + GitHub (+ комплект для Nexus).
- **Р6.** Работает и с геймпада (кнопка клонируется с игровой, попадает в навигацию).

## 4. Разведка (проверено по Assembly-CSharp)

- `UIBaseCraftSelectionWindow`: `OnPressPlusQueue()`, `ChangeCraftCount(int)`,
  `OnAddToQueuePressed()`, `UpdateCraftCountElements()`, `UpdateCounters()`, `OnDpadDownPressed()`.
- `UIBaseCraftSelectionWindowData`: `CraftsCount` (get/set, **без клампа**), `SetCraftCount(int)`,
  `AddCraftsCount(int)` (клампится `MinCraftsCount`=1..`MaxCraftsCount`=999), `OnPlus()`, `OnMinus()`,
  `CraftQueue`.
- `CraftComponent`: `MAX_QUEUE_CRAFTS=999`, `MIN_QUEUE_CRAFTS=1`, `craftElementsQueue`,
  `HasCraftsInQueue`, `IsAutoCraftable`, `CurrentCraftElement`, `LastStartedCraftWithRequirements`,
  `TryStartCraft(CraftElementBase)`, `AddCraftNoStart(CraftElementBase)`, `TryStartCurCraft()`,
  `TryFinishCurCraft()`, `OnCraftFinish`, `OnCraftAddedToQueue/OnCraftRemovedFromQueue`,
  `TryDropConveyorWorkbenchCraftInventory()`.

Вывод: поставить крафт в очередь программно можно (`TryStartCraft` / `AddCraftNoStart`);
«+» ограничен 999, но нам это не нужно — мы добавляем по одному по мере освобождения очереди.

## 5. Архитектура

### 5.1 Репозиторий (по шаблону GK2ZombieHQ / GK2ExtractAll)

```
GK2EndlessCrafting/
  src/GK2EndlessCrafting.Core/        netstandard2.0 — чистая логика, тесты
  src/GK2EndlessCrafting/             netstandard2.1 — BepInEx-плагин (Harmony, UI, игра)
  tests/GK2EndlessCrafting.Core.Tests/ net8.0 xUnit
  docs/superpowers/…
```

Ссылки на DLL игры из `$(GameDir)`; `GK2.Framework.dll` — `<Private>false</Private>`;
`[BepInDependency("ru.superman4eg.gk2.framework")]`.

### 5.2 Компоненты

- **`EndlessRegistry` (Core).** Состояние «какие станции в режиме ∞ и какой рецепт повторять»:
  `Set(stationId, recipeId)`, `Clear(stationId)`, `IsOn(stationId)`, `RecipeFor(stationId)`,
  `All()`. Не знает про Unity.
- **`EndlessStore` (Core).** Сериализация реестра в JSON и обратно (round-trip тестируется),
  с абстракцией файла (интерфейс чтения/записи) — чтобы тесты не трогали диск.
- **`CraftWindowPatch` (Plugin).** Harmony-postfix на построении окна выбора крафта
  (точка уточняется в игре: `UIBaseCraftSelectionWindow.*` — Init/OnEnable/Redraw) — добавляет
  кнопку «∞» рядом с «+» (клон игровой `LazyButton`, состояние — вкл/выкл подсветкой).
- **`EndlessRunner` (Plugin).** Следит за станциями в режиме ∞: когда у `CraftComponent` нет
  крафтов в очереди (`HasCraftsInQueue == false`) и станция «готова» — ставит сохранённый рецепт
  (`TryStartCraft`); ошибки ловим и логируем, режим не выключаем (Р3).
- **`StationKey` (Plugin).** Идентификатор станции (по `WgoData`/`uniqueId`) и рецепта
  (`CraftDefBase` id) — строки для Core-реестра.
- **`Mod` / settings (Plugin).** Настройки через GK2 Framework: `Enabled` (глобально вкл/выкл мод),
  `PollSeconds` (как часто проверять, по умолчанию 0.5 с), `Language`, `DebugLog`. Фолбэк — `Config.Bind`.

### 5.3 Поток

1. Игрок открывает окно станции → патч добавляет кнопку «∞».
2. Клик по «∞» → `EndlessRegistry.Set(stationId, recipeId)` (или `Clear`), UI показывает состояние,
   состояние сразу пишется в файл слота.
3. `EndlessRunner` раз в `PollSeconds` проходит по станциям реестра; станция считается готовой,
   если у неё есть `CraftComponent` и он сейчас не выполняет крафт (`HasCraftsInQueue == false`).
   Тогда раннер ставит сохранённый рецепт снова. Нет ингредиентов → просто пропускаем до
   следующего тика (Р3).
4. Загрузка сейва → реестр читается из файла слота (`BepInEx\config\GK2EndlessCrafting\<слот>.json`).

### 5.4 Обработка ошибок

- Все вызовы игровых методов — в try/catch с логом; исключение не выключает режим и не ломает окно.
- Станция исчезла (снесли/переехала) → запись остаётся, но раннер просто не найдёт `CraftComponent`
  и ничего не сделает; при повторном появлении с тем же id режим снова работает.
- Битый/нечитаемый JSON → пустой реестр + предупреждение в лог, файл не затираем до следующей записи.

## 6. Тестирование

- Core (xUnit): `EndlessRegistry` (вкл/выкл/перезапись рецепта, перебор), `EndlessStore`
  (round-trip, пустой файл, битый JSON, чужие ключи игнорируются), решения раннера
  (ставить/не ставить: пусто ли, включено ли, есть ли рецепт).
- Ручная проверка в игре: обычная станция, конвейер/авто-крафтер, станция без ингредиентов,
  выключение режима, перезапуск игры (режим восстановился), геймпад.

## 7. Публикация

- Версия 1.0.0; описание RU/EN (как у Zombie HQ/Extract All).
- Steam Workshop: новый айтем, превью 512×512 + один реальный скрин (лимит 2 картинки, ≤2 МБ!).
- GitHub Release: zip `BepInEx/plugins/GK2EndlessCrafting/{dll, README.txt}`.
- Nexus: комплект (BBCode-описание, поля, zip, картинки) в `E:\GK2Upload\nexus\`.

## 8. Риски и открытые вопросы

- **Точный метод постановки в очередь.** `TryStartCraft` (стартует сразу) против
  `AddCraftNoStart` (просто в очередь). Решение: ставить `AddCraftNoStart`, если он ставит без
  старта и игра сама подхватит; иначе `TryStartCraft`. Проверяется в игре на первом прототипе.
- **Сигнал «очередь пуста».** `HasCraftsInQueue` — базовый; на конвейерах логика может отличаться
  (`IsAutoCraftable`, `TryDropConveyorWorkbenchCraftInventory`) — отдельная проверка в игре.
- **Определение слота сейва** для имени файла (напр. «Steam_1») — берём то же, что использует
  Shopping List (`SaveSystem`/текущий слот); если недоступно — один общий файл.
- **Производительность:** опрос раз в 0.5 с по десяткам станций дёшев, но если станций много —
  переходим на события (`OnCraftFinish`/`OnCraftRemovedFromQueue`) вместо опроса.
- **Мультистанции с одинаковым id** (клоны) — считаем их одной записью (приемлемо).
