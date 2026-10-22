using System;
using UnityEngine;

namespace SanaBuildOverlay
{
    public sealed partial class BuildOverlay
    {
        private static readonly Color Background = new Color(0.045f, 0.065f, 0.10f, 0.96f);
        private static readonly Color Surface = new Color(0.082f, 0.112f, 0.16f, 0.98f);
        private static readonly Color Raised = new Color(0.115f, 0.155f, 0.215f, 0.98f);
        private static readonly Color Accent = new Color(0.30f, 0.88f, 0.79f, 1f);
        private static readonly Color Text = new Color(0.96f, 0.97f, 1f, 1f);
        private static readonly Color Muted = new Color(0.68f, 0.74f, 0.81f, 1f);
        private static readonly Color Line = new Color(0.22f, 0.30f, 0.40f, 1f);

        private void EnsureStyles()
        {
            if (windowStyle != null) return;
            solidPixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            solidPixel.SetPixel(0, 0, Color.white);
            solidPixel.Apply();
            windowStyle = new GUIStyle(GUI.skin.window);
            windowStyle.normal.background = null;
            windowStyle.onNormal.background = null;
            windowStyle.border = new RectOffset(0, 0, 0, 0);
            windowStyle.padding = new RectOffset(0, 0, 0, 0);
            windowStyle.margin = new RectOffset(0, 0, 0, 0);
            windowStyle.normal.textColor = Color.clear;

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.normal.textColor = Text;
            labelStyle.fontSize = 12;
            labelStyle.alignment = TextAnchor.MiddleLeft;
            labelStyle.clipping = TextClipping.Clip;
            labelStyle.wordWrap = false;

            headingStyle = new GUIStyle(labelStyle);
            headingStyle.fontSize = 15;
            headingStyle.fontStyle = FontStyle.Bold;

            smallStyle = new GUIStyle(labelStyle);
            smallStyle.fontSize = 11;
            smallStyle.normal.textColor = Muted;

            subtleStyle = new GUIStyle(smallStyle);
            subtleStyle.fontSize = 10;

            boldStyle = new GUIStyle(labelStyle);
            boldStyle.fontStyle = FontStyle.Bold;
            boldStyle.fontSize = 13;

            centeredStyle = new GUIStyle(smallStyle);
            centeredStyle.alignment = TextAnchor.MiddleCenter;

            pillStyle = new GUIStyle(centeredStyle);
            pillStyle.fontStyle = FontStyle.Bold;
            pillStyle.normal.textColor = Accent;

            buttonStyle = new GUIStyle(centeredStyle);
            buttonStyle.normal.textColor = Text;
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Accent;
            buttonStyle.fontStyle = FontStyle.Bold;
            buttonStyle.fontSize = 12;
        }

        // All geometry here is absolute. This avoids IMGUI GUILayout overflow
        // and fixes the clipped mini panel seen in earlier screenshots.
        private void OnGUI()
        {
            if (!visible) return;
            UpdateCursor();
            EnsureStyles();
            // World projection and label generation are useful while inspecting
            // loot, but waste frame time during combat. Alt already means inspect.
            if (settings != null && settings.WorldMarkers.Value && overlayInputActive) DrawWorldMarkers();
            float uiScale = settings != null ? settings.Scale.Value : 1f;
            Matrix4x4 originalMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
            float logicalScreenWidth = Screen.width / uiScale;
            float logicalScreenHeight = Screen.height / uiScale;
            float w = iconOnly ? 70f : expanded ? 536f : 322f;
            float h = iconOnly ? 84f : expanded ? 518f : 184f;
            windowRect.width = Mathf.Min(w, Mathf.Max(70f, logicalScreenWidth - 12f));
            windowRect.height = Mathf.Min(h, Mathf.Max(84f, logicalScreenHeight - 12f));
            windowRect.x = Mathf.Clamp(windowRect.x, 0f, Mathf.Max(0f, logicalScreenWidth - windowRect.width));
            windowRect.y = Mathf.Clamp(windowRect.y, 0f, Mathf.Max(0f, logicalScreenHeight - windowRect.height));
            Color original = GUI.color;
            if (overlayInputActive)
            {
                windowRect = GUI.Window(WindowId, windowRect, DrawWindow, GUIContent.none, windowStyle);
            }
            else
            {
                // Avoid creating an IMGUI window while fighting. The passive group
                // draws the same HUD but has no window control that could catch input.
                GUI.BeginGroup(windowRect);
                DrawWindow(WindowId);
                GUI.EndGroup();
            }
            GUI.color = original;
            GUI.matrix = originalMatrix;
        }

        private void DrawWindow(int id)
        {
            float w = windowRect.width;
            float h = windowRect.height;
            if (iconOnly)
            {
                DrawIconOnly(w, h);
                return;
            }
            DrawFill(new Rect(0f, 0f, w, h), Background);
            DrawFill(new Rect(0f, 0f, w, 3f), Accent);
            DrawFill(new Rect(0f, 29f, w, 1f), Line);
            Label(new Rect(13f, 4f, w - 155f, 23f), "SANA  /  BUILD ADVISOR", smallStyle);
            Label(new Rect(w - 123f, 5f, 38f, 21f), ToggleKeyName(), subtleStyle);
            if (FlatButton(new Rect(w - 73f, 4f, 30f, 22f), "◉", Surface, Accent))
            {
                expanded = false;
                iconOnly = true;
            }
            if (FlatButton(new Rect(w - 39f, 4f, 30f, 22f), expanded ? "−" : "+", Surface, Accent))
            {
                expanded = !expanded;
                detailsScroll = Vector2.zero;
            }
            DrawPlayer(new Rect(10f, 34f, w - 20f, 48f));
            DrawRecommendation(new Rect(10f, 88f, w - 20f, expanded ? 76f : 84f), !expanded);
            if (expanded)
            {
                DrawModes(new Rect(10f, 174f, w - 20f, 32f));
                DrawTabs(new Rect(10f, 214f, w - 20f, 32f));
                DrawPage(new Rect(10f, 254f, w - 20f, h - 273f));
                Label(new Rect(13f, h - 18f, w - 26f, 13f), "Удерживай Alt: окно  •  Отпусти Alt: сразу в игру", subtleStyle);
            }
            DrawHoverTooltip(w, h);
            // Keep header buttons clickable while retaining drag functionality.
            if (overlayInputActive && (settings == null || !settings.LockWindow.Value)) GUI.DragWindow(new Rect(0f, 0f, w - 135f, 29f));
        }

        private void DrawIconOnly(float w, float h)
        {
            DrawFill(new Rect(0f, 0f, w, h), Background);
            DrawFill(new Rect(0f, 0f, w, 3f), Accent);
            Label(new Rect(7f, 3f, w - 14f, 12f), "SANA  •  " + ToggleKeyName(), subtleStyle);
            Rect icon = new Rect(9f, 20f, 52f, 52f);
            Sprite targetIcon = bestAvailable != null ? bestAvailable.Icon : bestOverall == null ? null : bestOverall.Icon;
            DrawItemIcon(targetIcon, icon, "?");
            if (overlayInputActive) GUI.Label(icon, new GUIContent("", bestAvailable != null ? bestAvailable.Name : bestOverall == null ? "Нет рекомендации" : bestOverall.Name), GUIStyle.none);
            if (overlayInputActive && GUI.Button(new Rect(7f, 18f, 56f, 58f), new GUIContent("", "Нажмите, чтобы открыть мини-панель"), GUIStyle.none))
                iconOnly = false;
            if (bestAvailable != null || bestOverall != null) Label(new Rect(28f, 60f, 36f, 16f), "+1", centeredStyle);
            if (overlayInputActive && (settings == null || !settings.LockWindow.Value)) GUI.DragWindow(new Rect(0f, 0f, w, 17f));
        }

