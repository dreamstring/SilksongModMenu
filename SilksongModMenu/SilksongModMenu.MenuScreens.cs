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
            Logger.LogInfo("╔═══════════════════════════════════════════════════════════════════");
            Logger.LogInfo("║ SetupModsContent - Version 2.0 WITH SCROLLVIEW");
            Logger.LogInfo("╚═══════════════════════════════════════════════════════════════════");

#if DEBUG_UI
    DiagnoseAllScrollbars();
#endif

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

#if DEBUG_UI
    // ========== 详细分析原型 ==========
    if (rowPrototype != null)
    {
        Logger.LogInfo("╔════════════════════════════════════════════════════════════════");
        Logger.LogInfo("║ PROTOTYPE DETAILED ANALYSIS");
        Logger.LogInfo("╠════════════════════════════════════════════════════════════════");
        Logger.LogInfo("║ [HIERARCHY]");
        AnalyzeHierarchy(rowPrototype.transform, "║   ");
        Logger.LogInfo("╠════════════════════════════════════════════════════════════════");
        Logger.LogInfo("║ [ALL COMPONENTS]");

        var allComps = rowPrototype.GetComponentsInChildren<Component>(true);
        foreach (var comp in allComps)
        {
            if (comp == null) continue;
            var go = comp.gameObject;
            var typeName = comp.GetType().Name;
            Logger.LogInfo($"║ [{go.name}] {typeName}");

            if (comp is Selectable sel)
            {
                Logger.LogInfo($"║   ├─ Interactable: {sel.interactable}");
                Logger.LogInfo($"║   ├─ Navigation: {sel.navigation.mode}");
                Logger.LogInfo($"║   ├─ Transition: {sel.transition}");
            }

            if (comp is Animator anim)
            {
                Logger.LogInfo($"║   ├─ Animator:");
                Logger.LogInfo($"║   │  ├─ Controller: {anim.runtimeAnimatorController?.name ?? "null"}");
                Logger.LogInfo($"║   │  └─ Enabled: {anim.enabled}");
            }

            if (comp is UnityEngine.UI.Image img)
            {
                Logger.LogInfo($"║   ├─ Image:");
                Logger.LogInfo($"║   │  ├─ Sprite: {img.sprite?.name ?? "null"}");
                Logger.LogInfo($"║   │  ├─ Color: {ColorToString(img.color)}");
                Logger.LogInfo($"║   │  └─ Type: {img.type}");
            }

            if (comp is Text txt)
            {
                Logger.LogInfo($"║   ├─ Text: \"{txt.text}\"");
                Logger.LogInfo($"║   └─ Color: {ColorToString(txt.color)}");
            }
        }

        Logger.LogInfo("╚════════════════════════════════════════════════════════════════");
    }
