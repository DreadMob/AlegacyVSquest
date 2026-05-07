# План рефакторинга vsquest

## Волна 1: Critical fixes (🔴 High)

- [ ] **Task 1** — Убрать пустые `catch {}` по всему проекту, добавить `api.Logger.Warning/Error`
- [ ] **Task 2** — Вынести `QuestRegistry`, `ActionRegistry`, `ActionObjectiveRegistry` из `QuestSystem` в `QuestRegistryService`

## Волна 2: Decoupling & build fixes (🟡 Medium)

- [ ] **Task 3** — Заменить `QuestSystemCache` на `api.ModLoader.GetModSystem<>()`
- [ ] **Task 4** — Разнести вложенные конфиг-классы из `AlegacyVsQuestConfig` на отдельные файлы (`BossHuntConfig`, `ActionItemsConfig`, …)
- [ ] **Task 5** — Починить `BossAbilityBase` иерархию (CS0534, CS0246, CS0111, CS0108)

## Волна 3: Structural cleanup (🟢 Low, но масштабные)

- [ ] **Task 6** — Убрать `QuestNetworkHandler`, подключить хендлеры напрямую к subsystem
- [ ] **Task 7** — DTO-слой для ProtoBuf (`ActiveQuestDto`, `BossHuntStateDto`)
- [ ] **Task 8** — Разделить `IObjectiveTracker` → `IKillTracker`, `IBlockTracker`, `IGatherTracker`
- [ ] **Task 9** — Разделить `QuestSystem` God Class на `QuestCoreSystem`, `QuestServerSystem`, `QuestClientSystem`
- [ ] **Task 10** — Рефактор `Utils/` — разнести по subsystem-директориям

## Волна 4: Deep refactoring & legacy

- [ ] **Task 11** — Удалить/мигрировать legacy-трекеры в `ActiveQuest`, убрать `SyncToLegacyTrackers()`
- [ ] **Task 12** — Убрать static service-locator: `HarmonyPatchSwitches`, `PerformanceConfig`, `StaticActionItemRegistry`
- [ ] **Task 13** — Вынести boss-combat логику из `QuestEventHandler` в `BossCombatHandler`
- [ ] **Task 14** — Разгрузить `ActiveQuest`: вынести `completeQuest()`, `handOverItems()`, `itemsGathered()`
- [ ] **Task 15** — Добавить CI + тестовый проект

---

## Зависимости

- `Task 2` упрощает `Task 3` и `Task 12`
- `Task 9` зависит от `Task 2` и `Task 6`
- `Task 7` и `Task 11` лучше после `Task 8`, но не критично


vsquest — Архитектурный Анализ
1. God Class: QuestSystem (471 строк)
Проблема: QuestSystem — классический God Class. Он одновременно:
Владеет 3 публичными Dictionary-реестрами (QuestRegistry, ActionRegistry, ActionObjectiveRegistry)
Создаёт и хранит ~10+ саб-менеджеров (lifecycle, eventHandler, quizSystem, networkHandler, persistenceManager, 3 guiManager, notificationHandler, 3 registry)
Содержит ~20 packet handler-делегаций (внутренние OnQuest*, OnShow*, On*Message методы)
Занимается конфигурацией, инициализацией Harmony, локализацией, управлением жизненным циклом квеста
Рекомендация:
Разделить на 3 отдельных ModSystem (или больше): QuestCoreSystem (регистры + конфиг), QuestServerSystem (серверная логика), QuestClientSystem (клиентская)
Вынести registry в отдельный статический (или DI) QuestRegistryService
Убрать делегации между QuestNetworkHandler → QuestSystem → subsystem — сделать 3 прямых звена вместо 1
2. Тройная делегация в Network Layer
Проблема:
VsQuestNetworkRegistry → регистрирует хендлеры → QuestNetworkHandler (просто делегирует) → QuestSystem.OnQuest* (тоже просто делегирует) → реальный subsystem
plaintext
[Network message] → VsQuestNetworkRegistry.SetMessageHandler
  → QuestNetworkHandler.OnQuestAccepted (1-строчная делегация)
    → QuestSystem.OnQuestAccepted (1-строчная делегация)
      → lifecycleManager.OnQuestAccepted (реальная логика)