        private string ToggleKeyName()
        {
            return settings == null ? "F8" : settings.ToggleKey.Value.ToString();
        }

        private void DrawPlayer(Rect r)
        {
            DrawFill(r, Surface);
            DrawPortrait(new Rect(r.x + 6f, r.y + 6f, 36f, 36f));
            float labelWidth = r.width - (expanded ? 160f : 101f);
            Label(new Rect(r.x + 49f, r.y + 4f, labelWidth, 21f), characterName, boldStyle);
            Label(new Rect(r.x + 49f, r.y + 25f, labelWidth + 20f, 17f),
                hasCharacter ? "Ур. " + level + "  •  HP " + hp : "Ожидается забег", smallStyle);
            if (!expanded)
            {
                string modeName = commandMode ? "CMD" : "LOOT";
                if (FlatButton(new Rect(r.x + r.width - 51f, r.y + 11f, 45f, 26f), modeName, Raised, Accent))
                {
                    automaticMode = false;
                    commandMode = !commandMode;
                    UpdateRecommendations();
                }
            }
            else
            {
                Label(new Rect(r.x + r.width - 108f, r.y + 9f, 101f, 17f), "Урон: " + damage, smallStyle);
                Label(new Rect(r.x + r.width - 108f, r.y + 27f, 101f, 17f), "Крит: " + critChance, smallStyle);
            }
        }

        private void DrawPortrait(Rect rect)
        {
            DrawFill(rect, Raised);
            if (portrait != null) GUI.DrawTexture(rect, portrait, ScaleMode.ScaleToFit, true);
            else Label(rect, "?", centeredStyle);
        }

        private void DrawRecommendation(Rect r, bool compact)
        {
            DrawFill(r, Raised);
            DrawFill(new Rect(r.x, r.y, 3f, r.height), Accent);
            Rect icon = new Rect(r.x + 12f, r.y + (r.height - 46f) * 0.5f, 46f, 46f);
            DrawItemIcon(bestAvailable != null ? bestAvailable.Icon : bestOverall == null ? null : bestOverall.Icon, icon, "?");
            if (overlayInputActive) GUI.Label(icon, new GUIContent("", bestAvailable != null ? bestAvailable.Name + ": " + bestAvailable.Reason
                : bestOverall == null ? "Нет рекомендации" : bestOverall.Name + ": " + bestOverall.Reason), GUIStyle.none);
            float tx = icon.xMax + 11f;
            float avail = r.xMax - tx - 8f;
            bool actual = bestAvailable != null;
            Label(new Rect(tx, r.y + 5f, avail, 17f), actual ? "ЛУЧШЕЕ ИЗ ЛУТА РЯДОМ" :
                commandMode ? "СЛЕДУЮЩАЯ ЦЕЛЬ • COMMAND" : "СЛЕДУЮЩАЯ ЦЕЛЬ • СЛУЧАЙНЫЙ ЛУТ", subtleStyle);
            Label(new Rect(tx, r.y + 20f, avail, 23f), actual ? bestAvailable.Name : bestOverall == null ? "Нет подтверждённого совета" : bestOverall.Name, boldStyle);
            string detail = actual
                ? bestAvailable.Source + " • " + bestAvailable.Distance.ToString("0") + " м"
                    + (bestAvailable.AffordabilityKnown ? bestAvailable.Affordable ? " • доступно" : " • не хватает золота" : "")
                    + " • " + EvidenceName(bestAvailable.Status)
                : bestOverall != null ? "x" + bestOverall.Count + "  →  x" + bestOverall.Goal + "  •  " + DisplayTier(bestOverall.Tier)
                    + (string.IsNullOrEmpty(bestOverall.StackState) ? "" : " • " + bestOverall.StackState)
                    : hasCharacter ? "Правила пока ограничены" : "Начните забег";
            Label(new Rect(tx, r.y + 43f, avail, 16f), detail, smallStyle);
            string qLine = bestEquipmentAdvice != null && bestEquipmentAdvice.AlreadyEquipped
                ? "Q: " + currentEquipmentName + " • оставить"
                : "Q: " + currentEquipmentName + " → " + recommendedEquipmentName;
            if (!string.IsNullOrEmpty(strategyNotice) && Time.unscaledTime < strategyNoticeUntil) qLine = strategyNotice;
            Label(new Rect(tx, r.y + 60f, avail, 16f), qLine, subtleStyle);
        }

        private void DrawModes(Rect r)
        {
            float bw = (r.width - 12f) / 3f;
            string[] labels = { "АВТО", "COMMAND", "ОБЫЧНЫЙ ЛУТ" };
            for (int i = 0; i < labels.Length; i++)
            {
                bool chosen = i == 0 ? automaticMode : !automaticMode && commandMode == (i == 1);
                if (FlatButton(new Rect(r.x + i * (bw + 6f), r.y, bw, r.height), labels[i], chosen ? Raised : Surface, chosen ? Accent : Muted))
                { automaticMode = i == 0; if (!automaticMode) commandMode = i == 1; ReadMode(); UpdateRecommendations(); SaveSettings(); }
            }
        }

        private void DrawTabs(Rect r)
        {
            string[] names = { "СТАТЫ", "НАВЫКИ", "ПРЕДМ.", "ПЛАН", "ЗАБЕГ", "КОМАНДА", "⚙" };
            float gap = 5f;
            float bw = (r.width - (names.Length - 1) * gap) / names.Length;
            for (int i = 0; i < names.Length; ++i)
            {
                if (FlatButton(new Rect(r.x + i * (bw + gap), r.y, bw, r.height), names[i], page == i ? Raised : Surface,
                    page == i ? Accent : Muted))
                {
                    page = i;
                    detailsScroll = Vector2.zero;
                    nextRefreshTime = 0f;
                    if (i == 5) nextTeamRefreshTime = 0f;
                    if (i == 3 || i == 4 || i == 5) nextThreatRefreshTime = 0f;
                    if (i == 4) nextLootRefreshTime = 0f;
                }
            }
        }

        private void DrawPage(Rect viewport)
        {
            float w = viewport.width - 18f;
            float contentHeight = page == 0 ? 620f : page == 1 ? 350f : page == 2 ? InventoryPageHeight(w)
                : page == 3 ? PlanPageHeight(w) : page == 4 ? 1290f : page == 5 ? TeamPageHeight() : 660f;
            if (overlayInputActive)
                detailsScroll = GUI.BeginScrollView(viewport, detailsScroll, new Rect(0f, 0f, w, contentHeight), false, true);
            else
            {
                GUI.BeginGroup(viewport);
                GUI.BeginGroup(new Rect(0f, -detailsScroll.y, w, contentHeight));
            }
            if (page == 0) DrawStatsPage(w);
            else if (page == 1) DrawSkillsPage(w);
            else if (page == 2) DrawInventoryPage(w);
            else if (page == 3) DrawPlanPage(w);
            else if (page == 4) DrawRunPage(w);
            else if (page == 5) DrawTeamPage(w);
            else DrawSettingsPage(w);
            if (overlayInputActive) GUI.EndScrollView();
            else
            {
                GUI.EndGroup();
                GUI.EndGroup();
            }
        }