#endif

            // 创建 ScrollView 包装
            Logger.LogInfo(">>> CREATING SCROLLVIEW WRAPPER <<<");

            // 1. 保存原始 Content 的信息
            var originalContentRT = modsContentParent.GetComponent<RectTransform>();
            var originalParent = modsContentParent.parent;
            var originalSiblingIndex = modsContentParent.GetSiblingIndex();

            Logger.LogInfo($"Original Content: parent={originalParent.name}, siblingIndex={originalSiblingIndex}");
            Logger.LogInfo($"Original Content RectTransform: anchors=({originalContentRT.anchorMin}, {originalContentRT.anchorMax}), pos={originalContentRT.anchoredPosition}, size={originalContentRT.sizeDelta}");

            // 2. 创建 ScrollView 根对象
            var scrollViewObj = new GameObject("ModListScrollView", typeof(RectTransform));
            var scrollViewRT = scrollViewObj.GetComponent<RectTransform>();
            scrollViewRT.SetParent(originalParent, false);
            scrollViewRT.SetSiblingIndex(originalSiblingIndex);

            // 3. 复制原始 Content 的布局参数到 ScrollView
            scrollViewRT.anchorMin = originalContentRT.anchorMin;
            scrollViewRT.anchorMax = originalContentRT.anchorMax;
            scrollViewRT.anchoredPosition = originalContentRT.anchoredPosition;
            scrollViewRT.sizeDelta = originalContentRT.sizeDelta;

            Logger.LogInfo($"ScrollView created: pos={scrollViewRT.anchoredPosition}, size={scrollViewRT.sizeDelta}");

            #if DEBUG_UI
            // 添加调试背景（紫色）
            var scrollViewImage = scrollViewObj.AddComponent<Image>();
            scrollViewImage.color = new Color(0.5f, 0f, 0.5f, 0.3f);
            Logger.LogInfo("Added PURPLE debug background to ScrollView");
            #endif

            // 4. 添加 ScrollRect 组件
            var scrollRect = scrollViewObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.elasticity = 0.1f;
            scrollRect.scrollSensitivity = 30f;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;
            Logger.LogInfo("ScrollRect component added");

            // 5. 创建 Viewport
            var viewportObj = new GameObject("Viewport", typeof(RectTransform));
            var viewportRT = viewportObj.GetComponent<RectTransform>();
            viewportRT.SetParent(scrollViewRT, false);
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.sizeDelta = Vector2.zero;
            viewportRT.anchoredPosition = Vector2.zero;
            Logger.LogInfo($"Viewport created: size={viewportRT.rect.size}");

            // ========== 关键修复1：添加透明 Image 以接收鼠标事件 ==========
            var viewportImage = viewportObj.AddComponent<Image>();
            #if DEBUG_UI
            // 调试模式：半透明蓝色
            viewportImage.color = new Color(0f, 0.5f, 1f, 0.3f);
            Logger.LogInfo("Added BLUE debug background to Viewport");
            #else
            // 生产模式：完全透明（但仍能接收事件）
            viewportImage.color = new Color(1f, 1f, 1f, 0.01f); // 几乎透明，但 raycastTarget 为 true
            Logger.LogInfo("Added transparent Image to Viewport for mouse events");
            #endif

            // 6. 添加 RectMask2D（裁剪）
            var rectMask = viewportObj.AddComponent<RectMask2D>();
            scrollRect.viewport = viewportRT;
            Logger.LogInfo("RectMask2D added to Viewport");

            // 7. 将原始 Content 移到 Viewport 下
            modsContentParent.SetParent(viewportRT, false);
            Logger.LogInfo($"Original Content moved to Viewport (parent: {modsContentParent.parent.name})");

            // 8. 重新配置 Content 的 RectTransform（适配 ScrollView）
            originalContentRT.anchorMin = new Vector2(0, 1);
            originalContentRT.anchorMax = new Vector2(1, 1);
            originalContentRT.pivot = new Vector2(0.5f, 1);
            originalContentRT.anchoredPosition = Vector2.zero;
            originalContentRT.sizeDelta = new Vector2(0, 100);
            Logger.LogInfo($"Content RectTransform reconfigured: anchors=({originalContentRT.anchorMin}, {originalContentRT.anchorMax})");

            #if DEBUG_UI
            // 添加调试背景（绿色）
            var contentImage = modsContentParent.gameObject.AddComponent<Image>();
            contentImage.color = new Color(0f, 1f, 0f, 0.2f);
            Logger.LogInfo("Added GREEN debug background to Content");
            #endif

            // 9. 确保 VerticalLayoutGroup 存在
            var vlg = modsContentParent.GetComponent<VerticalLayoutGroup>();
            if (vlg == null)
            {
                vlg = modsContentParent.gameObject.AddComponent<VerticalLayoutGroup>();
                Logger.LogInfo("Added VerticalLayoutGroup to Content");
            }

            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(20, 20, 20, 20);
            Logger.LogInfo("VerticalLayoutGroup configured (childControlHeight=false)");

            // 10. 添加 ContentSizeFitter（自动调整高度）
            var csf = modsContentParent.GetComponent<ContentSizeFitter>();
            if (csf == null)
            {
                csf = modsContentParent.gameObject.AddComponent<ContentSizeFitter>();
                Logger.LogInfo("Added ContentSizeFitter to Content");
            }

            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Logger.LogInfo("ContentSizeFitter configured (verticalFit=PreferredSize)");

            // 11. 设置 ScrollRect 的 content 引用
            scrollRect.content = originalContentRT;
            Logger.LogInfo("ScrollRect.content assigned");

            // 12. 克隆原生滚动条（从成就菜单）
            var scrollbarObj = CloneNativeScrollbar(scrollViewObj, scrollViewRT);
            if (scrollbarObj != null)
            {
                var scrollbar = scrollbarObj.GetComponent<Scrollbar>();
                scrollRect.verticalScrollbar = scrollbar;
                scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
                scrollRect.verticalScrollbarSpacing = 10f;

                // 重置 Scrollbar 的初始值
                scrollbar.value = 1f; // 从顶部开始
                scrollbar.size = 0.5f; // 临时值，会被 ScrollRect 自动更新

                Logger.LogInfo($"Scrollbar linked: value={scrollbar.value}, size={scrollbar.size}");
                Logger.LogInfo("ScrollRect will auto-update scrollbar.size based on content height");
            }
            else
            {
                Logger.LogWarning("Failed to clone native scrollbar");
            }


            // 13. 确保原型在 Content 下
            if (rowPrototype.transform.parent != modsContentParent)
            {
                Logger.LogInfo("Moving prototype to new Content location");
                rowPrototype.transform.SetParent(modsContentParent, false);
            }

             #if DEBUG_UI
            Logger.LogInfo("╔═══════════════════════════════════════════════════════════════════");
            Logger.LogInfo("║ SCROLLVIEW STRUCTURE CREATED SUCCESSFULLY");
            Logger.LogInfo("╠═══════════════════════════════════════════════════════════════════");
            Logger.LogInfo($"║ ScrollView: {scrollViewRT.rect.size}");
            Logger.LogInfo($"║ Viewport: {viewportRT.rect.size}");
            Logger.LogInfo($"║ Content: sizeDelta={originalContentRT.sizeDelta}, height={originalContentRT.rect.height}");
            Logger.LogInfo($"║ modsContentParent: {modsContentParent.name}");
            Logger.LogInfo($"║ modsContentParent parent: {modsContentParent.parent.name}");
            Logger.LogInfo($"║ rowPrototype parent: {rowPrototype.transform.parent.name}");
            Logger.LogInfo($"║ Has ScrollRect: {modsContentParent.GetComponentInParent<ScrollRect>() != null}");
            Logger.LogInfo("╚═══════════════════════════════════════════════════════════════════");
            #endif

            Logger.LogInfo("=== SetupModsContent END ===");
        }



        private static void CreateScrollView(Transform modsContentParent, ref GameObject rowPrototype)
        {
            try
            {
                Logger.LogInfo(">>> CREATING SCROLLVIEW WRAPPER <<<");

                var originalContent = modsContentParent.gameObject;
                var menuScreen = originalContent.transform.parent;

                Logger.LogInfo($"Original Content: parent={menuScreen.name}, siblingIndex={originalContent.transform.GetSiblingIndex()}");

                var originalRT = originalContent.GetComponent<RectTransform>();
                Logger.LogInfo($"Original Content RectTransform: anchors=({originalRT.anchorMin}, {originalRT.anchorMax}), pos={originalRT.anchoredPosition}, size={originalRT.sizeDelta}");

                // ========== 1. 创建 ScrollView 容器 ==========
                var scrollViewObj = new GameObject("ModListScrollView", typeof(RectTransform));
                var scrollViewRT = scrollViewObj.GetComponent<RectTransform>();
                scrollViewRT.SetParent(menuScreen, false);
                scrollViewRT.SetSiblingIndex(originalContent.transform.GetSiblingIndex());

                scrollViewRT.anchorMin = new Vector2(0.5f, 0.5f);
                scrollViewRT.anchorMax = new Vector2(0.5f, 0.5f);
                scrollViewRT.pivot = new Vector2(0.5f, 0.5f);
                scrollViewRT.anchoredPosition = originalRT.anchoredPosition;
                scrollViewRT.sizeDelta = originalRT.sizeDelta;

                Logger.LogInfo($"ScrollView created: pos={scrollViewRT.anchoredPosition}, size={scrollViewRT.sizeDelta}");

                // 添加调试背景
                var scrollViewBg = scrollViewObj.AddComponent<Image>();
                scrollViewBg.color = new Color(0.5f, 0f, 0.5f, 0.3f); // 紫色
                Logger.LogInfo("Added PURPLE debug background to ScrollView");

                // ========== 2. 添加 ScrollRect 组件 ==========
                var scrollRect = scrollViewObj.AddComponent<ScrollRect>();
                Logger.LogInfo("ScrollRect component added");

                // ========== 3. 创建 Viewport ==========
                var viewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
                var viewportRT = viewportObj.GetComponent<RectTransform>();
                viewportRT.SetParent(scrollViewRT, false);

                viewportRT.anchorMin = Vector2.zero;
                viewportRT.anchorMax = Vector2.one;
                viewportRT.sizeDelta = Vector2.zero;
                viewportRT.anchoredPosition = Vector2.zero;

                Logger.LogInfo($"Viewport created: size={viewportRT.sizeDelta}");

                // 添加调试背景
                var viewportBg = viewportObj.AddComponent<Image>();
                viewportBg.color = new Color(0f, 0f, 1f, 0.2f); // 蓝色
                Logger.LogInfo("Added BLUE debug background to Viewport");

                var mask = viewportObj.GetComponent<RectMask2D>();
                Logger.LogInfo("RectMask2D added to Viewport");

                // ========== 4. 移动原始 Content 到 Viewport ==========
                originalContent.transform.SetParent(viewportRT, false);
                Logger.LogInfo($"Original Content moved to Viewport (parent: {originalContent.transform.parent.name})");

                // 重新配置 Content 的 RectTransform
                originalRT.anchorMin = new Vector2(0, 1);
                originalRT.anchorMax = new Vector2(1, 1);
                originalRT.pivot = new Vector2(0.5f, 1);
                originalRT.anchoredPosition = Vector2.zero;
                originalRT.sizeDelta = new Vector2(0, 100);

                Logger.LogInfo("Content RectTransform reconfigured: anchors=((0.00, 1.00), (1.00, 1.00))");

                // 添加调试背景
                var contentBg = originalContent.AddComponent<Image>();
                contentBg.color = new Color(0f, 1f, 0f, 0.2f); // 绿色
                Logger.LogInfo("Added GREEN debug background to Content");

                // ========== 5. 配置 VerticalLayoutGroup ==========
                var layoutGroup = originalContent.GetComponent<VerticalLayoutGroup>();
                if (layoutGroup == null)
                {
                    layoutGroup = originalContent.AddComponent<VerticalLayoutGroup>();
                }

                layoutGroup.childControlWidth = true;
                layoutGroup.childControlHeight = false;
                layoutGroup.childForceExpandWidth = true;
                layoutGroup.childForceExpandHeight = false;
                layoutGroup.spacing = 10f;
                layoutGroup.padding = new RectOffset(20, 20, 20, 20);
                layoutGroup.childAlignment = TextAnchor.UpperCenter;

                Logger.LogInfo("VerticalLayoutGroup configured (childControlHeight=false)");

                // ========== 6. 添加 ContentSizeFitter ==========
                var sizeFitter = originalContent.GetComponent<ContentSizeFitter>();
                if (sizeFitter == null)
                {
                    sizeFitter = originalContent.AddComponent<ContentSizeFitter>();
                    Logger.LogInfo("Added ContentSizeFitter to Content");
                }

                sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                Logger.LogInfo("ContentSizeFitter configured (verticalFit=PreferredSize)");

                // ========== 7. 配置 ScrollRect ==========
                scrollRect.content = originalRT;
                scrollRect.viewport = viewportRT;
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
                scrollRect.inertia = true;
                scrollRect.decelerationRate = 0.135f;
                scrollRect.scrollSensitivity = 60f;
                scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
                scrollRect.verticalScrollbarSpacing = 10f;

                Logger.LogInfo("ScrollRect.content assigned");

                // ========== 8. 克隆原生滚动条 ==========
                var scrollbar = CloneNativeScrollbar(scrollViewObj, scrollViewRT);
                if (scrollbar != null)
                {
                    var scrollbarComponent = scrollbar.GetComponent<Scrollbar>();
                    scrollRect.verticalScrollbar = scrollbarComponent;
                    Logger.LogInfo("Scrollbar created and linked");
                }
                else
                {
                    Logger.LogWarning("Failed to create scrollbar, using ScrollRect without scrollbar");
                }

                // ========== 9. 输出最终结构 ==========
                Logger.LogInfo("╔═══════════════════════════════════════════════════════════════════");
                Logger.LogInfo("║ SCROLLVIEW STRUCTURE CREATED SUCCESSFULLY");
                Logger.LogInfo("╠═══════════════════════════════════════════════════════════════════");
                Logger.LogInfo($"║ ScrollView (PURPLE): {scrollViewRT.sizeDelta}");
                Logger.LogInfo($"║ Viewport (BLUE): {viewportRT.sizeDelta}");
                Logger.LogInfo($"║ Content (GREEN): sizeDelta={originalRT.sizeDelta}, height={originalRT.rect.height}");
                Logger.LogInfo($"║ modsContentParent: {modsContentParent.name}");
                Logger.LogInfo($"║ modsContentParent parent: {modsContentParent.parent.name}");
                Logger.LogInfo($"║ rowPrototype parent: {rowPrototype.transform.parent.name}");

                // 修复：正确查找 ScrollRect
                var foundScrollRect = scrollViewObj.GetComponent<ScrollRect>();
                Logger.LogInfo($"║ Has ScrollRect: {foundScrollRect != null}");
                if (foundScrollRect != null)
                {
                    Logger.LogInfo($"║ ScrollRect.content: {foundScrollRect.content?.name ?? "null"}");
                    Logger.LogInfo($"║ ScrollRect.viewport: {foundScrollRect.viewport?.name ?? "null"}");
                    Logger.LogInfo($"║ ScrollRect.verticalScrollbar: {foundScrollRect.verticalScrollbar?.name ?? "null"}");
                }

                Logger.LogInfo("╚═══════════════════════════════════════════════════════════════════");
                
                // ========== 10. 添加滚轮支持 ==========
                Logger.LogInfo(">>> Adding scroll wheel support <<<");
                var scrollWheelHandler = scrollViewObj.AddComponent<ScrollWheelHandler>();
                scrollWheelHandler.scrollRect = scrollRect;
                Logger.LogInfo("Scroll wheel support added");


            }
            catch (Exception e)
            {
                Logger.LogError($"CreateScrollView failed: {e}");
            }
        }

        /// <summary>
        /// 克隆原生滚动条（从成就菜单）
        /// </summary>
        private static GameObject CloneNativeScrollbar(GameObject menuObj, Transform parent)
        {
            try
            {
                Logger.LogInfo(">>> Attempting to clone native scrollbar <<<");

                Scrollbar nativeScrollbar = null;

                // 方法1：通过完整路径查找
                var uiManager = GameObject.Find("_UIManager");
                if (uiManager != null)
                {
                    var achievementsPath = "UICanvas/AchievementsMenuScreen/Content/Scrollbar";
                    var scrollbarTransform = uiManager.transform.Find(achievementsPath);
                    if (scrollbarTransform != null)
                    {
                        nativeScrollbar = scrollbarTransform.GetComponent<Scrollbar>();
                        if (nativeScrollbar != null)
                        {
                            Logger.LogInfo($"Found native scrollbar via path: {achievementsPath}");
                        }
                    }
                }

                // 方法2：全局搜索（备选）
                if (nativeScrollbar == null)
                {
                    var allScrollbars = Resources.FindObjectsOfTypeAll<Scrollbar>();
                    Logger.LogInfo($"Searching through {allScrollbars.Length} scrollbars...");

                    foreach (var sb in allScrollbars)
                    {
                        if (sb.name == "Scrollbar" && sb.transform.parent?.name == "Content")
                        {
                            var grandParent = sb.transform.parent.parent;
                            if (grandParent != null && grandParent.name == "AchievementsMenuScreen")
                            {
                                nativeScrollbar = sb;
                                Logger.LogInfo("Found achievements scrollbar via search");
                                break;
                            }
                        }
                    }

                    if (nativeScrollbar == null && allScrollbars.Length > 0)
                    {
                        nativeScrollbar = allScrollbars[0];
                        Logger.LogInfo($"Using first available scrollbar: {nativeScrollbar.name}");
                    }
                }

                if (nativeScrollbar == null)
                {
                    Logger.LogWarning("No native scrollbar found, creating custom one");
                    return CreateCustomScrollbar(parent);
                }

                // 克隆滚动条
                var clonedScrollbarObj = Instantiate(nativeScrollbar.gameObject, parent);
                clonedScrollbarObj.name = "ModListScrollbar";

                var clonedScrollbarRT = clonedScrollbarObj.GetComponent<RectTransform>();
                var clonedScrollbar = clonedScrollbarObj.GetComponent<Scrollbar>();

                // ========== 关键修复：正确设置滚动条位置 ==========
                clonedScrollbarRT.anchorMin = new Vector2(1, 0);
                clonedScrollbarRT.anchorMax = new Vector2(1, 1);
                clonedScrollbarRT.pivot = new Vector2(1, 0.5f);

                // 先设置 offsetMin/offsetMax，再设置 anchoredPosition
                // 上下边距各 20 像素
                clonedScrollbarRT.offsetMin = new Vector2(clonedScrollbarRT.offsetMin.x, 20);
                clonedScrollbarRT.offsetMax = new Vector2(clonedScrollbarRT.offsetMax.x, -20);

                // 从右边缘向左偏移 100 像素（这个值会覆盖 offsetMax.x）
                clonedScrollbarRT.anchoredPosition = new Vector2(-100, 0);

                Logger.LogInfo($"Scrollbar positioned:");
                Logger.LogInfo($"  ├─ anchorMin: {clonedScrollbarRT.anchorMin}");
                Logger.LogInfo($"  ├─ anchorMax: {clonedScrollbarRT.anchorMax}");
                Logger.LogInfo($"  ├─ pivot: {clonedScrollbarRT.pivot}");
                Logger.LogInfo($"  ├─ anchoredPosition: {clonedScrollbarRT.anchoredPosition}");
                Logger.LogInfo($"  ├─ offsetMin: {clonedScrollbarRT.offsetMin}");
                Logger.LogInfo($"  ├─ offsetMax: {clonedScrollbarRT.offsetMax}");
                Logger.LogInfo($"  └─ sizeDelta: {clonedScrollbarRT.sizeDelta}");

                clonedScrollbar.direction = Scrollbar.Direction.BottomToTop;

                // 移除本地化组件
                var localizers = clonedScrollbarObj.GetComponentsInChildren<AutoLocalizeTextUI>(true);
                foreach (var loc in localizers)
                {
                    if (loc != null) Destroy(loc);
                }

                // 激活所有子对象
                foreach (Transform child in clonedScrollbarObj.transform)
                {
                    child.gameObject.SetActive(true);
                }

                // ========== 关键修复：确保 Sliding Area 填满整个滚动条 ==========
                var slidingArea = clonedScrollbarObj.transform.Find("Sliding Area");
                if (slidingArea != null)
                {
                    var slidingAreaRT = slidingArea.GetComponent<RectTransform>();
                    if (slidingAreaRT != null)
                    {
                        // 让 Sliding Area 填满整个滚动条（不留边距）
                        slidingAreaRT.anchorMin = Vector2.zero;
                        slidingAreaRT.anchorMax = Vector2.one;
                        slidingAreaRT.offsetMin = Vector2.zero;
                        slidingAreaRT.offsetMax = Vector2.zero;
                        slidingAreaRT.anchoredPosition = Vector2.zero;

                        Logger.LogInfo("Sliding Area adjusted to full size");
                        Logger.LogInfo($"  ├─ anchorMin: {slidingAreaRT.anchorMin}");
                        Logger.LogInfo($"  ├─ anchorMax: {slidingAreaRT.anchorMax}");
                        Logger.LogInfo($"  ├─ offsetMin: {slidingAreaRT.offsetMin}");
                        Logger.LogInfo($"  ├─ offsetMax: {slidingAreaRT.offsetMax}");
                        Logger.LogInfo($"  └─ sizeDelta: {slidingAreaRT.sizeDelta}");

                        // 调整 Handle
                        var handle = slidingArea.Find("Handle");
                        if (handle != null)
                        {
                            var handleRT = handle.GetComponent<RectTransform>();
                            if (handleRT != null)
                            {
                                handleRT.anchorMin = new Vector2(0, 0);
                                handleRT.anchorMax = new Vector2(1, 1);
                                handleRT.offsetMin = Vector2.zero;
                                handleRT.offsetMax = Vector2.zero;
                                handleRT.anchoredPosition = Vector2.zero;

                                Logger.LogInfo("Handle adjusted");
                                Logger.LogInfo($"  ├─ anchorMin: {handleRT.anchorMin}");
                                Logger.LogInfo($"  ├─ anchorMax: {handleRT.anchorMax}");
                                Logger.LogInfo($"  └─ sizeDelta: {handleRT.sizeDelta}");
                            }
                        }
                    }
                }

                // 调整 Background
                var background = clonedScrollbarObj.transform.Find("Background");
                if (background != null)
                {
                    var backgroundRT = background.GetComponent<RectTransform>();
                    if (backgroundRT != null)
                    {
                        // 将 Background 移到 Sliding Area 下
                        background.SetParent(slidingArea, false);

                        // 让 Background 垂直填满 Sliding Area
                        backgroundRT.anchorMin = new Vector2(0.5f, 0);
                        backgroundRT.anchorMax = new Vector2(0.5f, 1);
                        backgroundRT.pivot = new Vector2(0.5f, 0.5f);
                        backgroundRT.anchoredPosition = Vector2.zero;
                        backgroundRT.offsetMin = new Vector2(-2.5f, 0);  // 宽度 5px
                        backgroundRT.offsetMax = new Vector2(2.5f, 0);

                        Logger.LogInfo("Background moved to Sliding Area and stretched");
                        Logger.LogInfo($"  ├─ anchorMin: {backgroundRT.anchorMin}");
                        Logger.LogInfo($"  ├─ anchorMax: {backgroundRT.anchorMax}");
                        Logger.LogInfo($"  └─ sizeDelta: {backgroundRT.sizeDelta}");
                    }
                }
                else
                {
                    Logger.LogWarning("Background not found");
                }

                // ========== 关键修复：重新设置 Scrollbar 的 handleRect ==========
                var handleTransform = slidingArea?.Find("Handle");
                if (handleTransform != null)
                {
                    var handleRT = handleTransform.GetComponent<RectTransform>();
                    clonedScrollbar.handleRect = handleRT;

                    // 确保 targetGraphic 正确设置
                    var handleImage = handleTransform.GetComponent<Image>();
                    if (handleImage != null)
                    {
                        clonedScrollbar.targetGraphic = handleImage;
                        Logger.LogInfo("Scrollbar targetGraphic set to Handle Image");
                    }

                    Logger.LogInfo($"Scrollbar handleRect set to: {handleRT.name}");
                }
                else
                {
                    Logger.LogWarning("Handle not found, scrollbar may not respond to clicks");
                }

#if DEBUG_UI
        Logger.LogInfo("========== SCROLLBAR HIERARCHY DEBUG ==========");

        void LogTransformHierarchy(Transform t, int depth = 0)
        {
            string indent = new string(' ', depth * 2);
            var rt = t.GetComponent<RectTransform>();

            if (rt != null)
            {
                Logger.LogInfo($"{indent}├─ {t.name}");
                Logger.LogInfo($"{indent}│  ├─ anchorMin: {rt.anchorMin}");
                Logger.LogInfo($"{indent}│  ├─ anchorMax: {rt.anchorMax}");
                Logger.LogInfo($"{indent}│  ├─ offsetMin: {rt.offsetMin}");
                Logger.LogInfo($"{indent}│  ├─ offsetMax: {rt.offsetMax}");
                Logger.LogInfo($"{indent}│  ├─ sizeDelta: {rt.sizeDelta}");
                Logger.LogInfo($"{indent}│  ├─ anchoredPosition: {rt.anchoredPosition}");
                Logger.LogInfo($"{indent}│  └─ pivot: {rt.pivot}");
            }
            else
            {
                Logger.LogInfo($"{indent}├─ {t.name} (no RectTransform)");
            }

            foreach (Transform child in t)
            {
                LogTransformHierarchy(child, depth + 1);
            }
        }

        LogTransformHierarchy(clonedScrollbarObj.transform);
        Logger.LogInfo("===============================================");
#endif

                clonedScrollbarObj.SetActive(true);

                Logger.LogInfo("Native scrollbar cloned successfully");
                Logger.LogInfo($"  ├─ Has TopFleur: {clonedScrollbarObj.transform.Find("TopFleur") != null}");
                Logger.LogInfo($"  ├─ Has Background: {clonedScrollbarObj.transform.Find("Background") != null}");
                Logger.LogInfo($"  ├─ Handle: {clonedScrollbar.handleRect?.name ?? "null"}");
                Logger.LogInfo($"  ├─ Direction: {clonedScrollbar.direction}");
                Logger.LogInfo($"  ├─ Value: {clonedScrollbar.value}");
                Logger.LogInfo($"  └─ Size: {clonedScrollbar.size}");

                return clonedScrollbarObj;

            }
            catch (Exception e)
            {
                Logger.LogError($"CloneNativeScrollbar failed: {e}");
                return CreateCustomScrollbar(parent);
            }
        }



        /// <summary>
        /// 创建自定义滚动条（备用方案）
        /// </summary>
        private static GameObject CreateCustomScrollbar(Transform parent)
        {
            try
            {
                Logger.LogInfo(">>> Creating custom scrollbar (fallback) <<<");

                var scrollbarObj = new GameObject("Scrollbar", typeof(RectTransform));
                var scrollbarRT = scrollbarObj.GetComponent<RectTransform>();
                scrollbarRT.SetParent(parent, false);

                scrollbarRT.anchorMin = new Vector2(1, 0);
                scrollbarRT.anchorMax = new Vector2(1, 1);
                scrollbarRT.pivot = new Vector2(1, 0.5f);
                scrollbarRT.anchoredPosition = new Vector2(-10, 0);
                scrollbarRT.sizeDelta = new Vector2(12, 0);

                var scrollbar = scrollbarObj.AddComponent<Scrollbar>();
                scrollbar.direction = Scrollbar.Direction.BottomToTop;

                // 背景
                var bgImage = scrollbarObj.AddComponent<Image>();
                bgImage.color = new Color(0.15f, 0.15f, 0.15f, 0.6f);

                // Sliding Area
                var slidingAreaObj = new GameObject("Sliding Area", typeof(RectTransform));
                var slidingAreaRT = slidingAreaObj.GetComponent<RectTransform>();
                slidingAreaRT.SetParent(scrollbarRT, false);
                slidingAreaRT.anchorMin = Vector2.zero;
                slidingAreaRT.anchorMax = Vector2.one;
                slidingAreaRT.sizeDelta = new Vector2(-2, -2);
                slidingAreaRT.anchoredPosition = Vector2.zero;

                // Handle
                var handleObj = new GameObject("Handle", typeof(RectTransform), typeof(Image));
                var handleRT = handleObj.GetComponent<RectTransform>();
                handleRT.SetParent(slidingAreaRT, false);
                handleRT.anchorMin = Vector2.zero;
                handleRT.anchorMax = Vector2.one;
                handleRT.sizeDelta = new Vector2(0, 40);
                handleRT.anchoredPosition = Vector2.zero;

                var handleImage = handleObj.GetComponent<Image>();
                handleImage.color = new Color(0.9f, 0.9f, 0.9f, 0.9f);

                scrollbar.handleRect = handleRT;
                scrollbar.targetGraphic = handleImage;

                Logger.LogInfo("Custom scrollbar created");
                return scrollbarObj;
            }
            catch (Exception e)
            {
                Logger.LogError($"CreateCustomScrollbar failed: {e}");
                return null;
            }
        }



        /// <summary>
        /// 添加滚轮支持（监听鼠标滚轮和键盘上下键）
        /// </summary>
        private static void AddScrollWheelSupport(GameObject scrollViewObj, ScrollRect scrollRect)
        {
            try
            {
                Logger.LogInfo(">>> Adding scroll wheel support <<<");

                // 添加 ScrollWheelHandler 组件
                var handler = scrollViewObj.AddComponent<ScrollWheelHandler>();
                handler.scrollRect = scrollRect;

                Logger.LogInfo("Scroll wheel support added");
            }
            catch (Exception e)
            {
                Logger.LogError($"AddScrollWheelSupport failed: {e}");
            }
        }

        /// <summary>
        /// 滚轮处理组件
        /// </summary>
        private class ScrollWheelHandler : MonoBehaviour
        {
            public ScrollRect scrollRect;
            private float scrollSpeed = 0.1f; // 每次滚动的距离（0-1之间）

            private void Update()
            {
                if (scrollRect == null) return;

                // 1. 鼠标滚轮
                float scrollDelta = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scrollDelta) > 0.01f)
                {
                    // 向上滚动（scrollDelta > 0）-> 内容向下移动（verticalNormalizedPosition 增加）
                    // 向下滚动（scrollDelta < 0）-> 内容向上移动（verticalNormalizedPosition 减少）
                    scrollRect.verticalNormalizedPosition = Mathf.Clamp01(
                        scrollRect.verticalNormalizedPosition + scrollDelta * scrollSpeed * 10f
                    );
                }

                // 2. 键盘方向键（可选）
                if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W))
                {
                    scrollRect.verticalNormalizedPosition = Mathf.Clamp01(
                        scrollRect.verticalNormalizedPosition + scrollSpeed * Time.deltaTime
                    );
                }
                else if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S))
                {
                    scrollRect.verticalNormalizedPosition = Mathf.Clamp01(
                        scrollRect.verticalNormalizedPosition - scrollSpeed * Time.deltaTime
                    );
                }

                // 3. 手柄摇杆（可选）
                float verticalAxis = Input.GetAxis("Vertical");
                if (Mathf.Abs(verticalAxis) > 0.1f)
                {
                    scrollRect.verticalNormalizedPosition = Mathf.Clamp01(
                        scrollRect.verticalNormalizedPosition + verticalAxis * scrollSpeed * Time.deltaTime
                    );
                }
            }
        }


        private static GameObject CreateScrollbar(Transform parent)
        {
            try
            {
                // 1. 创建 Scrollbar 根对象
                var scrollbarObj = new GameObject("Scrollbar", typeof(RectTransform));
                var scrollbarRT = scrollbarObj.GetComponent<RectTransform>();
                scrollbarRT.SetParent(parent, false);

                // 2. 定位到右侧
                scrollbarRT.anchorMin = new Vector2(1, 0);
                scrollbarRT.anchorMax = new Vector2(1, 1);
                scrollbarRT.pivot = new Vector2(1, 0.5f);
                scrollbarRT.anchoredPosition = new Vector2(-5, 0); // 距离右边缘5像素
                scrollbarRT.sizeDelta = new Vector2(20, 0); // 宽度20像素

                // 3. 添加 Scrollbar 组件
                var scrollbar = scrollbarObj.AddComponent<Scrollbar>();
                scrollbar.direction = Scrollbar.Direction.BottomToTop;

                // 4. 创建 Sliding Area
                var slidingAreaObj = new GameObject("Sliding Area", typeof(RectTransform));
                var slidingAreaRT = slidingAreaObj.GetComponent<RectTransform>();
                slidingAreaRT.SetParent(scrollbarRT, false);
                slidingAreaRT.anchorMin = Vector2.zero;
                slidingAreaRT.anchorMax = Vector2.one;
                slidingAreaRT.sizeDelta = new Vector2(-20, -20); // 留边距
                slidingAreaRT.anchoredPosition = Vector2.zero;

                // 5. 创建 Handle
                var handleObj = new GameObject("Handle", typeof(RectTransform), typeof(Image));
                var handleRT = handleObj.GetComponent<RectTransform>();
                handleRT.SetParent(slidingAreaRT, false);
                handleRT.anchorMin = Vector2.zero;
                handleRT.anchorMax = Vector2.one;
                handleRT.sizeDelta = new Vector2(20, 20);
                handleRT.anchoredPosition = Vector2.zero;

                var handleImage = handleObj.GetComponent<Image>();
                handleImage.color = new Color(1f, 1f, 1f, 0.5f); // 半透明白色

                // 6. 设置 Scrollbar 的 handleRect
                scrollbar.handleRect = handleRT;
                scrollbar.targetGraphic = handleImage;

                // 7. 添加背景
                var bgImage = scrollbarObj.AddComponent<Image>();
                bgImage.color = new Color(0f, 0f, 0f, 0.3f); // 半透明黑色背景

                Logger.LogInfo("Scrollbar created successfully");
                return scrollbarObj;
            }
            catch (Exception e)
            {
                Logger.LogError($"CreateScrollbar failed: {e}");
                return null;
            }
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
