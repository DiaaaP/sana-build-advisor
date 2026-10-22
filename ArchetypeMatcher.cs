using System;
using System.Collections.Generic;

namespace SanaBuildOverlay
{
    // Initial verified archetypes are deliberately narrow. New DLC combinations
    // remain hypotheses until their mechanics are confirmed in the installed game.
    public static class ArchetypeMatcher
    {
        public static List<ArchetypeMatch> Match(BuildSnapshot b)
        {
            var result = new List<ArchetypeMatch>();
            if (b == null || !b.InventoryKnown) return result;
            string token = b.CharacterToken ?? "";
            if (token.IndexOf("HUNTRESS", StringComparison.OrdinalIgnoreCase) >= 0 &&
                b.Primary != null && b.Primary.Id == "HUNTRESS_PRIMARY_ALT_NAME")
            {
                bool corrupted = b.Count("CritGlassesVoid") > 0;
                BuildLifecycle phase = corrupted ? BuildLifecycle.Blocked
                    : StatCalculator.Finite(b.Crit) && b.Crit >= 100 ? BuildLifecycle.Saturation
                    : b.Count("CritGlasses") == 0 ? BuildLifecycle.Search
                    : b.Count("CritGlasses") == 1 ? BuildLifecycle.Activation
                    : BuildLifecycle.Strengthening;
                result.Add(new ArchetypeMatch {
                    Id = "huntress-flurry-crit", Name = "Flurry: критический залп", Lifecycle = phase, Verified = true,
                    Evidence = "Установленное описание Flurry: 3 стрелы, при крите — 6. Lens-Maker's Glasses даёт +10 п.п. крита.",
                    NextStep = corrupted ? "Обычные крит-очки заменены Void-вариантом; маршрут через их стаки закрыт."
                        : phase == BuildLifecycle.Saturation ? "Шанс крита достиг 100%; искать другой ограничивающий фактор."
                        : "Повышать шанс крита, сравнивая следующий стак с защитой и мобильностью.",
                    Risk = "Фактический DPS и гарантированные криты других эффектов не моделируются."
                });
            }
            if (token.IndexOf("RAILGUNNER", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                BuildMetrics metrics = BuildProfileAnalyzer.Measure(b);
                int crowbars = b.Count("Crowbar");
                result.Add(new ArchetypeMatch {
                    Id = "railgunner-crit-conversion", Name = "Railgunner: крит + первый выстрел",
                    Lifecycle = !metrics.CoreReady ? BuildLifecycle.Search
                        : crowbars == 0 ? BuildLifecycle.Activation : BuildLifecycle.Strengthening,
                    Verified = true,
                    Evidence = "Пассивка преобразует шанс крита в критический урон; Crowbar даёт +75% за стак только по цели выше 90% HP.",
                    NextStep = !metrics.CoreReady ? "Сначала закрыть базовую мобильность и защиту; чистый all-in в Crowbar оставляет билд хрупким."
                        : crowbars == 0 ? "База закрыта: Crowbar может запустить специализацию первого попадания."
                        : "Crowbar x" + crowbars + ": следующий стак усиливает открывающий выстрел; Crit Glasses остаются альтернативой для критического урона.",
                    Risk = "Это не гарантированный ваншот любой цели: нужен HP выше 90%, попадание/слабая точка, а здоровье боссов и сложность продолжают масштабироваться."
                });
            }
            if (token.IndexOf("ENGI", StringComparison.OrdinalIgnoreCase) >= 0 &&
                b.Special != null && b.Special.Id == "ENGI_SPECIAL_NAME")
            {
                int fungus = b.Count("Mushroom");
                result.Add(new ArchetypeMatch {
                    Id = "engineer-stationary-turret", Name = "Engineer: стационарные турели",
                    Lifecycle = fungus == 0 ? BuildLifecycle.Search : fungus == 1 ? BuildLifecycle.Activation : BuildLifecycle.Strengthening,
                    Verified = true,
                    Evidence = "Стационарные турели наследуют предметы Engineer и постоянно стоят на месте; Bustling Fungus создаёт лечебную зону после 1 секунды неподвижности.",
                    NextStep = fungus == 0 ? "Найти первый Bustling Fungus для самоподдержки турелей."
                        : "Bustling Fungus x" + fungus + ": дополнительные стаки усиливают лечение и расширяют радиус; дальше сравнивать с уроном и защитой турелей.",
                    Risk = "Маршрут относится к стационарным Gauss Turret; мобильные Carbonizer Turret не поддерживают постоянную зону тем же способом."
                });
            }
            if (token.IndexOf("SEEKER", StringComparison.OrdinalIgnoreCase) >= 0 &&
                b.Utility != null && b.Utility.Id == "SEEKER_UTILITY_NAME")
            {
                int transcendence = b.Count("ShieldOnly");
                int glass = b.Count("LunarDagger");
                int crowbar = b.Count("Crowbar");
                BuildLifecycle phase = transcendence == 0 ? BuildLifecycle.Search
                    : glass == 0 ? BuildLifecycle.Activation : BuildLifecycle.Strengthening;
                result.Add(new ArchetypeMatch {
                    Id = "seeker-sojourn-lunar-burst", Name = "Seeker / Sojourn: лунный взрыв", Lifecycle = phase, Verified = false,
                    Evidence = "Локальные описания: Sojourn расходует 25% здоровья/с и усиливает взрыв; Transcendence переводит здоровье в щит; Shaped Glass повышает базовый урон и режет максимум HP; Crowbar усиливает первый удар.",
                    NextStep = transcendence == 0 ? "Для гипотезы нужен Transcendence; без него взаимодействие со щитом не запускается."
                        : glass == 0 ? "Основа со щитом собрана; Shaped Glass может усилить взрыв ценой максимального здоровья."
                        : crowbar == 0 ? "Crowbar усиливает попадание по цели выше 90% HP; проверить порог на реальном взрыве."
                        : "Компоненты гипотезы собраны. Не наращивать риск без проверки времени полёта и округления самоурона.",
                    Risk = "ГИПОТЕЗА для версии 1.5.0#1210*: длительность полёта, округление самоурона, OSP и масштаб взрыва не подтверждены кодом/игровым тестом."
                });
            }
            if (token.IndexOf("ACOLYTE", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                int transcendence = b.Count("ShieldOnly");
                result.Add(new ArchetypeMatch {
                    Id = "acolyte-transcendence", Name = "Acolyte / Transcendence", Verified = false,
                    Lifecycle = transcendence == 0 ? BuildLifecycle.Search : BuildLifecycle.Activation,
                    Evidence = "Локальная пассивка Acolyte запрещает обычное лечение и превращает его в скорость атаки; Transcendence переводит почти всё HP в регенерирующий щит.",
                    NextStep = transcendence == 0 ? "Transcendence может заменить недоступное обычное лечение регенерацией щита; сначала оценить риск задержки восстановления."
                        : "Transcendence активен: избегать частого урона, который постоянно откладывает восстановление щита.",
                    Risk = "ГИПОТЕЗА для версии 1.5.0#1210*: сброс задержки щита, recoverable health альтернативной пассивки и особые источники лечения не проверены."
                });
            }
            if (result.Count == 0 && !string.IsNullOrEmpty(token))
            {
                BuildMetrics metrics = BuildProfileAnalyzer.Measure(b);
                bool burst = BuildProfileAnalyzer.IsBurstOpener(b);
                bool proc = BuildProfileAnalyzer.IsRapidProc(b);
                bool close = BuildProfileAnalyzer.IsCloseRange(b);
                string id = burst ? "adaptive-heavy-hit" : proc ? "adaptive-proc" : close ? "adaptive-close-range" : "adaptive-balanced";
                string name = burst ? "Тяжёлый первый удар" : proc ? "Proc и постоянный урон"
                    : close ? "Ближний бой и восстановление" : "Адаптивный универсальный билд";
                string next = !metrics.CoreReady ? "Сначала взять хотя бы один источник защиты и мобильности."
                    : burst ? "База закрыта: сравни Crowbar, Bands и AoE с текущей слабейшей полосой."
                    : proc ? "База закрыта: усиливай частые срабатывания, но не теряй зачистку и защиту."
                    : close ? "Поддерживай защиту и восстановление выше темпа роста сложности, затем усиливай урон вблизи."
                    : "Закрывай самую низкую полосу; после 120% переходи к синергии выбранных навыков.";
                result.Add(new ArchetypeMatch { Id = id, Name = name,
                    Lifecycle = !metrics.CoreReady ? BuildLifecycle.Search : BuildLifecycle.Strengthening,
                    Verified = true,
                    Evidence = "Маршрут выбран по текущему персонажу, активным навыкам и live-полосам; отдельная формула навыка не выдумывается.",
                    NextStep = next,
                    Risk = "Это безопасный общий маршрут. Уникальные взаимодействия неподдержанных навыков могут изменить лучший выбор." });
            }
            return result;
        }

        public static string PhaseName(BuildLifecycle phase)
        {
            switch (phase)
            {
                case BuildLifecycle.Search: return "ПОИСК";
                case BuildLifecycle.Activation: return "АКТИВАЦИЯ";
                case BuildLifecycle.Strengthening: return "УСИЛЕНИЕ";
                case BuildLifecycle.Saturation: return "НАСЫЩЕНИЕ";
                case BuildLifecycle.Blocked: return "МАРШРУТ ЗАКРЫТ";
                default: return "НЕИЗВЕСТНО";
            }
        }
    }
}