        private void DrawStatsPage(float w)
        {
            SectionTitle("ХАРАКТЕРИСТИКИ ИЗ ИГРЫ", 0f, w);
            float cw = (w - 16f) / 3f;
            StatTile(new Rect(0f, 28f, cw, 61f), "HP", hp);
            StatTile(new Rect(cw + 8f, 28f, cw, 61f), "ЩИТ", shield);
            StatTile(new Rect(2f * (cw + 8f), 28f, cw, 61f), "БАРЬЕР", barrier);
            StatTile(new Rect(0f, 96f, cw, 61f), "УРОН (100%)", damage);
            StatTile(new Rect(cw + 8f, 96f, cw, 61f), "СКОРОСТЬ АТАКИ", attackSpeed);
            StatTile(new Rect(2f * (cw + 8f), 96f, cw, 61f), "БРОНЯ", armor);
            StatTile(new Rect(0f, 164f, cw, 61f), "СКОРОСТЬ БЕГА", moveSpeed);
            StatTile(new Rect(cw + 8f, 164f, cw, 61f), "КРИТ-СТАТ", critStat);
            StatTile(new Rect(2f * (cw + 8f), 164f, cw, 61f), "КРИТ-МНОЖ.", critMultiplier);
            SectionTitle("МОДЕЛЬ 100% УРОНА", 240f, w);
            StatTile(new Rect(0f, 270f, cw, 65f), "БЕЗ КРИТА", damage);
            StatTile(new Rect(cw + 8f, 270f, cw, 65f), "С КРИТОМ", critDamage);
            StatTile(new Rect(2f * (cw + 8f), 270f, cw, 65f), "СРЕДНИЙ", expectedDamage);
            WrappedNote(new Rect(0f, 346f, w, 92f), critDisclaimer + " Это не урон конкретного навыка или DPS.");
            SectionTitle("ЖИВУЧЕСТЬ", 451f, w);
            StatTile(new Rect(0f, 479f, cw, 61f), "РЕГЕНЕРАЦИЯ", regen);
            StatTile(new Rect(cw + 8f, 479f, cw, 61f), "ЭФФЕКТ БРОНИ", armorEffect);
            StatTile(new Rect(2f * (cw + 8f), 479f, cw, 61f), "УСЛОВНЫЙ EHP", effectiveHealth);
            Label(new Rect(0f, 548f, w, 18f), "OSP  •  " + oneShotProtection, smallStyle);
            Label(new Rect(0f, 570f, w * 0.5f - 4f, 18f), "БАФФЫ  •  " + activeBuffs, smallStyle);
            Label(new Rect(w * 0.5f + 4f, 570f, w * 0.5f - 4f, 18f), "ДЕБАФФЫ  •  " + activeDebuffs, smallStyle);
            Label(new Rect(0f, 592f, w, 24f), "EHP учитывает текущий барьер и броню только против обычного урона; некоторые типы урона обходят броню.", subtleStyle, true);
        }

        private void DrawSkillsPage(float w)
        {
            SectionTitle("ТЕКУЩИЙ НАБОР НАВЫКОВ", 0f, w);
            SkillTile(new Rect(0f, 28f, w, 64f), "PRIMARY", primaryInfo);
            SkillTile(new Rect(0f, 99f, w, 64f), "SECONDARY", secondaryInfo);
            SkillTile(new Rect(0f, 170f, w, 64f), "UTILITY", utilityInfo);
            SkillTile(new Rect(0f, 241f, w, 64f), "SPECIAL", specialInfo);
            Label(new Rect(0f, 312f, w, 16f), "Выбранные варианты обновляются во время забега.", smallStyle);
        }

        private void SkillTile(Rect rect, string slotName, SkillInfo info)
        {
            DrawFill(rect, Surface);
            DrawItemIcon(info == null ? null : info.Icon, new Rect(rect.x + 9f, rect.y + 9f, 46f, 46f), "?");
            Label(new Rect(rect.x + 65f, rect.y + 6f, rect.width - 74f, 17f), slotName, subtleStyle);
            Label(new Rect(rect.x + 65f, rect.y + 23f, rect.width - 74f, 23f), info == null ? "Нет данных" : info.Name, boldStyle);
            Label(new Rect(rect.x + 65f, rect.y + 46f, rect.width - 74f, 14f), VariantLabel(info), smallStyle);
        }

        private static string VariantLabel(SkillInfo info)
        {
            if (info == null) return "Слот недоступен";
            if (!info.FoundInFamily) return "Вариант не подтверждён";
            return info.VariantIndex == info.DefaultVariantIndex ? "Основной вариант" : "Альтернатива №" + (info.VariantIndex + 1);
        }

        private float InventoryPageHeight(float w)
        {
            int cols = Math.Max(1, (int)((w - 6f) / 67f));
            int rows = (inventoryItems.Count + cols - 1) / cols;
            return 68f + rows * 73f + (selectedItemId != null ? 91f : 0f);
        }

        private void DrawInventoryPage(float w)
        {
            SectionTitle("ИНВЕНТАРЬ", 0f, w);
            Label(new Rect(0f, 24f, w, 19f), inventoryStatus, smallStyle);
            if (!inventoryAvailable)
            {
                WrappedNote(new Rect(0f, 48f, w, 74f), "Инвентарь пока не прочитан. Иконки и стаки появятся после начала забега.");
                return;
            }
            int cols = Math.Max(1, (int)((w - 6f) / 67f));
            float cellWidth = (w - 6f) / cols;
            for (int i = 0; i < inventoryItems.Count; i++)
            {
                ItemEntry item = inventoryItems[i];
                float x = (i % cols) * cellWidth;
                float y = 53f + (i / cols) * 73f;
                Rect tile = new Rect(x, y, cellWidth - 6f, 68f);
                DrawFill(tile, selectedItemId == item.Id ? new Color(0.18f, 0.30f, 0.35f, 1f) : Surface);
                DrawFill(new Rect(x, y + 65f, tile.width, 3f), TierColor(item.Tier));
                float isz = Math.Min(tile.width - 10f, 46f);
                DrawItemIcon(item.Icon, new Rect(x + (tile.width - isz) / 2f, y + 5f, isz, isz), "?");
                Label(new Rect(x + 2f, y + 51f, tile.width - 4f, 14f), "x" + item.Count, centeredStyle);
                if (overlayInputActive && GUI.Button(tile, new GUIContent("", item.Name), GUIStyle.none)) selectedItemId = item.Id;
            }
            if (!string.IsNullOrEmpty(selectedItemId))
            {
                ItemEntry item = inventoryItems.Find(delegate(ItemEntry e) { return e.Id == selectedItemId; });
                if (item != null)
                {
                    float y = 55f + (inventoryItems.Count + cols - 1) / cols * 73f;
                    DrawFill(new Rect(0f, y, w, 78f), Raised);
                    Label(new Rect(9f, y + 6f, w - 20f, 21f), item.Name + "  x" + item.Count, boldStyle);
                    Label(new Rect(9f, y + 29f, w - 20f, 16f), "Категория: " + item.Tier, smallStyle);
                    Label(new Rect(9f, y + 48f, w - 20f, 16f), "ID: " + item.Id, smallStyle);
                }
            }
        }