Три уровня forwarding без единой строки логики в промежуточных.Рекомендация:
Регистрировать хендлеры напрямую на subsystem, убрать QuestNetworkHandler как прослойку (он не делает ничего, кроме передачи).
3. Legacy-совместимость как архитектурный налог
Проблема: ActiveQuest всё ещё несёт [Obsolete] поля killTrackers, blockPlaceTrackers, blockBreakTrackers, interactTrackers для ProtoBuf-совместимости. Метод SyncToLegacyTrackers() вызывается при каждом событии (OnEntityKilled, OnBlockPlaced, OnBlockBroken, OnBlockUsed) — это оверхед.Есть TODO: "Remove once all quest packs are migrated" — но он висит уже долго.Рекомендация:
Решить: либо мигрировать все квест-паки и удалить legacy трекеры, либо сделать фазу миграции с versioning в ProtoBuf
Либо сделать IObjectiveTracker единственной системой, а ProtoBuf сериализовать через кастомный сериализатор, который читает/пишет только трекеры
4. Избыточные пустые try/catch (empty swallow)
Проблема: по всему коду множество пустых или почти пустых блоков:
csharp
catch { }
// и
try { ... } catch { }
Это:
В BossHuntSystem.ApplyCoreConfig() — пустой catch
В ItemSystem.ApplyCoreConfig() — пустой catch
В QuestEventHandler.OnPlayerJoin() — пустой catch (x3)
В QuestEventHandler.OnEntityDeath() — пустой catch (x6)
В QuestEventHandler.OnBlockBroken() — пустой catch
В QuestEventHandler.OnBlockPlaced() — пустой catch
В ActiveQuest.OnBlockUsed() — пустой catch
Это маскирует реальные ошибки и делает дебаг невозможным.Рекомендация:
Минимум логировать Exception.Message через api.Logger.Warning/Error
Использовать Debug.Assert() или if/throw с конкретными guard clauses
Пройтись по всем catch { } и заменить на логгирование
5. Static/service-locator паттерны
Проблема: несколько статических/глобальных точек доступа:
QuestSystemCache.Initialize() / QuestSystemCache.Get()
PerformanceConfig — статическая обёртка
StaticActionItemRegistry — статический дубликат реестра
HarmonyPatchSwitches — статический класс с 30+ static bool полями
lastInteractCacheByPlayerUid — static readonly SimpleLRUCache
Это делает код не-тестируемым и создаёт скрытое связывание между компонентами.Рекомендация:
Заменить статические кэши на DI через конструктор
HarmonyPatchSwitches сделать instance-классом, который передаётся в патчи
QuestSystemCache избыточен — если нужен доступ к QuestSystem, использовать api.ModLoader.GetModSystem<>() (как VS API и предполагает)
6. Config как service locator (вложенные классы)
Проблема: AlegacyVsQuestConfig (423 строки) содержит 15+ вложенных классов конфигурации. Subsystem-ы получают доступ к своей части конфига через api.ModLoader.GetModSystem<QuestSystem>().CoreConfig.BossHunt. Это:
Связывает все subsystem-ы с QuestSystem
Создаёт циклические/скрытые зависимости
Каждый подслой дублирует ApplyCoreConfig()
Рекомендация:
Выделить BossHuntConfig, ActionItemsConfig, PerformanceConfig и т.д. как отдельные независимые файлы
Каждый ModSystem загружает свой собственный JSON-файл
Или хотя бы вынести вложенные классы в отдельные .cs файлы
7. Utils/ — мусорная свалка
Проблема: Utils/Quests/ содержит файлы с несвязанными обязанностями:
QuestInteractAtUtil.cs (23.4KB) — огромный утилитарный класс
QuestProgressTextUtil.cs (21.5KB) — третий большой файл
QuestDeathUtil.cs, KillActionObjectiveUtil.cs, и другие
Файлы в Utils/ живут вне системы наследования/композиции, и в них часто дублируется логикаРекомендация:
Разделить по бизнес-назначению: InteractionService, ProgressTextFormatter, DeathHandler
Переместить в соответствующие subsystem-директории
8. Single Responsibility проблемы в отдельных классах
QuestEventHandler (418 строк): содержит прямую логику босс-комбата (heal, announce, credit calculation). Event handler не должен этим заниматься.
ActiveQuest (695 строк): содержит completeQuest() (удаление блоков, раздача предметов), handOverItems(), itemsGathered() — это разные уровни ответственности
QuestLifecycleManager: прямо создаёт EventTracker в CreateTrackers(), не используя ObjectiveTrackerFactory
9. Отсутствие interface segregation
Проблема:
IQuestAction требует только Execute() — и это нормально для действий
Но ActionObjectiveBase — это abstract class, а не интерфейс
IRegistry — только Register(), нет IQuestRepository, IActionRepository и т.д.
IObjectiveTracker — 10 методов, хотя не все трекеры используют все события (gather не использует OnEntityKilled, kill не использует OnBlockPlaced)
Рекомендация:
IQuestRepository / IActionRepository / IObjectiveRepository
Segregated tracker interfaces: IKillTracker, IBlockTracker, IGatherTracker
10. Boss ability hierarchy — build errors
Из build_errors.log:
CS0534 — 5 классов не реализуют BossAbilityBase.StopAbility()
CS0246 — 8 классов не имеют using System.Collections.Generic
CS0111 — EntityBehaviorBossDespair имеет дублирующиеся члены
CS0108 — несколько классов скрывают базовые члены через new
Смешение namespace/проектов внутри одной сборки
Это говорит о проблемах:
Отсутствие обязательного code review
Нет CI проверок сборки
Нарушение LSP (Liskov substitution) в иерархии способностей
11. ProtoContract на доменной модели
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)] на классах вроде ActiveQuest, BossHuntStateEntry — это coupling сериализации и доменной логики. Если изменится схема ProtoBuf, изменится доменная модель, и наоборот.Рекомендация:
DTO-слой (ActiveQuestDto, BossHuntStateDto) с маппингом туда/обратно. Да, это больше кода, но разделяет concerns.
12. Отсутствие тестов
Во всём репозитории нет ни одного тестового проекта, ни одного [Test]. Учитывая, что это:
Сложная система с ~50+ типами квестовых objective/action
Network messages
Stateful persistence
Harmony patches
Это критический риск.
Приоритетные действия (что можно сделать сейчас)
Приоритет	Что делать	Где
🔴 High	Убрать пустые catch { }	По всему проекту
🔴 High	Начать выделять registry в отдельные компоненты	QuestSystem.cs
🟡 Medium	Избавиться от QuestSystemCache статики	Заменить на api.ModLoader.GetModSystem
🟡 Medium	Extract nested config classes	AlegacyVsQuestConfig.cs → отдельные файлы
🟡 Medium	Починить BossAbilityBase иерархию	src/Entity/Behavior/Boss/Abilities/
🟢 Low	Упростить network delegation цепочку	Убрать QuestNetworkHandler
🟢 Low	DTO для ProtoBuf	Отделить ActiveQuest от [ProtoContract]
🟢 Low	Segregated tracker interfaces	IObjectiveTracker → IKillTracker, IBlockTracker