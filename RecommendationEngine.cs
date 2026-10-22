using System;
using System.Collections.Generic;
using System.Globalization;

namespace SanaBuildOverlay
{
    public sealed class SkillSnapshot
    {
        public string Id = "";
        public string Name = "";
        public double Recharge = double.NaN;
        public int RequiredStock;
        public bool AttackSpeedRestocks;
    }
    public sealed class BuildSnapshot
    {
        public readonly Dictionary<string, int> Counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> Categories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Available = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public bool InventoryKnown;
        public bool Railgunner;
        public string CharacterToken = "";
        public double Crit = double.NaN, Damage = double.NaN, CritMultiplier = double.NaN;
        public double NaturalDamage = double.NaN, AttackSpeed = double.NaN;
        public double Health = double.NaN, NaturalHealth = double.NaN, EffectiveHealth = double.NaN;
        public double Armor = double.NaN, Regen = double.NaN, MoveSpeed = double.NaN;
        public int Stage = 1, HostileCount;
        public double Difficulty = double.NaN;
        public EncounterKind Encounter;
        public int LiveDamageItems, LiveHealingItems, LiveUtilityItems, LiveAoeItems;
        public int DamageReadiness = -1, HealthReadiness = -1, DefenseReadiness = -1, MobilityReadiness = -1;
        public int Style = 1;
        public int Goal;
        public SkillSnapshot Primary, Secondary, Utility, Special;
        public int Count(string id) { int n; return Counts.TryGetValue(id, out n) ? Math.Max(0, n) : 0; }
    }
    public sealed class ItemAdvice
    {
        public string Id, Tier, Reason;
        public int Count, Goal;
        public double Score;
        public double MarginalPercent = double.NaN;
        public string StackState = "ОЦЕНКА";
        public string StackText = "";
        public string ImpactText = "";
        public EvidenceStatus Status = EvidenceStatus.Estimated;
    }
    public static class RecommendationEngine
    {
        private static string F(double n) { return n.ToString("0.#", CultureInfo.CurrentCulture); }
        private static bool RechargeSkill(SkillSnapshot s)
        { return s != null && StatCalculator.Finite(s.Recharge) && s.Recharge > 0 && s.RequiredStock > 0; }