        private void DrawPlanPage(float w)
        {
            SectionTitle("ПОДБОР ПРЕДМЕТОВ", 0f, w);
            Label(new Rect(0f, 28f, w, 17f), "ЦЕЛЬ БИЛДА", subtleStyle);
            float goalWidth = (w - 12f) / 3f;
            string[] goals = { "БАЛАНС", "УРОН", "ВЫЖИВАНИЕ", "МОБИЛЬНОСТЬ", "OP-БИЛД", "СТРАТЕГИЯ" };
            for (int i = 0; i < goals.Length; i++)
            {
                float gy = i < 3 ? 48f : 80f;
                int col = i % 3;
                if (FlatButton(new Rect(col * (goalWidth + 6f), gy, goalWidth, 28f), goals[i],
                    buildGoal == i ? Raised : Surface, buildGoal == i ? Accent : Muted))
                { buildGoal = i; UpdateRecommendations(); SaveSettings(); }
            }
            string readinessTitle = runReadiness == null || runReadiness.Difficulty <= 0d ? "ГОТОВНОСТЬ • НЕТ ДАННЫХ ЭТАПА"
                : "ГОТОВНОСТЬ К ЭТАПУ " + runReadiness.Stage + " • СЛОЖН. " + runReadiness.Difficulty.ToString("0.##") + "x • ЭВРИСТИКА";
            Label(new Rect(0f, 116f, w, 17f), readinessTitle, subtleStyle);
            DrawReadinessBars(0f, 136f, w, runReadiness);
            Label(new Rect(0f, 184f, w, 17f), "ПРОФИЛЬ УГРОЗЫ • " + encounterSummary, subtleStyle);
            DrawAdvancedBars(0f, 204f, w, advancedAssessment);
            Label(new Rect(0f, 320f, w, 17f), "СТИЛЬ РИСКА" + (settings != null && settings.AdaptiveRisk.Value
                ? " • АВТО → " + (effectiveRiskStyle == 0 ? "БЕЗОПАСНЫЙ" : effectiveRiskStyle == 2 ? "АГРЕССИВНЫЙ" : "БАЛАНС") : ""), subtleStyle);
            float bw = (w - 12f) / 3f;
            string[] labels = { "БЕЗОПАСНЫЙ", "БАЛАНС", "АГРЕССИВНЫЙ" };
            for (int i = 0; i < 3; i++)
            {
                if (FlatButton(new Rect(i * (bw + 6f), 340f, bw, 28f), labels[i],
                    playStyle == i ? Raised : Surface, playStyle == i ? Accent : Muted))
                {
                    playStyle = i;
                    UpdateRecommendations();
                    SaveSettings();
                }
            }
            string strategy = activeArchetype == null ? "Особая стратегия не распознана."
                : "Стратегия: " + activeArchetype.Name + (activeArchetype.Verified ? "" : " [ГИПОТЕЗА]")
                    + " [" + ArchetypeMatcher.PhaseName(activeArchetype.Lifecycle) + "]. " + activeArchetype.NextStep;
            WrappedNote(new Rect(0f, 378f, w, 98f), "Цель: " + GoalName(buildGoal) + ". " + modeEvidence + ". " + (commandMode
                ? "План следующего выбора по редкости. +1 — следующий шаг, а не оптимальный итоговый стак."
                : "Главный HUD показывает лучший оценённый предмет, если он реально виден рядом; иначе практичную цель. Красные — долгосрочный план, а не обещание дропа.") + " " + strategy);
            float y = 486f;
            DrawFill(new Rect(0f, y, w, 56f), Raised);
            Label(new Rect(10f, y + 6f, w - 20f, 18f), "СЕЙЧАС: " + (bestAvailable != null ? bestAvailable.Name : bestOverall == null ? "нет цели" : bestOverall.Name), boldStyle);
            Label(new Rect(10f, y + 30f, w - 20f, 17f), "ПОТОМ: " + (bestLongTerm == null ? "нет долгосрочной цели" : bestLongTerm.Name), smallStyle);
            y += 66f;
            SectionTitle("АНАЛИЗ БИЛДА", y, w);
            y += 27f;
            DrawFill(new Rect(0f, y, w, 142f), Surface);
            DecisionReport dr = decisionReport ?? new DecisionReport();
            Label(new Rect(10f, y + 6f, w - 20f, 18f), "DNA: " + dr.BuildDna + " • роль: " + dr.TeamRole + " • лут " + dr.LootFit, boldStyle);
            Label(new Rect(10f, y + 29f, w - 20f, 32f), dr.Synergy, smallStyle, true);
            Label(new Rect(10f, y + 63f, w - 20f, 32f), dr.Overinvestment, smallStyle, true);
            Label(new Rect(10f, y + 97f, w - 20f, 18f), dr.Teleporter + " • " + nextStageForecast, smallStyle);
            Label(new Rect(10f, y + 118f, w - 20f, 18f), nearbyFacilityAdvice, subtleStyle);
            y += 152f;
            DrawNearbyPlan(w, ref y);
            DrawTierPlan(w, ref y, "БЕЛЫЕ", "Tier1");
            DrawTierPlan(w, ref y, "ЗЕЛЁНЫЕ", "Tier2");
            DrawTierPlan(w, ref y, "КРАСНЫЕ", "Tier3");
            DrawSpecialPlan(w, ref y);
            SectionTitle("Q-СНАРЯЖЕНИЕ", y, w);
            y += 28f;
            DrawEquipmentPlan(new Rect(0f, y, w, 125f));
            y += 135f;
            WrappedNote(new Rect(0f, y, w, 90f), "Полосы готовности сравнивают реальные статы с эвристической целью текущего этапа и коэффициента сложности. Это ориентир, а не гарантия выживания или времени убийства. После подбора план пересчитывается.");
        }

        private float AdviceHeight(Recommendation rec, float w)
        {
            string text = rec == null ? "Нет доступного совета в проверенном наборе." : rec.Reason
                + (string.IsNullOrEmpty(rec.ImpactText) ? "" : " " + rec.ImpactText);
            var wrapped = new GUIStyle(smallStyle);
            wrapped.wordWrap = true;
            return 80f + wrapped.CalcHeight(new GUIContent(text), Math.Max(40f, w - 18f));
        }

        private float PlanPageHeight(float w)
        {
            float height = 1012f;
            int nearby = Math.Min(3, nearbyOffers.Count);
            height += nearby == 0 ? 62f : nearby * 106f;
            foreach (string tier in new string[] { "Tier1", "Tier2", "Tier3" })
            {
                int count = 0;
                foreach (Recommendation r in rankedAdvice)
                    if (r.Tier == tier && count++ < 2) height += AdviceHeight(r, w) + 10f;
                if (count == 0) height += AdviceHeight(null, w) + 10f;
            }
            int special = 0;
            foreach (Recommendation r in rankedAdvice)
                if (IsSpecialTier(r.Tier) && special++ < 3) height += AdviceHeight(r, w) + 10f;
            if (special == 0) height += 42f;
            return height;
        }

