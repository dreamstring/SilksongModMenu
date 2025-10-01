// SilksongModMenu.MenuScreens.cs
using GlobalEnums;
using HarmonyLib;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ModUINamespace
{
    public partial class SilksongModMenu
    {
        private static void ShowModOptionsMenu(UIManager uiManager)
        {
            Logger.LogInfo("Showing Mod Options menu...");

            if (modOptionsMenuScreen == null)
            {
                CreateModOptionsMenu(uiManager);
            }

            ResetAllRowAdjustments();

            BuildModsList();

            uiManager.StartCoroutine(ShowModMenuCoroutine(uiManager));
        }

        private static void CreateModOptionsMenu(UIManager uiManager)
        {
            Logger.LogInfo("Creating Mod Options menu (non-destructive)...");

            var templateMenu = uiManager.gameOptionsMenuScreen;
            if (templateMenu == null)
            {
                Logger.LogError("Cannot find gameOptionsMenuScreen template");
                return;
            }

            var modMenuObj = Instantiate(templateMenu.gameObject, templateMenu.transform.parent);
            modMenuObj.name = "ModOptionsMenuScreen";
            modOptionsMenuScreen = modMenuObj.GetComponent<MenuScreen>();
            if (modOptionsMenuScreen == null)
            {
                Logger.LogError("Failed to get MenuScreen component from cloned object");
                return;
            }

            DisableMenuButtonList(modMenuObj);
            UpdateMenuTitleToMod(modMenuObj, uiManager);
            HeavySanitizeMenu(modMenuObj);

            // Back按钮处理
            if (modOptionsMenuScreen.backButton != null)
            {
                Logger.LogInfo("Setting up Back button...");

                // 彻底清理Back按钮
                CompletelyCleanButton(modOptionsMenuScreen.backButton.gameObject);

                // 额外清理：直接清理MenuButton的所有可能事件（反射）
                var backMenuButton = modOptionsMenuScreen.backButton;
                try
                {
                    var type = backMenuButton.GetType();
                    var fields = type.GetFields(System.Reflection.BindingFlags.Public |
                                               System.Reflection.BindingFlags.NonPublic |
                                               System.Reflection.BindingFlags.Instance);

                    foreach (var field in fields)
                    {
                        if (typeof(UnityEngine.Events.UnityEventBase).IsAssignableFrom(field.FieldType))
                        {
                            var unityEvent = field.GetValue(backMenuButton) as UnityEngine.Events.UnityEventBase;
                            if (unityEvent != null)
                            {
                                Logger.LogInfo($"Clearing Back button UnityEvent field: {field.Name}");
                                unityEvent.RemoveAllListeners();
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.LogWarning($"Back button reflection cleanup error: {e.Message}");
                }

                // 重新创建OnSubmitPressed事件
                backMenuButton.OnSubmitPressed = new UnityEngine.Events.UnityEvent();
                backMenuButton.OnSubmitPressed.AddListener(() =>
                {
                    Logger.LogInfo("=== Back button clicked - returning to Extras menu ===");
                    uiManager.StartCoroutine(ReturnToExtrasMenuCoroutine(uiManager));
                });

                modOptionsMenuScreen.defaultHighlight = modOptionsMenuScreen.backButton;
                Logger.LogInfo("Back button setup completed");
            }

            // ====== 将 Reset Defaults 改造成 Restart Game，点击后弹出基于 quitGamePrompt 的确认框 ======
            try
            {
                MenuButton restartBtn = null;
                Text restartBtnText = null;

                // 优先通过 GameMenuOptions.resetButton 获取
                var gmo = modMenuObj.GetComponent<GameMenuOptions>();
                if (gmo != null && gmo.resetButton != null)
                {
                    restartBtn = gmo.resetButton as MenuButton ?? gmo.resetButton.GetComponent<MenuButton>();
                    if (restartBtn != null)
                    {
                        restartBtnText = restartBtn.GetComponentInChildren<Text>(true);
                    }
                }

                // 回退查找
                if (restartBtn == null)
                {
                    var allMenuButtons = modMenuObj.GetComponentsInChildren<MenuButton>(true);
                    foreach (var mb in allMenuButtons)
                    {
                        Text t = null;
                        try { t = mb.GetComponentInChildren<Text>(true); } catch { /* ignore */ }
                        var s = (t != null ? t.text : mb.name) ?? string.Empty;
                        var lower = s.ToLowerInvariant();
                        if (lower.Contains("reset") || lower.Contains("default"))
                        {
                            restartBtn = mb;
                            restartBtnText = t;
                            break;
                        }
                    }
                }

                // 若依旧没有，动态创建一个按钮
                if (restartBtn == null)
                {
                    Logger.LogWarning("Could not find 'Reset Defaults' button, creating a new Restart button.");

                    var backBtn = modOptionsMenuScreen.backButton;
                    Transform parentForBtn = backBtn != null ? backBtn.transform.parent : modMenuObj.transform;

                    var btnGO = new GameObject("RestartGameButton", typeof(RectTransform), typeof(Image));
                    btnGO.transform.SetParent(parentForBtn, false);
                    var im = btnGO.GetComponent<Image>();
                    im.color = new Color(1, 1, 1, 0.06f);

                    restartBtn = btnGO.AddComponent<MenuButton>();

                    // 文本
                    var txtGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
                    txtGO.transform.SetParent(btnGO.transform, false);
                    restartBtnText = txtGO.GetComponent<Text>();

                    var refText = modMenuObj.GetComponentsInChildren<Text>(true).OrderByDescending(x => x.fontSize).FirstOrDefault();
                    if (refText && refText.font) restartBtnText.font = refText.font;
                    restartBtnText.fontSize = refText ? Math.Max(22, refText.fontSize - 6) : 24;
                    restartBtnText.alignment = TextAnchor.MiddleCenter;
                    restartBtnText.color = Color.white;

                    var rt = (RectTransform)btnGO.transform;
                    rt.sizeDelta = new Vector2(0, 56);

                    var trt = (RectTransform)txtGO.transform;
                    trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
                    trt.pivot = new Vector2(0.5f, 0.5f);
                }

                // 统一清理并改造为 RESTART GAME
                CompletelyCleanButton(restartBtn.gameObject);

                var autoLoc = restartBtn.GetComponentInChildren<AutoLocalizeTextUI>(true);
                if (autoLoc) Destroy(autoLoc);

                if (restartBtnText != null) restartBtnText.text = "RESTART GAME";

                // 直接重启，不弹窗
                restartBtn.OnSubmitPressed = new UnityEngine.Events.UnityEvent();
                restartBtn.OnSubmitPressed.AddListener(() =>
                {
                    Logger.LogInfo("[Restart] Immediate restart requested.");
                    // 可选：先保存设置/状态
                    // uiManager?.gameSettings?.SaveXXX();
                GameRestartUtil.RestartGameImmediate();
                });
            }
            catch (Exception e)
            {
                Logger.LogError($"Setup Restart button failed: {e}");
            }


            // 内容区域：找到语言选项的父容器作为“行原型容器”
            SetupModsContent(modMenuObj);

            // 不再放“安全占位行”，让 BuildModsList 直接填充
            modMenuObj.SetActive(false);
            Logger.LogInfo("Mod Options menu created (animations/navigation preserved)");
        }

        private static IEnumerator ShowRestartPromptCoroutine(UIManager uiManager)
        {
            if (uiManager == null)
            {
                Logger.LogWarning("ShowRestartPromptCoroutine: uiManager is null.");
                yield break;
            }

            var prompt = uiManager.quitGamePrompt;
            if (prompt == null)
            {
                Logger.LogWarning("ShowRestartPromptCoroutine: quitGamePrompt is null, fallback to direct restart.");
                RestartGame(uiManager);
                yield break;
            }

            // 停止 UI 输入（与 UIManager.GoToQuitGamePrompt 一致）
            var ih = Traverse.Create(uiManager).Field("ih").GetValue<InputHandler>();
            if (ih != null) ih.StopUIInput();

            // A) 如果你是从 Mod 菜单打开弹窗，建议先隐藏 Mod 菜单（保持和原生进入提示框的路径一致）
            // 这里不强制隐藏 Mod 菜单，由于 ShowMenu 会把提示框置顶且 blocksRaycasts，穿透会被阻止。
            // 如需完全一致，可取消注释：
            // if (modOptionsMenuScreen != null && modOptionsMenuScreen.gameObject.activeSelf)
            // {
            //     yield return uiManager.HideMenu(modOptionsMenuScreen);
            // }

            // B) 就地改造 quitGamePrompt 的文案与按钮
            //    注意：不要 Destroy AutoLocalizeTextUI，只需要禁用，避免 PlatformSpecificLocalisation 依赖报错
            try
            {
                var root = prompt.gameObject;
                var texts = root.GetComponentsInChildren<Text>(true);
                Text title = texts.FirstOrDefault(t => t.name.ToLowerInvariant().Contains("title")) ?? texts.FirstOrDefault();
                Text body = texts.FirstOrDefault(t => t.name.ToLowerInvariant().Contains("desc") || t.name.ToLowerInvariant().Contains("body"));

                if (title)
                {
                    var loc = title.GetComponent<AutoLocalizeTextUI>();
                    if (loc) loc.enabled = false;
                    title.text = "Restart Game";
                }
                if (body)
                {
                    var loc = body.GetComponent<AutoLocalizeTextUI>();
                    if (loc) loc.enabled = false;
                    body.text = "Are you sure you want to restart the game now?";
                }

                var buttons = root.GetComponentsInChildren<MenuButton>(true);
                var confirmBtn = buttons.ElementAtOrDefault(0);
                var cancelBtn = buttons.ElementAtOrDefault(1);

                if (confirmBtn)
                {
                    CompletelyCleanButton(confirmBtn.gameObject);
                    var t = confirmBtn.GetComponentInChildren<Text>(true);
                    if (t)
                    {
                        var loc = t.GetComponent<AutoLocalizeTextUI>();
                        if (loc) loc.enabled = false;
                        t.text = "Restart";
                    }
                    confirmBtn.OnSubmitPressed = new UnityEngine.Events.UnityEvent();
                    confirmBtn.OnSubmitPressed.AddListener(() =>
                    {
                        Logger.LogInfo("Restart prompt: confirmed.");
                        uiManager.StartCoroutine(CloseRestartPromptThenRestart(uiManager));
                    });
                }

                if (cancelBtn)
                {
                    CompletelyCleanButton(cancelBtn.gameObject);
                    var t = cancelBtn.GetComponentInChildren<Text>(true);
                    if (t)
                    {
                        var loc = t.GetComponent<AutoLocalizeTextUI>();
                        if (loc) loc.enabled = false;
                        t.text = "Cancel";
                    }
                    cancelBtn.OnSubmitPressed = new UnityEngine.Events.UnityEvent();
                    cancelBtn.OnSubmitPressed.AddListener(() =>
                    {
                        Logger.LogInfo("Restart prompt: canceled.");
                        uiManager.StartCoroutine(CloseRestartPrompt(uiManager));
                    });
                }

                prompt.defaultHighlight = confirmBtn != null ? confirmBtn : cancelBtn;
            }
            catch (System.Exception e)
            {
                Logger.LogWarning($"ShowRestartPromptCoroutine: patch prompt ui failed, fallback. {e}");
                RestartGame(uiManager);
                yield break;
            }

            // C) 用原生 ShowMenu 打开（这一步会驱动 CanvasGroup 的淡入，并设置花饰动画）
            yield return uiManager.ShowMenu(prompt);

            // D) 设置 menuState 让 UIManager 认识当前菜单（沿用 QUIT_GAME_PROMPT，省去改枚举）
            // 通过反射或公共方法设置
            var setMenuState = typeof(UIManager).GetMethod("SetMenuState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (setMenuState != null)
            {
                var enumType = typeof(MainMenuState);
                var promptState = System.Enum.Parse(enumType, "QUIT_GAME_PROMPT");
                setMenuState.Invoke(uiManager, new object[] { promptState });
            }

            // 恢复 UI 输入
            if (ih != null) ih.StartUIInput();
        }

        private static IEnumerator CloseRestartPrompt(UIManager uiManager)
        {
            if (uiManager == null)
                yield break;

            var ih = Traverse.Create(uiManager).Field("ih").GetValue<InputHandler>();
            if (ih != null) ih.StopUIInput();

            var prompt = uiManager.quitGamePrompt;
            if (prompt != null)
            {
                // 原生淡出
                yield return uiManager.HideMenu(prompt);
            }

            // 关闭后可以把状态还原到打开前的菜单（这里保持简单：如果你的 Mod 菜单仍在，就不改 menuState；
            // 若你在打开时隐藏了 Mod 菜单，这里可以 ShowMenu(modOptionsMenuScreen) 再 set state）
            if (ih != null) ih.StartUIInput();
        }

        private static IEnumerator CloseRestartPromptThenRestart(UIManager uiManager)
        {
            yield return CloseRestartPrompt(uiManager);
            RestartGame(uiManager);
        }

        private static void RestartGame(UIManager uiManager)
        {
            try
            {
                Logger.LogInfo("RestartGame: begin.");

                // 最贴近原生的“回主菜单”方式：如果有 GameManager 的 ReturnToMainMenu 流程可复用，优先走它。
                // 但因为我们处于主菜单体系内，这里直接加载主菜单场景更直观。
                const string startupSceneName = "Menu_Title"; // 按实际工程中的主菜单场景名修改
                Logger.LogInfo($"RestartGame: loading startup scene '{startupSceneName}'.");
                SceneManager.LoadScene(startupSceneName);

                Logger.LogInfo("RestartGame: done.");
            }
            catch (Exception e)
            {
                Logger.LogError($"RestartGame failed: {e}");
            }
        }

        private static IEnumerator ClosePrompt(UIManager uiManager, MenuScreen prompt)
        {
            if (prompt != null)
            {
                yield return uiManager.HideMenu(prompt);
                if (prompt.gameObject) GameObject.Destroy(prompt.gameObject);
            }
        }

        private static IEnumerator ClosePromptThenRestart(UIManager uiManager, MenuScreen prompt)
        {
            yield return ClosePrompt(uiManager, prompt);
            RestartGame(uiManager);
        }

        private static void UpdateMenuTitleToMod(GameObject menuObj, UIManager uiManager = null)
        {
            var allTexts = menuObj.GetComponentsInChildren<Text>(true);
            foreach (var text in allTexts)
            {
                var s = text.text ?? "";
                if (s.ToLower().Contains("game") || s.ToLower().Contains("options") || IsTitleText(text))
                {
                    // 先移除本地化组件，防止它覆盖我们的文本
                    var loc = text.GetComponent<AutoLocalizeTextUI>();
                    if (loc)
                    {
                        loc.enabled = false; // 先禁用
                        Destroy(loc); // 然后销毁
                    }

                    // 强制设置文本
                    text.text = "MOD OPTIONS";

                    // 通过 UIManager 实例调用协程
                    if (uiManager != null)
                    {
                        uiManager.StartCoroutine(EnsureTitleText(text));
                    }

                    Logger.LogInfo($"Updated menu title to: {text.text}");
                    break;
                }
            }
        }

        private static IEnumerator EnsureTitleText(Text titleText)
        {
            yield return new WaitForEndOfFrame();
            if (titleText != null)
            {
                titleText.text = "MOD OPTIONS";
            }
        }

        private static void SetupModsContent(GameObject menuObj)
        {
            Logger.LogInfo("=== SetupModsContent START ===");

            modsContentParent = FindContentParent(menuObj);
            Logger.LogInfo($"modsContentParent found: {modsContentParent != null}");

            if (modsContentParent == null)
            {
                Logger.LogError("Content parent not found.");
                return;
            }

            Logger.LogInfo($"Content parent name: {modsContentParent.name}, children: {modsContentParent.childCount}");

            // **使用 nativeAchievementsOption 作为原型**
            var gmo = menuObj.GetComponentInChildren<GameMenuOptions>(true);
            Logger.LogInfo($"GameMenuOptions found: {gmo != null}");

            if (gmo != null && gmo.nativeAchievementsOption != null)
            {
                Logger.LogInfo($"nativeAchievementsOption found: {gmo.nativeAchievementsOption.name}");
                var p = gmo.nativeAchievementsOption.transform.parent;
                if (p != null)
                {
                    rowPrototype = p.gameObject;
                    Logger.LogInfo($"Using nativeAchievementsOption parent as prototype: {rowPrototype.name}");
                }
            }

            // 备选方案：使用 languageOption
            if (rowPrototype == null && gmo != null && gmo.languageOption != null)
            {
                Logger.LogWarning("nativeAchievementsOption not found, using languageOption as fallback");
                var p = gmo.languageOption.transform.parent;
                if (p != null)
                {
                    rowPrototype = p.gameObject;
                    Logger.LogInfo($"Using languageOption parent as prototype: {rowPrototype.name}");
                }
            }

            if (rowPrototype == null)
            {
                Logger.LogError("Cannot find suitable prototype!");
                return;
            }

            // 给原型打标记
            rowPrototype.name = "___MOD_ROW_PROTOTYPE___";

            // 清空内容(跳过原型)
            for (int i = modsContentParent.childCount - 1; i >= 0; i--)
            {
                var child = modsContentParent.GetChild(i).gameObject;
                if (child == rowPrototype)
                {
                    Logger.LogInfo($"Skipping prototype during initial cleanup: {child.name}");
                    continue;
                }
                Logger.LogInfo($"Destroying child: {child.name}");
                Destroy(child);
            }

            // 确保原型隐藏
            rowPrototype.SetActive(false);
            if (rowPrototype.transform.parent != modsContentParent)
            {
                Logger.LogInfo("Moving prototype to content parent");
                rowPrototype.transform.SetParent(modsContentParent, false);
            }

            Logger.LogInfo($"Final rowPrototype: {rowPrototype != null}, active: {rowPrototype.activeSelf}, name: {rowPrototype.name}");

            // ========== 详细分析原型 ==========
            if (rowPrototype != null)
            {
                Logger.LogInfo("╔════════════════════════════════════════════════════════════════");
                Logger.LogInfo("║ PROTOTYPE DETAILED ANALYSIS");
                Logger.LogInfo("╠════════════════════════════════════════════════════════════════");

                // 1. 分析层级结构
                Logger.LogInfo("║ [HIERARCHY]");
                AnalyzeHierarchy(rowPrototype.transform, "║   ");

                Logger.LogInfo("╠════════════════════════════════════════════════════════════════");
                Logger.LogInfo("║ [ALL COMPONENTS]");

                // 2. 分析所有组件
                var allComps = rowPrototype.GetComponentsInChildren<Component>(true);
                foreach (var comp in allComps)
                {
                    if (comp == null) continue;

                    var go = comp.gameObject;
                    var typeName = comp.GetType().Name;
                    Logger.LogInfo($"║ [{go.name}] {typeName}");

                    // 3. 详细分析 Selectable
                    if (comp is Selectable sel)
                    {
                        Logger.LogInfo($"║   ├─ Interactable: {sel.interactable}");
                        Logger.LogInfo($"║   ├─ Navigation: {sel.navigation.mode}");
                        Logger.LogInfo($"║   ├─ Transition: {sel.transition}");

                        if (sel.transition == Selectable.Transition.ColorTint)
                        {
                            var colors = sel.colors;
                            Logger.LogInfo($"║   ├─ ColorBlock:");
                            Logger.LogInfo($"║   │  ├─ Normal: {ColorToString(colors.normalColor)}");
                            Logger.LogInfo($"║   │  ├─ Highlighted: {ColorToString(colors.highlightedColor)}");
                            Logger.LogInfo($"║   │  ├─ Pressed: {ColorToString(colors.pressedColor)}");
                            Logger.LogInfo($"║   │  ├─ Selected: {ColorToString(colors.selectedColor)}");
                            Logger.LogInfo($"║   │  └─ Disabled: {ColorToString(colors.disabledColor)}");
                        }
                        else if (sel.transition == Selectable.Transition.SpriteSwap)
                        {
                            Logger.LogInfo($"║   ├─ SpriteState:");
                            Logger.LogInfo($"║   │  ├─ HighlightedSprite: {sel.spriteState.highlightedSprite?.name ?? "null"}");
                            Logger.LogInfo($"║   │  ├─ PressedSprite: {sel.spriteState.pressedSprite?.name ?? "null"}");
                            Logger.LogInfo($"║   │  └─ SelectedSprite: {sel.spriteState.selectedSprite?.name ?? "null"}");
                        }
                        else if (sel.transition == Selectable.Transition.Animation)
                        {
                            Logger.LogInfo($"║   ├─ AnimationTriggers:");
                            Logger.LogInfo($"║   │  ├─ Normal: {sel.animationTriggers.normalTrigger}");
                            Logger.LogInfo($"║   │  ├─ Highlighted: {sel.animationTriggers.highlightedTrigger}");
                            Logger.LogInfo($"║   │  ├─ Pressed: {sel.animationTriggers.pressedTrigger}");
                            Logger.LogInfo($"║   │  └─ Selected: {sel.animationTriggers.selectedTrigger}");
                        }

                        // 检查 TargetGraphic
                        if (sel.targetGraphic != null)
                        {
                            Logger.LogInfo($"║   └─ TargetGraphic: {sel.targetGraphic.GetType().Name} on '{sel.targetGraphic.gameObject.name}'");
                        }
                    }

                    // 4. 详细分析 EventTrigger
                    if (comp is EventTrigger et)
                    {
                        Logger.LogInfo($"║   ├─ EventTrigger count: {et.triggers.Count}");
                        foreach (var trigger in et.triggers)
                        {
                            Logger.LogInfo($"║   │  ├─ {trigger.eventID} (callbacks: {trigger.callback.GetPersistentEventCount()})");
                        }
                    }

                    // 5. 分析 Button
                    if (comp is Button btn)
                    {
                        Logger.LogInfo($"║   ├─ Button.onClick listeners: {btn.onClick.GetPersistentEventCount()}");
                    }

                    // 6. 分析 MenuButton
                    if (typeName == "MenuButton")
                    {
                        try
                        {
                            var type = comp.GetType();
                            var onSubmitField = type.GetField("OnSubmitPressed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (onSubmitField != null)
                            {
                                var submitEvent = onSubmitField.GetValue(comp);
                                if (submitEvent != null)
                                {
                                    var getCountMethod = submitEvent.GetType().GetProperty("PersistentEventCount");
                                    if (getCountMethod != null)
                                    {
                                        var count = getCountMethod.GetValue(submitEvent);
                                        Logger.LogInfo($"║   ├─ MenuButton.OnSubmitPressed listeners: {count}");
                                    }
                                }
                            }
                        }
                        catch (System.Exception e)
                        {
                            Logger.LogInfo($"║   ├─ MenuButton analysis failed: {e.Message}");
                        }
                    }

                    // 7. 分析 Animator
                    if (comp is Animator anim)
                    {
                        Logger.LogInfo($"║   ├─ Animator:");
                        Logger.LogInfo($"║   │  ├─ Controller: {anim.runtimeAnimatorController?.name ?? "null"}");
                        Logger.LogInfo($"║   │  └─ Enabled: {anim.enabled}");
                    }

                    // 8. 分析 Image
                    if (comp is UnityEngine.UI.Image img)
                    {
                        Logger.LogInfo($"║   ├─ Image:");
                        Logger.LogInfo($"║   │  ├─ Sprite: {img.sprite?.name ?? "null"}");
                        Logger.LogInfo($"║   │  ├─ Color: {ColorToString(img.color)}");
                        Logger.LogInfo($"║   │  └─ Type: {img.type}");
                    }

                    // 9. 分析 Text
                    if (comp is Text txt)
                    {
                        Logger.LogInfo($"║   ├─ Text: \"{txt.text}\"");
                        Logger.LogInfo($"║   └─ Color: {ColorToString(txt.color)}");
                    }
                }

                Logger.LogInfo("╚════════════════════════════════════════════════════════════════");
            }

            Logger.LogInfo("=== SetupModsContent END ===");
        }

        private static void DisableMenuButtonList(GameObject menuObj)
        {
            try
            {
                var menuButtonList = menuObj.GetComponent<MenuButtonList>();
                if (menuButtonList != null)
                {
                    Logger.LogInfo("Destroying MenuButtonList to prevent crashes");
                    // 直接销毁，最简单有效
                    Destroy(menuButtonList);
                }

                // 也检查子对象中的MenuButtonList
                var childMenuButtonLists = menuObj.GetComponentsInChildren<MenuButtonList>(true);
                foreach (var mbl in childMenuButtonLists)
                {
                    if (mbl != null)
                    {
                        Logger.LogInfo($"Destroying child MenuButtonList on {mbl.gameObject.name}");
                        Destroy(mbl);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"DisableMenuButtonList error: {e}");
            }
        }

        private static void PreciseStripDangerousBehaviours(GameObject root)
        {
            try
            {
                string[] risky = {
                    "GameMenuOptions",            // 游戏设置逻辑
                    "MenuOptionHorizontal",       // 具体设置选项（左右切换）逻辑
                    "MenuOptionToggle"            // 具体开关逻辑
                };

                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    var tn = mb.GetType().Name;
                    if (risky.Any(r => tn.Contains(r)))
                    {
                        UnityEngine.Object.Destroy(mb);
                    }
                }

                // 注意：不要清空 Button.onClick / EventTrigger 全局！
                // 仅对即将被我们接管的“行”做最小改动（在 CreateModRow 内部处理）
            }
            catch (Exception e)
            {
                Logger.LogError($"PreciseStripDangerousBehaviours error: {e}");
            }
        }

        private static void SafeStripDangerousOnly(GameObject go)
        {
            try
            {
                // 只移除会修改游戏设置的脚本
                string[] dangerousOnly = {
                    "GameMenuOptions",      // 游戏设置逻辑
                    "MenuOptionHorizontal", // 具体选项逻辑  
                    "MenuOptionToggle"      // 开关逻辑
                };

                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    var tn = mb.GetType().Name;
                    if (dangerousOnly.Any(d => tn.Contains(d)))
                    {
                        Destroy(mb);
                    }
                }

                // 只清理Button.onClick，不要清理EventTrigger
                foreach (var btn in go.GetComponentsInChildren<Button>(true))
                    btn.onClick.RemoveAllListeners();

                // 不要销毁EventTrigger
                // foreach (var trig in go.GetComponentsInChildren<UnityEngine.EventSystems.EventTrigger>(true))
                //     Destroy(trig);
            }
            catch (Exception e)
            {
                Logger.LogError($"SafeStripDangerousOnly error: {e}");
            }
        }

        private static void SetupSafePlaceholderContent(GameObject menuObj)
        {
            var contentParent = FindContentParent(menuObj);
            if (contentParent == null)
            {
                Logger.LogError("Cannot find content parent for placeholder");
                return;
            }

            // 删除原内容
            for (int i = contentParent.childCount - 1; i >= 0; i--)
            {
                var child = contentParent.GetChild(i);
                if (child == null) continue;
                Destroy(child.gameObject);
            }

            // 放一个安全占位行
            var row = new GameObject("PlaceholderRow", typeof(RectTransform), typeof(Image));
            var rowRt = (RectTransform)row.transform;
            rowRt.SetParent(contentParent, false);
            rowRt.sizeDelta = new Vector2(0, 48);
            var img = row.GetComponent<Image>();
            img.color = new Color(1, 1, 1, 0.05f);

            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var lrt = (RectTransform)label.transform;
            lrt.SetParent(rowRt, false);
            lrt.anchorMin = new Vector2(0, 0.5f);
            lrt.anchorMax = new Vector2(0, 0.5f);
            lrt.pivot = new Vector2(0, 0.5f);
            lrt.anchoredPosition = new Vector2(14, 0);
            var txt = label.GetComponent<Text>();
            txt.text = "Mod list will be displayed here";
            txt.fontSize = 26;
            txt.color = Color.white;

            var any = menuObj.GetComponentInChildren<Text>(true);
            if (any && any.font) txt.font = any.font;

            // 无任何行为脚本、无 EventTrigger、无 Button
        }

        private static Transform FindContentParent(GameObject menuObj)
        {
            var layouts = menuObj.GetComponentsInChildren<VerticalLayoutGroup>(true);
            foreach (var layout in layouts)
            {
                // 选择更“内容化”的容器（通常子节点较多）
                if (layout.transform.childCount >= 1)
                    return layout.transform;
            }

            // 兜底：找到所有 MenuButton 的公共父
            var buttons = menuObj.GetComponentsInChildren<MenuButton>(true);
            if (buttons.Length > 0)
            {
                var p = buttons[0].transform.parent;
                if (buttons.All(b => b.transform.parent == p))
                    return p;
            }

            return menuObj.transform;
        }

        private static bool IsTitleText(Text textComponent)
        {
            var rect = textComponent.GetComponent<RectTransform>();
            if (rect != null)
            {
                Vector3[] corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                float maxY = corners.Max(corner => corner.y);
                if (maxY > Screen.height * 0.7f) return true;
            }
            return false;
        }

        private static IEnumerator ShowModMenuCoroutine(UIManager uiManager)
        {
            if (modOptionsMenuScreen == null) yield break;

            isShowingModMenu = true;

            var inputHandler = Traverse.Create(uiManager).Field("ih").GetValue<InputHandler>();
            if (inputHandler != null) inputHandler.StopUIInput();

            #if UI_DIAG
            DiagnoseUIHierarchy(modOptionsMenuScreen.gameObject, "BEFORE SHOW");
            #endif

            // 用原生隐藏 Extras，触发原生出场动画（不要传 disable）
            if (uiManager.extrasMenuScreen != null && uiManager.extrasMenuScreen.gameObject.activeSelf)
            {
                yield return uiManager.HideMenu(uiManager.extrasMenuScreen);
            }


            // 用原生 ShowMenu 展示，触发丝线进场动画
            yield return uiManager.ShowMenu(modOptionsMenuScreen);

            yield return new WaitForSeconds(0.1f); // 等动画稳定
            #if UI_DIAG
            DiagnoseUIHierarchy(modOptionsMenuScreen.gameObject, "AFTER SHOW (animation done)");
            #endif

            var canvasGroup = modOptionsMenuScreen.ScreenCanvasGroup;
            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                canvasGroup.alpha = 1f;
            }

            // 默认高亮（保留原生默认选择，如果你想高亮 Back/第一行，请给 defaultHighlight 赋值）
            if (modOptionsMenuScreen.defaultHighlight != null)
                modOptionsMenuScreen.defaultHighlight.Select();
            else
                modOptionsMenuScreen.HighlightDefault();

            if (inputHandler != null) inputHandler.StartUIInput();

            Logger.LogInfo("Mod Options menu shown via ShowMenu()");
        }

        // 在这里添加第二个新方法（备用方案）
        private static IEnumerator SimpleReturnToExtras(UIManager uiManager)
        {
            Logger.LogInfo("Simple return to extras...");

            // 隐藏Mod菜单
            if (modOptionsMenuScreen != null && modOptionsMenuScreen.gameObject != null)
            {
                modOptionsMenuScreen.gameObject.SetActive(false);
            }

            isShowingModMenu = false;

            // 等一帧
            yield return null;

            // 强制显示Extras菜单
            if (uiManager.extrasMenuScreen != null)
            {
                uiManager.extrasMenuScreen.gameObject.SetActive(true);

                // 确保可交互
                var canvasGroup = uiManager.extrasMenuScreen.ScreenCanvasGroup;
                if (canvasGroup != null)
                {
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                    canvasGroup.alpha = 1f;
                }

                // 设置为当前菜单
                var currentMenuField = Traverse.Create(uiManager).Field("currentMenuScreen");
                if (currentMenuField != null)
                {
                    currentMenuField.SetValue(uiManager.extrasMenuScreen);
                }

                // 设置默认高亮
                if (uiManager.extrasMenuScreen.defaultHighlight != null)
                {
                    uiManager.extrasMenuScreen.defaultHighlight.Select();
                }
            }

            Logger.LogInfo("Simple return completed");
        }

        public static void HideModOptionsMenu(UIManager uiManager)
        {
            try
            {
                Logger.LogInfo("Hiding Mod Options menu...");

                if (modOptionsMenuScreen != null)
                {
                    modOptionsMenuScreen.gameObject.SetActive(false);
                }

                if (uiManager?.extrasMenuScreen != null)
                {
                    uiManager.ShowMenu(uiManager.extrasMenuScreen);
                }

                Logger.LogInfo("Returned to Extras menu");
            }
            catch (Exception e)
            {
                Logger.LogError($"HideModOptionsMenu error: {e}");
            }
        }

        private static IEnumerator HideModMenuCoroutine(UIManager uiManager)
        {
            var inputHandler = Traverse.Create(uiManager).Field("ih").GetValue<InputHandler>();
            if (inputHandler != null) inputHandler.StopUIInput();

            if (modOptionsMenuScreen != null && modOptionsMenuScreen.gameObject.activeSelf)
            {
                // 原生隐藏，触发退场动画
                yield return uiManager.HideMenu(modOptionsMenuScreen);
                modOptionsMenuScreen.gameObject.SetActive(false);
            }

            if (uiManager.extrasMenuScreen != null)
            {
                // 原生展示 Extras
                yield return uiManager.ShowMenu(uiManager.extrasMenuScreen);
            }
            else
            {
                yield return uiManager.GoToExtrasMenu();
            }

            if (inputHandler != null) inputHandler.StartUIInput();
            isShowingModMenu = false;
        }
    }
}