        public static List<ItemAdvice> Evaluate(BuildSnapshot b)
        {
            var result = new List<ItemAdvice>();
            if (!b.InventoryKnown) return result;
            BuildMetrics profile = BuildProfileAnalyzer.Measure(b);
            bool burstOpener = BuildProfileAnalyzer.IsBurstOpener(b);
            bool rapidProc = BuildProfileAnalyzer.IsRapidProc(b);
            bool closeRange = BuildProfileAnalyzer.IsCloseRange(b);
            foreach (ItemRule rule in ItemDatabase.Rules)
            {
                if (!b.Available.Contains(rule.Id) || (rule.CorruptedBy != null && b.Count(rule.CorruptedBy) > 0)) continue;
                int n = b.Count(rule.Id);
                if (n == int.MaxValue) continue;
                double score = rule.Priority / (1 + n * 0.35);
                string reason;
                bool defensive = rule.Category == "movement" || rule.Category == "defense" || rule.Category == "healing";
                score += defensive ? (b.Style == 0 ? 18 : b.Style == 2 ? -10 : 0)
                    : (b.Style == 2 ? 18 : b.Style == 0 ? -8 : 0);
                if (b.Goal == 1 && rule.Category == "offense") score += 26;
                else if (b.Goal == 2 && (rule.Category == "defense" || rule.Category == "healing")) score += 30;
                else if (b.Goal == 3 && rule.Category == "movement") score += 32;
                else if (b.Goal == 4 && (rule.Category == "offense" || rule.Category == "skill")) score += 18;
                else if (b.Goal == 5 && rule.Category == "skill") score += 12;
                score += ReadinessAdjustment(b, rule.Category);
                score += EncounterAdjustment(b, rule.Id, rule.Category);
                switch (rule.Id)
                {
                    case "Hoof":
                        reason = "+14% базовой скорости за стак; модель только Hoof: +" + F(StatCalculator.ItemOnlyGain(.14, n)) + "% к её множителю. Другие бонусы не учтены.";
                        break;
                    case "SprintBonus":
                        reason = "+25% скорости спринта за стак по описанию установленной игры. Действует только во время спринта; итог с другими множителями не прогнозируется.";
                        break;
                    case "Bear":
                        reason = "Дополнительный шанс блока. Итоговый шанс здесь не рассчитывается; это не +15 п.п. к текущему шансу.";
                        break;
                    case "ArmorPlate":
                        reason = "Уменьшает каждый входящий удар на 5 за стак, но не ниже 1. Особенно заметно против частых мелких ударов; это не процентное снижение.";
                        break;
                    case "FlatHealth":
                        reason = "+25 максимального здоровья за стак. Полезность зависит от уровня, щита, проклятия и процентного лечения.";
                        break;
                    case "PersonalShield":
                        reason = "+8% максимального здоровья в виде щита за стак; щит восстанавливается вне опасности. Получение урона задерживает восстановление.";
                        break;
                    case "Medkit":
                        reason = "Через 2 секунды после получения урона лечит на 20 + 5% максимального HP за стак. Повторный урон и запрет лечения могут снизить ценность.";
                        break;
                    case "Mushroom":
                        reason = "После 1 секунды без движения создаёт лечебную зону: 4.5% HP/с, ещё +2.25% за стак; радиус также растёт. Слабее для постоянно движущегося билда.";
                        if (b.CharacterToken.IndexOf("ENGI", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            b.Special != null && b.Special.Id == "ENGI_SPECIAL_NAME")
                        {
                            score += 44;
                            reason += " Стационарные турели Engineer наследуют предметы и постоянно выполняют условие неподвижности — это ключевая синергия билда.";
                        }
                        break;
                    case "HealWhileSafe":
                        reason = "+3 HP/с базовой регенерации за стак вне боя. Не помогает под непрерывным входящим уроном.";
                        break;
                    case "Syringe":
                        reason = "+15% базовой скорости атаки. Это не +15% DPS.";
                        if (RechargeSkill(b.Primary) && !b.Primary.AttackSpeedRestocks)
                        {
                            score -= 25;
                            reason += " Primary расходует заряды; SkillDef не связывает их восстановление со скоростью атаки.";
                        }
                        else if (b.Primary != null && b.Primary.AttackSpeedRestocks)
                        {
                            score += 8;
                            reason += " У выбранного Primary SkillDef разрешает ускорение восстановления от скорости атаки.";
                        }
                        break;
                    case "CritGlasses":
                        if (b.Railgunner)
                        {
                            reason = "Railgunner преобразует крит-стат в критический урон. +10 крит-стата; обычный шанс и средний урон не прогнозируются.";
                        }
                        else
                        {
                            if (!StatCalculator.Finite(b.Crit) || b.Crit >= 100) continue;
                            double gain = StatCalculator.CritGain(b.Crit);
                            score *= gain / 10;
                            reason = "Шанс крита: " + F(StatCalculator.ClampChance(b.Crit)) + "% → " + F(StatCalculator.ClampChance(b.Crit + 10)) + "% (+" + F(gain) + " п.п.).";
                            bool flurry = b.Primary != null && b.Primary.Id == "HUNTRESS_PRIMARY_ALT_NAME";
                            if (flurry)
                            {
                                score += 30;
                                reason += " Flurry: крит выпускает 6 стрел вместо 3; общий урон залпа пока не рассчитывается.";
                            }
                            else if (b.CharacterToken.IndexOf("BANDIT2", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                score -= 25;
                                reason += " У Bandit атаки со спины уже критуют; полезность зависит от позиции.";
                            }
                            else
                            {
                                double before = StatCalculator.ExpectedHit(b.Damage, b.Crit, b.CritMultiplier);
                                double after = StatCalculator.ExpectedHit(b.Damage, b.Crit + 10, b.CritMultiplier);
                                if (StatCalculator.Finite(before) && StatCalculator.Finite(after) && before > 0)
                                    reason += " Модель попадания 100%: " + F(before) + " → " + F(after) + "; не DPS, без удачи и гарантированных критов.";
                            }
                        }
                        break;
                    case "Crowbar":
                        reason = "+75% урона за стак по целям выше 90% здоровья. Силен для первого тяжёлого попадания; почти не помогает после выхода цели из порога.";
                        if (burstOpener)
                        {
                            if (profile.CoreReady)
                            {
                                score += 80d / (1d + n * 0.05d);
                                reason += " Базовое покрытие защиты и движения уже есть: Crowbar остаётся специализацией первого выстрела, но его относительная отдача убывает с каждым стаком.";
                                int survival = MinKnown(b.HealthReadiness, b.DefenseReadiness);
                                if (survival >= 0 && survival < 95)
                                    reason += " Сейчас живучесть ниже цели этапа, поэтому защитный стак может быть важнее.";
                            }
                            else
                            {
                                reason += " Специализацию пока рано форсировать: сначала закрыть хотя бы базовую мобильность и защиту.";
                            }
                        }
                        break;
                    case "BleedOnHit":
                        reason = "+10 п.п. шанса кровотечения за стак; кровотечение наносит 240% базового урона. Реальный шанс зависит от proc coefficient атаки.";
                        break;
                    case "StickyBomb":
                        reason = "+5 п.п. шанса бомбы за стак; взрыв наносит 180% TOTAL damage. Реальный шанс зависит от proc coefficient атаки.";
                        break;
                    case "SecondarySkillMagazine":
                        if (!RechargeSkill(b.Secondary)) continue;
                        if (b.Railgunner && b.Secondary.Id == "RAILGUNNER_SECONDARY_ALT_NAME") continue;
                        reason = "+1 заряд Secondary: " + b.Secondary.Name + ". Помогает запасу применений; не ускоряет восстановление. Особые переопределения навыков не моделируются.";
                        if (b.Railgunner && b.Secondary.Id == "RAILGUNNER_SECONDARY_NAME")
                        {
                            score += 18;
                            reason += " Для M99 дополнительный заряд даёт ещё один тяжёлый прицельный выстрел до ожидания перезарядки.";
                        }
                        break;
                    case "Feather":
                        reason = "+1 дополнительный прыжок. Повторный стак расширяет воздушную мобильность, но не повышает скорость бега.";
                        break;
                    case "SprintArmor":
                        reason = "+30 брони за стак только во время спринта. Не защищает, когда персонаж вынужден прекратить спринт для атаки.";
                        break;
                    case "BossDamageBonus":
                        reason = "+20% урона по боссам за стак. Не ускоряет зачистку обычных противников.";
                        break;
                    case "ChainLightning":
                        reason = n == 0 ? "Добавляет цепную молнию для групп. Частота зависит от proc coefficient; он для выбранной атаки пока не подтверждён."
                            : "Следующий стак расширяет цепь; шанс срабатывания от стаков не повышается.";
                        if (rapidProc && profile.CoreReady)
                        {
                            score += 18;
                            reason += " Скорострельный профиль уже имеет базовое покрытие, поэтому proc-цепочка получает дополнительный приоритет.";
                        }
                        if (b.Railgunner)
                        {
                            score += 16;
                            reason += " Railgunner сильна по одиночным целям, поэтому AoE частично закрывает её уязвимость к толпе.";
                        }
                        break;
                    case "Missile":
                        reason = "10% шанс ракеты на 300% TOTAL damage; каждый следующий стак добавляет ещё 300% урона ракеты. Частота зависит от proc coefficient.";
                        if (rapidProc && profile.CoreReady)
                        {
                            score += 30;
                            reason += " После закрытия базы скорострельный профиль может специализироваться на многократных proc-проверках.";
                        }
                        break;
                    case "ExplodeOnDeath":
                        reason = "Взрыв после убийства помогает группам. Требует убийства, поэтому не даёт самостоятельного прироста против одиночного босса без прислужников.";
                        if (b.Railgunner) { score += 18; reason += " Это компенсирует слабую зачистку групп после первого убийства."; }
                        break;
                    case "SlowOnHit":
                        reason = "Замедляет поражённые цели; следующий стак увеличивает длительность. Контроль полезен не всем боссам и не является прямым DPS.";
                        break;
                    case "NearbyDamageBonus":
                        reason = "+20% урона за стак по целям в пределах 13 м. Для дальнего билда бонус зависит от опасной близкой позиции.";
                        if (closeRange && profile.CoreReady)
                        {
                            score += 24;
                            reason += " Выбранный персонаж регулярно играет вблизи, поэтому условие заметно реалистичнее.";
                        }
                        break;
                    case "AttackSpeedOnCrit":
                        if (!b.Railgunner && (!StatCalculator.Finite(b.Crit) || b.Crit <= 0)) score -= 24;
                        reason = "Криты дают +12% скорости атаки до базового лимита 36%; дополнительные стаки повышают предел на 24%. Нужны реальные критические попадания.";
                        if (b.Railgunner && b.Secondary != null && b.Secondary.Id == "RAILGUNNER_SECONDARY_NAME")
                        { score += 12; reason += " С M99 скорость атаки помогает активной перезарядке и темпу следующих выстрелов."; }
                        break;
                    case "DeathMark":
                        reason = "При 4+ разных дебаффах цель получает +50% урона на 7 секунд; стаки продлевают метку, а не повышают её множитель. Наличие четырёх дебаффов пока не доказано.";
                        score -= 12;
                        break;
                    case "EquipmentMagazine":
                        reason = "+1 заряд Q и снижение его отката на 15% за стак по описанию игры. Ценность зависит от выбранного снаряжения.";
                        break;
                    case "FireRing":
                        reason = "Удар выше 400% запускает огненный вихрь на 300% TOTAL damage за стак; внутренний откат 10 секунд. Нужен действительно тяжёлый удар.";
                        if (burstOpener && profile.CoreReady)
                        {
                            score += 38;
                            reason += " Профиль первого удара регулярно выполняет порог, поэтому Kjaro становится основной burst-синергией.";
                        }
                        break;
                    case "IceRing":
                        reason = "Удар выше 400% запускает ледяной взрыв на 250% TOTAL damage за стак и сильное замедление; внутренний откат 10 секунд.";
                        if (burstOpener && profile.CoreReady)
                        {
                            score += 36;
                            reason += " Профиль первого удара регулярно выполняет порог, поэтому Runald усиливает burst и контроль.";
                        }
                        break;
                    case "AlienHead":
                        if (!RechargeSkill(b.Primary) && !RechargeSkill(b.Secondary) && !RechargeSkill(b.Utility) && !RechargeSkill(b.Special)) continue;
                        reason = "Сокращение откатов: в наборе есть навык с восстановлением зарядов. Описание игры: 25%; конечные откаты с особыми механиками не прогнозируются.";
                        break;
                    case "UtilitySkillMagazine":
                        if (!RechargeSkill(b.Utility)) continue;
                        reason = "+2 заряда Utility: " + b.Utility.Name + ". Игра также указывает снижение её отката; точный итог зависит от механики навыка.";
                        break;
                    case "Dagger":
                        reason = "После убийства выпускает 3 самонаводящихся кинжала по 150% базового урона; каждый стак добавляет урон. Не запускается без убийства.";
                        break;
                    case "Behemoth":
                        reason = "Все атаки взрываются на 60% TOTAL damage; стаки увеличивают радиус на 1.5 м. Сильнее по группам и при частых попаданиях.";
                        if (b.Railgunner) { score += 24; reason += " TOTAL damage масштабируется от тяжёлого попадания и добавляет нужную зачистку вокруг цели."; }
                        break;
                    case "Clover":
                        reason = "+1 благоприятный повтор случайных эффектов. Польза зависит от уже собранных предметов с шансом; без них эффект ограничен.";
                        break;
                    case "ShieldOnly":
                        reason = "Transcendence оставляет 1 HP, переводит остальное в восстанавливаемый щит и повышает максимум здоровья. Обычное лечение перестаёт восстанавливать этот щит; получение урона задерживает регенерацию.";
                        if (b.CharacterToken.IndexOf("ACOLYTE", StringComparison.OrdinalIgnoreCase) >= 0) score += 24;
                        else if (b.CharacterToken.IndexOf("SEEKER", StringComparison.OrdinalIgnoreCase) >= 0 && b.Utility != null && b.Utility.Id == "SEEKER_UTILITY_NAME") score += 12;
                        break;
                    case "LunarDagger":
                        reason = "Shaped Glass: +100% базового урона и −50% максимального здоровья за стак. Огромный риск; OSP и итоговое округление взаимодействий отдельно не гарантируются.";
                        score += b.Style == 2 ? 20 : b.Style == 0 ? -42 : -15;
                        break;
                    case "MushroomVoid":
                        reason = "Лечит на 2% HP/с за стак во время спринта и заменяет Bustling Fungus. Подходит мобильному билду; запрет лечения может выключить пользу.";
                        break;
                    case "BleedOnHitVoid":
                        reason = "+10 п.п. шанса Collapse за стак, 400% базового урона; заменяет все Tri-Tip Daggers. Частота зависит от proc coefficient.";
                        break;
                    case "MissileVoid":
                        reason = "Даёт щит 10% максимального HP; пока щит активен, попадания запускают ракету. Стаки усиливают ракету, а не щит; заменяет AtG.";
                        break;
                    case "SlowOnHitVoid":
                        reason = "+5 п.п. шанса обездвижить за стак и +1 с длительности; заменяет Chronobauble. Некоторые цели устойчивы к контролю.";
                        break;
                    case "ExplodeOnDeathVoid":
                        reason = "Первое попадание по цели с полным HP вызывает взрыв; стаки повышают радиус и урон. Заменяет Will-o'-the-wisp.";
                        break;
                    case "RechargeableJump":
                        reason = "Даёт 3 дополнительных прыжка, которые восстанавливаются после 5 секунд на земле; стак добавляет 2 прыжка и ускоряет восстановление. Каждый неиспользованный прыжок даёт 5 брони.";
                        break;
                    case "BlightZone":
                        reason = "Во время телепортера/holdout выпускает растущее облако Blight; командный урон усредняется между стаками. Вне зон событие не помогает.";
                        break;
                    case "SprintDamage":
                        reason = "Спринт накапливает заряды по пройденной дистанции; на полном заряде растут скорость и урон столкновением, масштабируемый скоростью движения.";
                        break;
                    case "GhostBlades":
                        reason = "Даёт 1% lifesteal за стак; крупное лечение создаёт до 6 клинков, а Secondary выпускает их вперёд. Нужны лечение и подходящий Secondary.";
                        if (b.CharacterToken.IndexOf("ACOLYTE", StringComparison.OrdinalIgnoreCase) >= 0) score -= 28;
                        break;
                    case "CausticPheromones":
                        reason = "После 10 попаданий навыками оглушает и включает friendly fire у цели на 5 секунд; стаки продлевают эффект. Есть 15-секундная задержка повторения.";
                        break;
                    case "DreamDemon":
                        reason = "20% шанс призвать Nightmare Demon; после накопления 1600% урона удар навыком взрывает его и активирует on-kill эффекты. Нужна полноценная цепочка активации.";
                        break;
                    case "SanguineChalice":
                        reason = "Даёт 10% шанс кровотечения; каждый тик bleed лечит на 0.5% максимального HP за стак. Ценность зависит от частоты bleed и возможности лечиться.";
                        if (b.Count("BleedOnHit") == 0 && b.Count("BleedOnHitVoid") == 0) score -= 8;
                        break;
                    case "VengefulSpirit":
                        reason = "Защитный дух принимает 50% входящего урона. После его гибели смерти союзников дают +40% скорости атаки и −20% откатов на 15 секунд за стак.";
                        break;
                    case "LowHealthOrbSpawner":
                        reason = "10% шанс за стак принять на себя расход или откат другого предмета; восстанавливается в начале этапа и не может восстановить себя.";
                        break;
                    case "GoldenGoose":
                        reason = "Удары выше 400% превращают ближайших врагов в золото на 5 секунд; стаки повышают радиус и добычу золота. Откат 10 секунд.";
                        break;
                    case "VileCenser":
                        reason = "10% шанс оглушения; удар навыком по оглушённой цели запускает цепные теневые щупальца. Стаки увеличивают урон и радиус цепи.";
                        break;
                    default: continue;
                }
                if (rule.Category == "healing" && b.CharacterToken.IndexOf("ACOLYTE", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score += 12;
                    reason += " У Acolyte обычное лечение преобразуется в скорость атаки и не восстанавливает HP напрямую.";
                }
                EvidenceStatus status = rule.Status;
                if (b.Railgunner && rule.Id == "CritGlasses") status = EvidenceStatus.Estimated;
                StackGuidance stack = AdvancedAdvisor.Stack(b, rule.Id, n);
                if (stack.State == "ПРЕДЕЛ") score -= 46d;
                else if (stack.State == "СЛАБАЯ ОТДАЧА") score -= 18d;
                if (!string.IsNullOrEmpty(stack.Text)) reason += " " + stack.Text;
                result.Add(new ItemAdvice { Id = rule.Id, Tier = rule.Tier, Count = n, Goal = n + 1,
                    Score = score, Reason = reason, Status = status, MarginalPercent = stack.MarginalPercent,
                    StackState = stack.State, StackText = stack.Text });
            }
            result.Sort(delegate(ItemAdvice a, ItemAdvice c) {
                int order = c.Score.CompareTo(a.Score);
                return order != 0 ? order : string.CompareOrdinal(a.Id, c.Id);
            });
            return result;
        }

        private static int MinKnown(int a, int c)
        {
            if (a < 0) return c;
            if (c < 0) return a;
            return Math.Min(a, c);
        }

        private static double ReadinessAdjustment(BuildSnapshot b, string category)
        {
            int survival = MinKnown(b.HealthReadiness, b.DefenseReadiness);
            if (category == "defense" || category == "healing")
                return survival < 0 ? 0d : Math.Max(0, 110 - survival) * 0.9d;
            if (category == "movement")
                return b.MobilityReadiness < 0 ? 0d : Math.Max(0, 110 - b.MobilityReadiness) * 0.8d;
            if (category == "offense" || category == "skill")
            {
                double adjustment = b.DamageReadiness < 0 ? 0d : Math.Max(0, 100 - b.DamageReadiness) * 0.55d;
                if (survival >= 0 && survival < 70) adjustment -= 60d;
                else if (survival >= 0 && survival < 85) adjustment -= 30d;
                if (b.MobilityReadiness >= 0 && b.MobilityReadiness < 75) adjustment -= 15d;
                return adjustment;
            }
            return 0d;
        }

        private static double EncounterAdjustment(BuildSnapshot b, string id, string category)
        {
            bool clear = id == "ChainLightning" || id == "ExplodeOnDeath" || id == "ExplodeOnDeathVoid"
                || id == "Behemoth" || id == "Dagger" || id == "Missile" || id == "MissileVoid";
            if (b.Encounter == EncounterKind.Swarm)
                return clear ? 32d : category == "control" ? 16d : id == "Crowbar" ? -14d : 0d;
            if (b.Encounter == EncounterKind.Teleporter)
                return clear ? 18d : category == "defense" || category == "healing" ? 8d : 0d;
            if (b.Encounter == EncounterKind.Boss)
            {
                if (id == "BossDamageBonus") return 34d;
                if (id == "FireRing" || id == "IceRing" || id == "Crowbar") return 14d;
                if (id == "ExplodeOnDeath" || id == "Dagger") return -12d;
            }
            return 0d;
        }

        // Until a real pickup/shop/Command picker is visible, prefer an ordinary
        // common goal for the compact HUD. Higher tiers remain ranked in the plan.
        public static ItemAdvice SelectPractical(IList<ItemAdvice> ranked)
        {
            if (ranked == null) return null;
            foreach (ItemAdvice advice in ranked) if (advice.Tier == "Tier1") return advice;
            foreach (ItemAdvice advice in ranked) if (advice.Tier == "Tier2") return advice;
            return ranked.Count > 0 ? ranked[0] : null;
        }
    }
}