        private void DrawNearbyPlan(float w, ref float y)
        {
            SectionTitle("РЕАЛЬНО ДОСТУПНО РЯДОМ", y, w);
            y += 27f;
            Label(new Rect(0f, y, w, 18f), nearbyStatus, smallStyle);
            y += 25f;
            int shown = Math.Min(3, nearbyOffers.Count);
            if (shown == 0)
            {
                WrappedNote(new Rect(0f, y, w, 47f), "Advisor не обещает случайный дроп: здесь появятся раскрытые магазины, принтеры, предметы на земле и Command-эссенции в радиусе 70 м.");
                y += 57f;
                return;
            }
            for (int i = 0; i < shown; i++)
            {
                NearbyOffer offer = nearbyOffers[i];
                DrawFill(new Rect(0f, y, w, 96f), i == 0 && offer.Status != EvidenceStatus.Unknown ? Raised : Surface);
                DrawItemIcon(offer.Icon, new Rect(8f, y + 10f, 48f, 48f), offer.IsEquipment ? "Q" : "?");
                Label(new Rect(67f, y + 6f, w - 151f, 21f), offer.Name, boldStyle);
                bool reserved = offer.Id == reservedItemId;
                if (FlatButton(new Rect(w - 76f, y + 5f, 70f, 22f), reserved ? "РЕЗЕРВ ✓" : "РЕЗЕРВ", reserved ? Raised : Background,
                    reserved ? Accent : Muted))
                {
                    if (reserved) { reservedItemId = reservedItemName = ""; }
                    else { reservedItemId = offer.Id; reservedItemName = offer.Name; }
                }
                string price = string.IsNullOrEmpty(offer.CostText) ? "" : " • цена " + offer.CostText;
                string afford = offer.AffordabilityKnown ? offer.Affordable ? " • можно купить" : " • пока дорого" : "";
                Label(new Rect(67f, y + 28f, w - 75f, 17f), offer.Source + " • " + offer.Distance.ToString("0") + " м" + price + afford + " • " + EvidenceName(offer.Status), smallStyle);
                string line = offer.Status == EvidenceStatus.Unknown ? "Виден рядом, но правило оценки ещё не добавлено."
                    : offer.IsEquipment ? "Q-снаряжение: сравнить с текущим."
                    : offer.CountKnown ? "Текущий стак x" + offer.Count + " → x" + offer.Goal + (offer.IsTierChoice ? " • выбор Command" : "")
                    : "Редкий предмет доступен сейчас • оценка по редкости";
                Label(new Rect(67f, y + 49f, w - 75f, 20f), line, subtleStyle, true);
                if (!string.IsNullOrEmpty(offer.ImpactText)) Label(new Rect(67f, y + 69f, w - 75f, 20f), offer.ImpactText, subtleStyle, true);
                y += 106f;
            }
        }

        private static string GoalName(int goal)
        {
            string[] names = { "сбалансированный", "максимум урона", "максимум выживаемости", "максимум мобильности", "OP / сильные синергии", "выбранная стратегия" };
            return goal >= 0 && goal < names.Length ? names[goal] : "неизвестно";
        }

        private void DrawReadinessBars(float x, float y, float w, RunReadiness readiness)
        {
            readiness = readiness ?? new RunReadiness();
            float gap = 8f;
            float cell = (w - gap) * 0.5f;
            DrawReadinessMetric(new Rect(x, y, cell, 18f), "УРОН", readiness.Damage);
            DrawReadinessMetric(new Rect(x + cell + gap, y, cell, 18f), "HP", readiness.Health);
            DrawReadinessMetric(new Rect(x, y + 22f, cell, 18f), "ЗАЩ", readiness.Defense);
            DrawReadinessMetric(new Rect(x + cell + gap, y + 22f, cell, 18f), "ДВИЖ", readiness.Mobility);
        }

        private void DrawAdvancedBars(float x, float y, float w, AdvancedAssessment assessment)
        {
            assessment = assessment ?? new AdvancedAssessment();
            DrawReadinessMetric(new Rect(x, y, w, 18f), "1-Й", assessment.Opener);
            DrawReadinessMetric(new Rect(x, y + 22f, w, 18f), "ТЕМП", assessment.Tempo);
            DrawReadinessMetric(new Rect(x, y + 44f, w, 18f), "УДАР", assessment.Burst);
            DrawReadinessMetric(new Rect(x, y + 66f, w, 18f), "БОЙ", assessment.Sustain);
            DrawReadinessMetric(new Rect(x, y + 88f, w, 18f), "ЗАЧ", assessment.Clear);
        }

        private void DrawReadinessMetric(Rect r, string name, ReadinessMetric metric)
        {
            metric = metric ?? new ReadinessMetric();
            int shown = metric.Percent < 0 ? 0 : metric.Percent;
            Color color = metric.Percent < 0 ? Muted : metric.Percent < 70 ? new Color(0.96f, 0.39f, 0.40f, 1f)
                : metric.Percent < 95 ? new Color(0.96f, 0.72f, 0.28f, 1f)
                : metric.Percent < 120 ? new Color(0.36f, 0.88f, 0.58f, 1f) : Accent;
            string value = metric.Percent < 0 ? "—" : metric.Percent + "%";
            Label(new Rect(r.x, r.y, 72f, r.height), name + " " + value, subtleStyle);
            Rect track = new Rect(r.x + 72f, r.y + 5f, Math.Max(12f, r.width - 72f), 8f);
            DrawFill(track, Surface);
            DrawFill(new Rect(track.x, track.y, track.width * Mathf.Clamp(shown, 0, 100) / 100f, track.height), color);
            Label(new Rect(track.x, r.y, track.width, r.height), metric.Status, centeredStyle);
            if (overlayInputActive) GUI.Label(r, new GUIContent("", metric.Detail), GUIStyle.none);
        }

        private void DrawSettingsPage(float w)
        {
            SectionTitle("НАСТРОЙКИ", 0f, w);
            if (settings == null) { WrappedNote(new Rect(0f, 30f, w, 70f), "Конфигурация недоступна."); return; }
            SettingStepper(w, 31f, "МАСШТАБ", settings.Scale.Value.ToString("0.00"), delegate(float d) {
                settings.Scale.Value = Mathf.Clamp(settings.Scale.Value + d * 0.05f, 0.75f, 1.50f); });
            SettingStepper(w, 82f, "ПРОЗРАЧНОСТЬ", settings.Opacity.Value.ToString("0.00"), delegate(float d) {
                settings.Opacity.Value = Mathf.Clamp(settings.Opacity.Value + d * 0.05f, 0.45f, 1f); });
            Label(new Rect(0f, 136f, w, 18f), "ГОРЯЧАЯ КЛАВИША: " + settings.ToggleKey.Value, subtleStyle);
            if (FlatButton(new Rect(0f, 158f, w, 30f), "СМЕНИТЬ F8 → F9 → F10", Surface, Text))
            {
                settings.ToggleKey.Value = settings.ToggleKey.Value == KeyCode.F8 ? KeyCode.F9
                    : settings.ToggleKey.Value == KeyCode.F9 ? KeyCode.F10 : KeyCode.F8;
            }
            ToggleSetting(w, 202f, "ПОДРОБНЫЕ ПРИЧИНЫ", settings.DetailedReasons);
            ToggleSetting(w, 240f, "ЭКСПЕРИМЕНТАЛЬНЫЕ СТРАТЕГИИ", settings.ExperimentalAdvice);
            ToggleSetting(w, 278f, "УВЕДОМЛЕНИЯ О СИНЕРГИЯХ", settings.SynergyNotifications);
            ToggleSetting(w, 316f, "ЗАКРЕПИТЬ ПОЗИЦИЮ ОКНА", settings.LockWindow);
            ToggleSetting(w, 354f, "МЕТКИ ЛУТА В МИРЕ", settings.WorldMarkers);
            ToggleSetting(w, 392f, "АДАПТИВНЫЙ СТИЛЬ РИСКА", settings.AdaptiveRisk);
            ToggleSetting(w, 430f, "ЭКОНОМНЫЙ РЕЖИМ", settings.PerformanceMode);
            if (FlatButton(new Rect(0f, 478f, w, 31f), "СБРОСИТЬ ПОЗИЦИЮ ОКНА", Surface, Text))
            { windowRect.x = 42f; windowRect.y = 65f; SaveSettings(); }
            WrappedNote(new Rect(0f, 526f, w, 112f), "Экономный режим обновляет билд по изменениям, а лут, угрозы и команду — только при просмотре с Alt. Настройки сохраняются в BepInEx/config/local.sana.ror2.buildoverlay.cfg. Метки показывают только раскрытый клиенту лут.");
        }

        private void SettingStepper(float w, float y, string name, string value, Action<float> change)
        {
            Label(new Rect(0f, y, w - 130f, 28f), name + ": " + value, boldStyle);
            if (FlatButton(new Rect(w - 124f, y, 58f, 28f), "−", Surface, Accent)) change(-1f);
            if (FlatButton(new Rect(w - 60f, y, 58f, 28f), "+", Surface, Accent)) change(1f);
        }

        private void ToggleSetting(float w, float y, string name, BepInEx.Configuration.ConfigEntry<bool> entry)
        {
            if (FlatButton(new Rect(0f, y, w, 30f), name + ": " + (entry.Value ? "ВКЛ" : "ВЫКЛ"), entry.Value ? Raised : Surface, entry.Value ? Accent : Muted))
                entry.Value = !entry.Value;
        }

        private void DrawTeamPage(float w)
        {
            SectionTitle("КОМАНДА", 0f, w);
            Label(new Rect(0f, 29f, w, 18f), teamStatus, smallStyle);
            string offeredName = bestAvailable != null && !bestAvailable.IsEquipment ? bestAvailable.Name : bestOverall == null ? null : bestOverall.Name;
            string allocation = teamDistribution == null ? "Нет подтверждённого получателя для текущей доступной цели."
                : (string.IsNullOrEmpty(offeredName) ? teamDistribution.ItemId : offeredName)
                    + " → " + teamDistribution.UserName
                    + (teamDistribution.TargetPercent < 0 ? "" : " • " + teamDistribution.TargetArea + " " + teamDistribution.TargetPercent + "%")
                    + (teamDistribution.OverallPercent < 0 ? "" : " • итог " + teamDistribution.OverallPercent + "%");
            if (!string.IsNullOrEmpty(reservedItemName)) allocation += " • локальный резерв: " + reservedItemName;
            DrawFill(new Rect(0f, 55f, w, 42f), Raised);
            Label(new Rect(10f, 58f, w - 20f, 15f), "КОМУ ОТДАТЬ ТЕКУЩУЮ ЦЕЛЬ", subtleStyle);
            Label(new Rect(10f, 73f, w - 20f, 21f), allocation, boldStyle);
            DrawFill(new Rect(0f, 104f, w, 38f), Surface);
            string weakest = weakestTeamMember == null ? "СЛАБЕЕ ВСЕХ: недостаточно синхронизированных данных"
                : "СЛАБЕЕ ВСЕХ: " + weakestTeamMember.UserName + " • итог " + weakestTeamMember.OverallReadiness
                    + "% • просадка: " + weakestTeamMember.WeakestArea + " " + weakestTeamMember.WeakestPercent + "%";
            Label(new Rect(10f, 112f, w - 20f, 21f), weakest, boldStyle);
            Label(new Rect(0f, 149f, w, 16f), teamGapSummary, boldStyle);
            Label(new Rect(0f, 169f, w, 16f), "ПОЛОСЫ = ГОТОВНОСТЬ К ТЕКУЩЕМУ ЭТАПУ • ЭВРИСТИКА", subtleStyle);
            float y = 192f;
            for (int i = 0; i < teamMembers.Count; i++)
            {
                TeamMemberSnapshot member = teamMembers[i];
                bool isWeakest = object.ReferenceEquals(member, weakestTeamMember);
                DrawFill(new Rect(0f, y, w, 204f), isWeakest ? new Color(0.23f, 0.10f, 0.13f, 0.98f) : member.IsLocal ? Raised : Surface);
                Label(new Rect(10f, y + 6f, w - 20f, 21f), member.UserName + (member.IsLocal ? "  •  ВЫ" : "")
                    + (isWeakest ? "  •  СЛАБЕЕ ВСЕХ" : ""), boldStyle);
                Label(new Rect(10f, y + 28f, w - 20f, 17f), member.CharacterName + "  •  "
                    + (member.Alive ? "жив" : member.BodyVisible ? "мёртв" : "тело не видно") + " • " + member.Role, smallStyle);
                string data = member.InventoryVisible && member.OverallReadiness >= 0
                    ? "Итог: " + member.OverallReadiness + "% • слабое: " + member.WeakestArea + " " + member.WeakestPercent + "%"
                    : member.InventoryVisible ? "Оценка этапа недоступна" : "Инвентарь недоступен";
                if (member.BestAdvice != null) data += " • цель: " + member.BestAdvice.Id;
                Label(new Rect(10f, y + 49f, w - 20f, 22f), data, subtleStyle, true);
                if (member.InventoryVisible) DrawReadinessBars(10f, y + 76f, w - 20f, member.Readiness);
                if (member.InventoryVisible)
                {
                    AdvancedAssessment a = member.Advanced ?? new AdvancedAssessment();
                    float gap = 6f, cell = (w - 20f - gap * 2f) / 3f;
                    DrawReadinessMetric(new Rect(10f, y + 122f, cell, 18f), "1-Й", a.Opener);
                    DrawReadinessMetric(new Rect(10f + cell + gap, y + 122f, cell, 18f), "ТЕМП", a.Tempo);
                    DrawReadinessMetric(new Rect(10f + (cell + gap) * 2f, y + 122f, cell, 18f), "ЗАЧ", a.Clear);
                    float half = (w - 20f - gap) / 2f;
                    DrawReadinessMetric(new Rect(10f, y + 144f, half, 18f), "УДАР", a.Burst);
                    DrawReadinessMetric(new Rect(10f + half + gap, y + 144f, half, 18f), "БОЙ", a.Sustain);
                    Label(new Rect(10f, y + 167f, w - 20f, 31f), a.Priority, subtleStyle, true);
                }
                y += 213f;
            }
            if (teamMembers.Count == 0) WrappedNote(new Rect(0f, y, w, 58f), "Игроки не обнаружены. Начни забег; без видимого инвентаря полосы не строятся.");
            y += teamMembers.Count == 0 ? 66f : 4f;
            Label(new Rect(0f, y, w, 32f), "Удалённые данные зависят от того, что синхронизировала игра.", subtleStyle, true);
        }

        private float TeamPageHeight()
        {
            return 270f + Math.Max(1, teamMembers.Count) * 213f;
        }

        private void DrawTierPlan(float w, ref float y, string name, string tier)
        {
            int count = 0;
            foreach (Recommendation r in rankedAdvice)
            {
                if (r.Tier != tier || count >= 2) continue;
                float height = AdviceHeight(r, w);
                string first = tier == "Tier3" ? " • ДОЛГОСРОЧНО" : " • ПРИ ВЫБОРЕ";
                RecommendationTile(new Rect(0f, y, w, height), name + (count == 0 ? first : " • АЛЬТЕРНАТИВА"), r);
                y += height + 10f;
                count++;
            }
            if (count == 0)
            {
                float height = AdviceHeight(null, w);
                RecommendationTile(new Rect(0f, y, w, height), name, null);
                y += height + 10f;
            }
        }

        private void DrawSpecialPlan(float w, ref float y)
        {
            int count = 0;
            foreach (Recommendation r in rankedAdvice)
            {
                if (!IsSpecialTier(r.Tier) || count >= 3) continue;
                float height = AdviceHeight(r, w);
                RecommendationTile(new Rect(0f, y, w, height), "ОСОБЫЙ ТИР • " + DisplayTier(r.Tier), r);
                y += height + 10f;
                count++;
            }
            if (count == 0)
            {
                Label(new Rect(0f, y, w, 26f), "Пустота / DLC: поддержанный доступный вариант сейчас не найден.", smallStyle);
                y += 36f;
            }
        }

        private static bool IsSpecialTier(string tier)
        {
            return !string.IsNullOrEmpty(tier) && (tier.IndexOf("Void", StringComparison.OrdinalIgnoreCase) >= 0
                || tier == "Hex" || tier == "Cursed" || tier == "Page" || tier == "FoodTier" || tier == "AssignedAtRuntime");
        }

        private void DrawEquipmentPlan(Rect r)
        {
            DrawFill(r, Surface);
            DrawItemIcon(recommendedEquipmentIcon, new Rect(r.x + 9f, r.y + 9f, 50f, 50f), "Q");
            Label(new Rect(r.x + 70f, r.y + 6f, r.width - 79f, 17f), "СЕЙЧАС: " + currentEquipmentName, subtleStyle);
            Label(new Rect(r.x + 70f, r.y + 25f, r.width - 79f, 23f), recommendedEquipmentName, boldStyle);
            Label(new Rect(r.x + 70f, r.y + 49f, r.width - 79f, 16f),
                bestEquipmentAdvice != null && bestEquipmentAdvice.AlreadyEquipped ? "Оставить текущее" : "Если встретишь — сравнить", smallStyle);
            Label(new Rect(r.x + 9f, r.y + 72f, r.width - 18f, r.height - 79f), recommendedEquipmentReason, smallStyle, true);
        }

        private void DrawRunPage(float w)
        {
            SectionTitle("КОНТЕКСТ ТЕКУЩЕГО ЗАБЕГА", 0f, w);
            float cw = (w - 16f) / 3f;
            StatTile(new Rect(0f, 29f, cw, 57f), "ЭТАПОВ ПРОЙДЕНО", stageCompleted);
            StatTile(new Rect(cw + 8f, 29f, cw, 57f), "ВРЕМЯ ЗАБЕГА", runTime);
            StatTile(new Rect(2f * (cw + 8f), 29f, cw, 57f), "ИГРОКОВ", partyCount);
            StatTile(new Rect(0f, 93f, cw, 57f), "СЛОЖНОСТЬ", difficultyName);
            StatTile(new Rect(cw + 8f, 93f, cw, 57f), "КОЭФ. СЛОЖНОСТИ", difficultyCoefficient);
            StatTile(new Rect(2f * (cw + 8f), 93f, cw, 57f), "МОИ ДЕНЬГИ", currentMoney);
            Label(new Rect(0f, 158f, w, 18f), "Игроков: " + partyEvidence, smallStyle);
            SectionTitle("ЭКОНОМИКА КОМАНДЫ", 185f, w);
            StatTile(new Rect(0f, 214f, (w - 8f) * 0.5f, 62f), "БЮДЖЕТ ИНТЕРАКТИВНЫХ ОБЪЕКТОВ", interactableBudget);
            StatTile(new Rect((w + 8f) * 0.5f, 214f, (w - 8f) * 0.5f, 62f), "УСЛОВНО НА ИГРОКА", approximateShare);
            WrappedNote(new Rect(0f, 285f, w, 53f), "Оценка только для стандартного Scene Director: +50% бюджета за дополнительного игрока. Это не число сундуков и не гарантия лута.");
            SectionTitle("ЧТО НЕ ЗАКРЫТО ПО НАШИМ ПРАВИЛАМ", 351f, w);
            Label(new Rect(0f, 379f, w, 19f), "МОБИЛЬНОСТЬ  •  " + mobilityCoverage, smallStyle);
            Label(new Rect(0f, 401f, w, 19f), "ЗАЩИТА  •  " + defenseCoverage, smallStyle);
            Label(new Rect(0f, 423f, w, 19f), "АТАКА  •  " + offenseCoverage, smallStyle);
            Label(new Rect(0f, 445f, w, 19f), "ПОДДЕРЖКА  •  " + supportCoverage, smallStyle);
            WrappedNote(new Rect(0f, 475f, w, 90f), nextStageWarning + " Модель использует только поддержанные механики и открытые клиенту значения; неизвестные эффекты не превращаются в точные цифры.");
            SectionTitle("АКТИВНЫЕ АРТЕФАКТЫ", 579f, w);
            WrappedNote(new Rect(0f, 606f, w, 57f), activeArtifacts);
            SectionTitle("ТЕКУЩАЯ УГРОЗА", 676f, w);
            WrappedNote(new Rect(0f, 703f, w, 58f), encounterSummary + ". "
                + (advancedAssessment == null ? "Оценка ещё не построена." : advancedAssessment.Priority)
                + (decisionReport == null ? "" : " " + decisionReport.Teleporter) + " " + dangerRadar);
            SectionTitle("РАЗБОР СМЕРТЕЙ • " + deathReviewCount, 774f, w);
            WrappedNote(new Rect(0f, 801f, w, 55f), lastDeathReview);
            SectionTitle("ПРОВЕРКА МУЛЬТИПЛЕЕРА", 869f, w);
            WrappedNote(new Rect(0f, 895f, w, 70f), remoteVisibility + " Сравни показания у хоста и клиента; неполные удалённые данные остаются помечены как неизвестные.");
            SectionTitle("ГРАФИК РАЗВИТИЯ ЗАБЕГА", 979f, w);
            DrawTrendChart(new Rect(0f, 1007f, w, 112f));
            SectionTitle("АВТООТЧЁТ", 1132f, w);
            WrappedNote(new Rect(0f, 1159f, w, 62f), runReport);
            SectionTitle("ОБМЕНЫ РЯДОМ", 1234f, w);
            Label(new Rect(0f, 1261f, w, 22f), nearbyFacilityAdvice, smallStyle, true);
        }

        private void DrawTrendChart(Rect r)
        {
            DrawFill(r, Surface);
            if (runTrend.Count == 0)
            {
                Label(new Rect(r.x + 10f, r.y + 10f, r.width - 20f, 24f), "Точки появятся во время активного забега.", smallStyle);
                return;
            }
            int shown = Math.Min(20, runTrend.Count);
            float gap = 3f;
            float width = (r.width - 18f - gap * (shown - 1)) / shown;
            for (int i = 0; i < shown; i++)
            {
                RunTrendPoint p = runTrend[runTrend.Count - shown + i];
                float ratio = Mathf.Clamp(p.Overall, 0, 150) / 150f;
                float height = ratio * 72f;
                float x = r.x + 9f + i * (width + gap);
                float y = r.y + 82f - height;
                Color color = p.Overall < 70 ? new Color(0.96f, 0.39f, 0.40f, 1f)
                    : p.Overall < 95 ? new Color(0.96f, 0.72f, 0.28f, 1f) : Accent;
                DrawFill(new Rect(x, y, width, height), color);
                if (i == shown - 1 || i == 0) Label(new Rect(x - 6f, r.y + 86f, width + 12f, 16f), "Э" + p.Stage, centeredStyle);
            }
            Label(new Rect(r.x + 9f, r.y + 2f, r.width - 18f, 17f), "ОБЩАЯ ГОТОВНОСТЬ • 0–150%", subtleStyle);
        }

        private void RecommendationTile(Rect r, string tier, Recommendation rec)
        {
            DrawFill(r, Surface);
            DrawItemIcon(rec == null ? null : rec.Icon, new Rect(r.x + 8f, r.y + 9f, 50f, 50f), "?");
            Label(new Rect(r.x + 70f, r.y + 6f, r.width - 78f, 18f), tier, subtleStyle);
            Label(new Rect(r.x + 70f, r.y + 24f, r.width - 78f, 22f), rec == null ? "Нет рекомендации" : rec.Name, boldStyle);
            Label(new Rect(r.x + 70f, r.y + 48f, r.width - 78f, 17f), rec == null ? "" : "x" + rec.Count + " → x" + rec.Goal
                + (string.IsNullOrEmpty(rec.StackState) ? "" : " • " + rec.StackState), smallStyle);
            string reason = rec == null ? "Для этой редкости пока нет проверенного приоритета."
                : "[" + EvidenceName(rec.Status) + "] " + rec.Reason
                    + (string.IsNullOrEmpty(rec.ImpactText) ? "" : " " + rec.ImpactText);
            if (settings != null && !settings.DetailedReasons.Value && rec != null) reason = "[" + EvidenceName(rec.Status) + "] Подробности отключены в настройках.";
            Label(new Rect(r.x + 9f, r.y + 71f, r.width - 18f, r.height - 76f), reason, smallStyle, true);
        }

        private static string EvidenceName(EvidenceStatus status)
        {
            switch (status)
            {
                case EvidenceStatus.Verified: return "ПРОВЕРЕНО";
                case EvidenceStatus.Calculated: return "РАСЧЁТ";
                case EvidenceStatus.Estimated: return "ОЦЕНКА";
                case EvidenceStatus.Experimental: return "ЭКСПЕРИМЕНТ";
                default: return "НЕИЗВЕСТНО";
            }
        }

        private void SectionTitle(string text, float y, float w)
        {
            DrawFill(new Rect(0f, y + 5f, 3f, 14f), Accent);
            Label(new Rect(11f, y, w - 11f, 23f), text, boldStyle);
        }

        private void StatTile(Rect r, string title, string value)
        {
            DrawFill(r, Surface);
            Label(new Rect(r.x + 9f, r.y + 6f, r.width - 18f, 17f), title, subtleStyle);
            Label(new Rect(r.x + 9f, r.y + 27f, r.width - 18f, 29f), value, headingStyle);
        }

        private void WrappedNote(Rect r, string text)
        {
            DrawFill(r, Surface);
            Label(new Rect(r.x + 10f, r.y + 5f, r.width - 20f, r.height - 10f), text, smallStyle, true);
        }

        private bool FlatButton(Rect r, string text, Color backColor, Color textColor)
        {
            DrawFill(r, backColor);
            Color old = buttonStyle.normal.textColor;
            buttonStyle.normal.textColor = textColor;
            bool clicked;
            if (overlayInputActive) clicked = GUI.Button(r, text, buttonStyle);
            else
            {
                GUI.Label(r, text, buttonStyle);
                clicked = false;
            }
            buttonStyle.normal.textColor = old;
            return clicked;
        }

        private void Label(Rect r, string value, GUIStyle style)
        {
            GUI.Label(r, value ?? "—", style);
        }

        private void Label(Rect r, string value, GUIStyle style, bool wrap)
        {
            bool prior = style.wordWrap;
            TextClipping priorClip = style.clipping;
            style.wordWrap = wrap;
            style.clipping = TextClipping.Clip;
            GUI.Label(r, value ?? "—", style);
            style.wordWrap = prior;
            style.clipping = priorClip;
        }

        private void DrawFill(Rect rect, Color color)
        {
            Color old = GUI.color;
            if (settings != null) color.a *= settings.Opacity.Value;
            GUI.color = color;
            GUI.DrawTexture(rect, solidPixel);
            GUI.color = old;
        }

        private void DrawHoverTooltip(float windowWidth, float windowHeight)
        {
            if (!overlayInputActive || string.IsNullOrEmpty(GUI.tooltip)) return;
            var tooltipStyle = new GUIStyle(smallStyle);
            tooltipStyle.wordWrap = true;
            tooltipStyle.clipping = TextClipping.Overflow;
            float width = Mathf.Min(330f, windowWidth - 16f);
            float height = Mathf.Min(82f, tooltipStyle.CalcHeight(new GUIContent(GUI.tooltip), width - 16f) + 14f);
            Vector2 mouse = Event.current.mousePosition;
            float x = Mathf.Clamp(mouse.x + 14f, 8f, windowWidth - width - 8f);
            float y = Mathf.Clamp(mouse.y + 18f, 8f, windowHeight - height - 8f);
            DrawFill(new Rect(x, y, width, height), new Color(0.025f, 0.035f, 0.055f, 0.99f));
            Label(new Rect(x + 8f, y + 7f, width - 16f, height - 12f), GUI.tooltip, tooltipStyle, true);
        }

        private void DrawWorldMarkers()
        {
            if (nearbyOffers == null || nearbyOffers.Count == 0) return;
            Camera camera = Camera.main;
            if (camera == null) return;
            int shown = 0;
            for (int i = 0; i < nearbyOffers.Count && shown < 3; i++)
            {
                NearbyOffer offer = nearbyOffers[i];
                if (offer == null || offer.Status == EvidenceStatus.Unknown || offer.Position == Vector3.zero) continue;
                Vector3 point = camera.WorldToScreenPoint(offer.Position + Vector3.up * 1.2f);
                if (point.z <= 0f) continue;
                float x = point.x - 82f;
                float y = Screen.height - point.y - 18f;
                if (x < -160f || x > Screen.width || y < -40f || y > Screen.height) continue;
                Color marker = offer == bestAvailable ? new Color(0.10f, 0.38f, 0.32f, 0.92f)
                    : offer.Score >= 80d ? new Color(0.14f, 0.28f, 0.34f, 0.90f) : new Color(0.10f, 0.13f, 0.18f, 0.86f);
                Rect rect = new Rect(x, y, 164f, 36f);
                DrawFill(rect, marker);
                Label(new Rect(x + 5f, y + 2f, 154f, 16f), offer.Name, centeredStyle);
                string sub = offer.Distance.ToString("0") + " м" + (offer == bestAvailable ? " • ЛУЧШИЙ" : "")
                    + (offer.Id == reservedItemId ? " • РЕЗЕРВ" : "")
                    + (offer.AffordabilityKnown && !offer.Affordable ? " • ДОРОГО" : "");
                Label(new Rect(x + 5f, y + 18f, 154f, 14f), sub, subtleStyle);
                shown++;
            }
        }

        private void DrawItemIcon(Sprite icon, Rect rect, string fallback)
        {
            DrawFill(rect, new Color(0.18f, 0.23f, 0.29f, 1f));
            if (icon == null || icon.texture == null)
            {
                Label(rect, fallback, centeredStyle);
                return;
            }
            // Packed/tightly packed atlases can reject textureRect. Never let
            // a single unsupported DLC sprite break the entire overlay.
            try
            {
                Texture tex = icon.texture;
                Rect uv;
                try { uv = icon.textureRect; }
                catch { uv = icon.rect; }
                uv = new Rect(uv.x / tex.width, uv.y / tex.height, uv.width / tex.width, uv.height / tex.height);
                GUI.DrawTextureWithTexCoords(rect, tex, uv, true);
            }
            catch
            {
                Label(rect, "?", centeredStyle);
            }
        }

        private Color TierColor(string tier)
        {
            if (tier == "Tier1") return new Color(0.80f, 0.84f, 0.88f);
            if (tier == "Tier2") return new Color(0.45f, 0.90f, 0.51f);
            if (tier == "Tier3") return new Color(0.96f, 0.39f, 0.40f);
            if (tier == "Boss") return new Color(0.96f, 0.78f, 0.32f);
            if (tier == "Lunar") return new Color(0.51f, 0.78f, 0.98f);
            if (tier.IndexOf("Void", StringComparison.OrdinalIgnoreCase) >= 0) return new Color(0.77f, 0.43f, 0.95f);
            if (tier == "Hex") return new Color(0.67f, 0.45f, 0.93f);
            if (tier == "Cursed") return new Color(1f, 0.56f, 0.32f);
            if (tier == "Page") return new Color(0.29f, 0.82f, 0.75f);
            return Muted;
        }

    }
}
