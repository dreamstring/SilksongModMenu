using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;


namespace ModUINamespace
{

    [BepInPlugin("com.yourname.silksongmodmenu", "Silksong Mod Menu", "1.0.0")]
    public class SilksongModMenu : BaseUnityPlugin
    {
        private static MenuScreen modOptionsMenuScreen;
        private static bool isShowingModMenu = false;
        internal new static ManualLogSource Logger;
        private static MenuButton modOptionsButton = null;
        private static Transform modsContentParent;
        private static GameObject rowPrototype;

        // ========== 在类的顶部添加静态字典来追踪每行的调整状态 ==========
        private static Dictionary<string, bool> rowAdjustmentStatus = new Dictionary<string, bool>();

        // ========== 添加重置方法（在进入 Mod Options 界面时调用） ==========
        private static void ResetAllRowAdjustments()
        {
            rowAdjustmentStatus.Clear();
            Logger.LogInfo("Reset all row adjustment status");
        }


        private void Awake()
        {
            Logger = base.Logger;
            Logger.LogInfo("Silksong Mod Menu loaded.");
            Harmony.CreateAndPatchAll(typeof(SilksongModMenu));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIManager), "GoToExtrasMenu")]
        public static void AddModButtonToExtrasMenu(UIManager __instance)
        {
            Logger.LogInfo("=== GoToExtrasMenu called ===");

            try
            {
                // 首先检查是否已经存在按钮
                if (__instance.extrasMenuScreen != null)
                {
                    var existingButton = __instance.extrasMenuScreen.transform.Find("ModOptionsButton");
                    if (existingButton != null)
                    {
                        Logger.LogInfo("Mod Options button already exists, skipping creation");
                        existingButton.gameObject.SetActive(true);

                        // 更新静态引用
                        modOptionsButton = existingButton.GetComponent<MenuButton>();
                        return;
                    }
                }

                // 检查静态引用是否有效
                if (modOptionsButton != null && modOptionsButton.gameObject != null)
                {
                    Logger.LogInfo("Static reference exists and valid, skipping creation");
                    modOptionsButton.gameObject.SetActive(true);
                    return;
                }

                // 重置引用，准备创建新按钮
                modOptionsButton = null;
                __instance.StartCoroutine(AddModButtonCoroutine(__instance));
            }
            catch (Exception e)
            {
                Logger.LogError($"AddModButtonToExtrasMenu error: {e}");
            }
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIManager), "Start")]
        public static void OnUIManagerStartPost(UIManager __instance)
        {
            try
            {
                // 检查这是否是有效的UIManager实例
                if (__instance != null && __instance == UIManager.instance)
                {
                    Logger.LogInfo("UIManager Start completed, resetting mod menu state");

                    // 只重置我们的状态，不影响游戏逻辑
                    ResetModMenuState();
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"OnUIManagerStartPost error: {e}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIManager), "OnDestroy")]
        public static void OnUIManagerDestroyPost(UIManager __instance)
        {
            try
            {
                Logger.LogInfo("UIManager OnDestroy completed, cleaning up mod menu");

                // 安全清理我们的组件
                SafeCleanupModMenu();
            }
            catch (Exception e)
            {
                Logger.LogError($"OnUIManagerDestroyPost error: {e}");
            }
        }

        private static void ResetModMenuState()
        {
            try
            {
                Logger.LogInfo("Resetting mod menu state...");

                // 检查现有引用是否还有效
                if (modOptionsButton != null && modOptionsButton.gameObject == null)
                {
                    modOptionsButton = null;
                }

                if (modOptionsMenuScreen != null && modOptionsMenuScreen.gameObject == null)
                {
                    modOptionsMenuScreen = null;
                }

                isShowingModMenu = false;

                Logger.LogInfo("Mod menu state reset completed");
            }
            catch (Exception e)
            {
                Logger.LogError($"ResetModMenuState error: {e}");
            }
        }

        private static void SafeCleanupModMenu()
        {
            try
            {
                Logger.LogInfo("Safe cleanup of mod menu...");

                // 安全销毁我们创建的对象
                if (modOptionsButton != null && modOptionsButton.gameObject != null)
                {
                    Destroy(modOptionsButton.gameObject);
                }
                modOptionsButton = null;

                if (modOptionsMenuScreen != null && modOptionsMenuScreen.gameObject != null)
                {
                    Destroy(modOptionsMenuScreen.gameObject);
                }
                modOptionsMenuScreen = null;

                isShowingModMenu = false;
                modsContentParent = null;
                rowPrototype = null;

                Logger.LogInfo("Safe cleanup completed");
            }
            catch (Exception e)
            {
                Logger.LogError($"SafeCleanupModMenu error: {e}");
            }
        }

        // 添加安全的引用重置方法
        private static bool IsModButtonValid()
        {
            try
            {
                return modOptionsButton != null &&
                       modOptionsButton.gameObject != null &&
                       modOptionsButton.gameObject.activeInHierarchy;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void ResetModButtonReference()
        {
            try
            {
                if (modOptionsButton != null && modOptionsButton.gameObject == null)
                {
                    Logger.LogInfo("Mod button GameObject was destroyed, resetting reference");
                    modOptionsButton = null;
                }
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Error checking mod button reference: {e.Message}");
                modOptionsButton = null;
            }
        }


        private static IEnumerator AddModButtonCoroutine(UIManager uiManager)
        {
            yield return new WaitForSeconds(0.2f);
            yield return null;

            try
            {
                // 检查是否已经创建过
                if (modOptionsButton != null && modOptionsButton.gameObject != null)
                {
                    Logger.LogInfo("Button already exists in coroutine, skipping creation");
                    yield break;
                }

                var extrasMenu = uiManager.extrasMenuScreen;
                if (extrasMenu == null)
                {
                    Logger.LogError("Cannot find extrasMenuScreen");
                    yield break;
                }

                var existingButton = extrasMenu.transform.Find("ModOptionsButton");
                if (existingButton != null)
                {
                    Logger.LogInfo("Found existing ModOptionsButton in transform, updating reference");
                    modOptionsButton = existingButton.GetComponent<MenuButton>();
                    yield break;
                }

                Logger.LogInfo($"Found extras menu: {extrasMenu.name}");

                var menuButtons = extrasMenu.GetComponentsInChildren<MenuButton>(true);
                Logger.LogInfo($"Found {menuButtons.Length} MenuButton components in extras menu");

                var creditsButton = menuButtons.FirstOrDefault(mb => mb.name.Contains("Credits"));
                if (creditsButton == null)
                {
                    Logger.LogError("Cannot find Credits button template");
                    yield break;
                }

                Logger.LogInfo($"Using button as template: {creditsButton.name}");

                var modButtonObj = Instantiate(creditsButton.gameObject, creditsButton.transform.parent);
                modButtonObj.name = "ModOptionsButton";
                modButtonObj.SetActive(true);
                modButtonObj.transform.SetSiblingIndex(creditsButton.transform.GetSiblingIndex());

                // 完全清理按钮事件
                CompletelyCleanButton(modButtonObj);

                // 获取MenuButton组件
                modOptionsButton = modButtonObj.GetComponent<MenuButton>();
                if (modOptionsButton == null)
                {
                    Logger.LogError("Failed to get MenuButton component");
                    yield break;
                }

                // 设置文本
                var textComponent = modButtonObj.GetComponentInChildren<Text>();
                if (textComponent != null)
                {
                    textComponent.text = "MOD OPTIONS";
                }

                // 只添加我们的事件
                modOptionsButton.OnSubmitPressed = new UnityEngine.Events.UnityEvent();
                modOptionsButton.OnSubmitPressed.AddListener(() =>
                {
                    Logger.LogInfo("=== Mod Options button clicked! ===");
                    ShowModOptionsMenu(uiManager);
                });

                // 手动定位
                var rectTransform = modButtonObj.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    float yOffset = -100f;
                    rectTransform.anchoredPosition = new Vector2(0, yOffset);
                    Logger.LogInfo($"Manually positioned button at: ({rectTransform.anchoredPosition.x:F2}, {rectTransform.anchoredPosition.y:F2}) (offset: {yOffset}f)");
                }

                Logger.LogInfo("Mod button added to Extras menu successfully!");
            }
            catch (Exception e)
            {
                Logger.LogError($"Error adding mod button: {e}");
            }
        }
        private static void CompletelyCleanButton(GameObject buttonObj)
        {
            try
            {
                Logger.LogInfo($"Completely cleaning button: {buttonObj.name}");

                // 清理所有可能的事件监听器
                var allComponents = buttonObj.GetComponentsInChildren<Component>(true);

                foreach (var comp in allComponents)
                {
                    if (comp == null) continue;

                    // 清理Button组件
                    if (comp is Button btn)
                    {
                        Logger.LogInfo($"Clearing Button.onClick on {btn.gameObject.name}");
                        btn.onClick.RemoveAllListeners();
                        btn.onClick = new Button.ButtonClickedEvent();
                    }

                    // 清理MenuButton组件
                    if (comp is MenuButton mb)
                    {
                        Logger.LogInfo($"Clearing MenuButton events on {mb.gameObject.name}");

                        // 清理OnSubmitPressed
                        if (mb.OnSubmitPressed != null)
                        {
                            mb.OnSubmitPressed.RemoveAllListeners();
                        }
                        mb.OnSubmitPressed = new UnityEngine.Events.UnityEvent();

                        // 使用反射清理所有UnityEvent字段
                        try
                        {
                            var type = mb.GetType();
                            var fields = type.GetFields(System.Reflection.BindingFlags.Public |
                                                       System.Reflection.BindingFlags.NonPublic |
                                                       System.Reflection.BindingFlags.Instance);

                            foreach (var field in fields)
                            {
                                if (typeof(UnityEngine.Events.UnityEventBase).IsAssignableFrom(field.FieldType))
                                {
                                    var unityEvent = field.GetValue(mb) as UnityEngine.Events.UnityEventBase;
                                    if (unityEvent != null)
                                    {
                                        Logger.LogInfo($"Clearing UnityEvent field: {field.Name}");
                                        unityEvent.RemoveAllListeners();
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.LogWarning($"Reflection cleanup error: {e.Message}");
                        }
                    }

                    // 清理EventTrigger组件
                    if (comp is UnityEngine.EventSystems.EventTrigger trigger)
                    {
                        Logger.LogInfo($"Clearing EventTrigger on {trigger.gameObject.name}");
                        if (trigger.triggers != null)
                        {
                            trigger.triggers.Clear();
                        }
                    }
                }

                // 处理本地化组件的依赖关系
                var localizers = buttonObj.GetComponentsInChildren<AutoLocalizeTextUI>(true);
                foreach (var loc in localizers)
                {
                    if (loc != null)
                    {
                        Logger.LogInfo($"Removing AutoLocalizeTextUI from {loc.gameObject.name}");

                        // 先检查依赖关系
                        var platformLoc = loc.GetComponent<PlatformSpecificLocalisation>();
                        if (platformLoc != null)
                        {
                            Logger.LogInfo($"Also removing PlatformSpecificLocalisation from {loc.gameObject.name}");
                            Destroy(platformLoc);
                        }

                        Destroy(loc);
                    }
                }

                Logger.LogInfo($"Button completely cleaned: {buttonObj.name}");
            }
            catch (Exception e)
            {
                Logger.LogError($"CompletelyCleanButton error: {e}");
            }
        }



        // 轻度清理 - 用于Extras按钮（保留更多原生功能）
        private static void LightSanitizeButton(GameObject go)
        {
            try
            {
                Logger.LogInfo($"Light sanitize: {go.name}");

                // 只清理Button.onClick
                foreach (var btn in go.GetComponentsInChildren<Button>(true))
                    btn.onClick.RemoveAllListeners();

                // 只清理MenuButton.OnSubmitPressed
                foreach (var mb in go.GetComponentsInChildren<MenuButton>(true))
                {
                    if (mb.OnSubmitPressed != null)
                        mb.OnSubmitPressed.RemoveAllListeners();
                }

                // 移除本地化
                foreach (var loc in go.GetComponentsInChildren<AutoLocalizeTextUI>(true))
                    if (loc != null) Destroy(loc);

                Logger.LogInfo($"Light sanitize completed: {go.name}");
            }
            catch (Exception e)
            {
                Logger.LogError($"LightSanitizeButton error: {e}");
            }
        }

        // 重度清理 - 用于Mod菜单（移除更多危险组件）
        private static void HeavySanitizeMenu(GameObject go)
        {
            try
            {
                Logger.LogInfo($"Heavy sanitize: {go.name}");

                // 移除所有危险的游戏设置相关组件
                string[] dangerousComponents = {
            "GameMenuOptions",
            "MenuOptionHorizontal",
            "MenuOptionToggle",
            "ControlReminder"
        };

                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    var typeName = mb.GetType().Name;
                    if (dangerousComponents.Any(d => typeName.Contains(d)))
                    {
                        Logger.LogInfo($"Destroying dangerous component: {typeName}");
                        Destroy(mb);
                    }
                }

                // 清理所有Button事件
                foreach (var btn in go.GetComponentsInChildren<Button>(true))
                    btn.onClick.RemoveAllListeners();

                // 清理所有MenuButton事件
                foreach (var mb in go.GetComponentsInChildren<MenuButton>(true))
                {
                    if (mb.OnSubmitPressed != null)
                        mb.OnSubmitPressed.RemoveAllListeners();
                }

                // 移除本地化
                foreach (var loc in go.GetComponentsInChildren<AutoLocalizeTextUI>(true))
                    if (loc != null) Destroy(loc);

                Logger.LogInfo($"Heavy sanitize completed: {go.name}");
            }
            catch (Exception e)
            {
                Logger.LogError($"HeavySanitizeMenu error: {e}");
            }
        }



        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIManager), "GoToExtrasContentMenu")]
        public static void OnReturnToExtrasMenu(UIManager __instance)
        {
            Logger.LogInfo("=== Returning to Extras Menu ===");
            __instance.StartCoroutine(EnsureModButtonExists(__instance));
        }

        private static IEnumerator EnsureModButtonExists(UIManager uiManager)
        {
            yield return new WaitForSeconds(0.1f);
            yield return null;

            var extrasMenu = uiManager.extrasMenuScreen;
            if (extrasMenu != null)
            {
                var modButton = extrasMenu.transform.Find("ModOptionsButton");
                if (modButton != null)
                {
                    modButton.gameObject.SetActive(true);
                    FixButtonPosition(modButton.gameObject);
                }
                else
                {
                    Logger.LogWarning("Mod button not found when returning from content menu, recreating");
                    uiManager.StartCoroutine(AddModButtonCoroutine(uiManager));
                }
            }
        }

        private static void OnModButtonClicked(UIManager uiManager)
        {
            if (isShowingModMenu) return;
            ShowModOptionsMenu(uiManager);
        }

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
            // 修改CreateModOptionsMenu中的Back按钮处理
            if (modOptionsMenuScreen.backButton != null)
            {
                Logger.LogInfo("Setting up Back button...");

                // 彻底清理Back按钮 - 使用CompletelyCleanButton而不是LightSanitizeButton
                CompletelyCleanButton(modOptionsMenuScreen.backButton.gameObject);

                // 额外清理：直接清理MenuButton的所有可能事件
                var backMenuButton = modOptionsMenuScreen.backButton;

                // 清理所有可能的UnityEvent
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

            // 内容区域：找到语言选项的父容器作为“行原型容器”
            SetupModsContent(modMenuObj);

            // 不再放“安全占位行”，让 BuildModsList 直接填充
            modMenuObj.SetActive(false);
            Logger.LogInfo("Mod Options menu created (animations/navigation preserved)");
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


        // 这个函数太危险，直接删除或改成：
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

        // 辅助方法：递归分析层级
        private static void AnalyzeHierarchy(Transform t, string prefix, int depth = 0)
        {
            if (depth > 5) return; // 防止太深

            Logger.LogInfo($"{prefix}{t.name} (active: {t.gameObject.activeSelf})");

            foreach (Transform child in t)
            {
                AnalyzeHierarchy(child, prefix + "  ", depth + 1);
            }
        }

        // 辅助方法：颜色转字符串
        private static string ColorToString(Color c)
        {
            return $"RGBA({c.r:F2}, {c.g:F2}, {c.b:F2}, {c.a:F2})";
        }



        // === 新增：查找行原型（语言选项父节点或任意 MenuSelectable 父节点） ===
        private static GameObject FindRowPrototype(Transform root)
        {
            var gmo = root.GetComponentInChildren<GameMenuOptions>(true);
            if (gmo != null && gmo.languageOption != null)
            {
                var p = gmo.languageOption.transform.parent;
                if (p != null) return p.gameObject;
            }

            var anySel = root.GetComponentsInChildren<MenuSelectable>(true).FirstOrDefault();
            if (anySel != null)
            {
                var p = anySel.transform.parent;
                return p ? p.gameObject : anySel.gameObject;
            }

            return null;
        }

        // === 新增：合成行原型（兜底） ===
        private static GameObject SynthesizeSimpleRow(Transform parent)
        {
            var row = new GameObject("RowPrototype", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)row.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(0, 52);
            var bg = row.GetComponent<Image>();
            bg.color = new Color(1, 1, 1, 0.05f);

            var labelGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var lrt = (RectTransform)labelGO.transform;
            lrt.SetParent(row.transform, false);
            lrt.anchorMin = new Vector2(0, 0.5f);
            lrt.anchorMax = new Vector2(0, 0.5f);
            lrt.pivot = new Vector2(0, 0.5f);
            lrt.anchoredPosition = new Vector2(20, 0);
            var anyText = parent.GetComponentInChildren<Text>(true);
            var ltxt = labelGO.GetComponent<Text>();
            if (anyText && anyText.font) ltxt.font = anyText.font;
            ltxt.fontSize = anyText ? Mathf.Max(22, anyText.fontSize - 4) : 24;
            ltxt.color = Color.white;
            ltxt.text = "Label";

            var right = new GameObject("RightButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var rrt = (RectTransform)right.transform;
            rrt.SetParent(row.transform, false);
            rrt.anchorMin = new Vector2(1, 0.5f);
            rrt.anchorMax = new Vector2(1, 0.5f);
            rrt.pivot = new Vector2(1, 0.5f);
            rrt.sizeDelta = new Vector2(160, 40);
            rrt.anchoredPosition = new Vector2(-20, 0);

            var btTextGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var btrt = (RectTransform)btTextGO.transform;
            btrt.SetParent(right.transform, false);
            btrt.anchorMin = btrt.anchorMax = new Vector2(0.5f, 0.5f);
            btrt.pivot = new Vector2(0.5f, 0.5f);
            var btxt = btTextGO.GetComponent<Text>();
            if (anyText && anyText.font) btxt.font = anyText.font;
            btxt.fontSize = anyText ? Mathf.Max(20, anyText.fontSize - 6) : 22;
            btxt.color = Color.white;
            btxt.text = "On";

            return row;
        }

        private static void BuildModsList()
        {
            Logger.LogInfo("=== BuildModsList START ===");
            Logger.LogInfo($"modOptionsMenuScreen: {modOptionsMenuScreen != null}");
            Logger.LogInfo($"modsContentParent: {modsContentParent != null}");
            Logger.LogInfo($"rowPrototype: {rowPrototype != null}");

            if (modOptionsMenuScreen == null)
            {
                Logger.LogError("modOptionsMenuScreen is null!");
                return;
            }

            if (modsContentParent == null || rowPrototype == null)
            {
                Logger.LogWarning("BuildModsList: content or rowPrototype missing, trying SetupModsContent again.");
                SetupModsContent(modOptionsMenuScreen.gameObject);

                Logger.LogInfo($"After retry - modsContentParent: {modsContentParent != null}");
                Logger.LogInfo($"After retry - rowPrototype: {rowPrototype != null}");

                if (modsContentParent == null || rowPrototype == null)
                {
                    Logger.LogError("BuildModsList failed: content parent or row prototype is null after retry.");
                    return;
                }
            }

            Logger.LogInfo($"Content parent children before clear: {modsContentParent.childCount}");

            // 清除旧行
            for (int i = modsContentParent.childCount - 1; i >= 0; i--)
            {
                var child = modsContentParent.GetChild(i).gameObject;
                if (child == rowPrototype)
                {
                    Logger.LogInfo($"Skipping prototype: {child.name}");
                    continue;
                }
                Logger.LogInfo($"Destroying old row: {child.name}");
                Destroy(child);
            }

            var mods = DiscoverLoadedPluginsForList();
            Logger.LogInfo($"Found {mods.Count} mods");

            if (mods.Count == 0)
            {
                CreateHintRow("No mods detected.");
            }
            else
            {
                foreach (var m in mods)
                {
                    Logger.LogInfo($"Creating row for mod: {m.displayName}");
                    CreateModRow_UseAchievementsStyle(m.displayName, m.enabled, m.onToggle);
                }
            }

            var rt = modsContentParent as RectTransform;
            if (rt) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            Logger.LogInfo($"Content parent children after build: {modsContentParent.childCount}");
            Logger.LogInfo("=== BuildModsList END ===");
        }

        private static void CreateModRow_UseAchievementsStyle(string modName, bool initialEnabled, System.Action<bool> onToggle)
        {
            if (rowPrototype == null || modsContentParent == null)
            {
                Logger.LogError("Cannot create mod row: prototype or parent is null");
                return;
            }

            var newRow = Instantiate(rowPrototype, modsContentParent);
            newRow.name = "Row_" + modName;
            newRow.SetActive(true);

            Logger.LogInfo($"Creating mod row for: {modName}");

            bool currentState = initialEnabled;

            // **保存原始的 Selectable 配置**
            var originalMenuOption = newRow.GetComponentInChildren<MenuOptionHorizontal>(true);
            Navigation originalNav = default;
            bool wasInteractable = true;

            if (originalMenuOption != null)
            {
                originalNav = originalMenuOption.navigation;
                wasInteractable = originalMenuOption.interactable;
                Logger.LogInfo($"Saved original navigation: {originalNav.mode}");
                DestroyImmediate(originalMenuOption);
            }

            // **处理文本**
            var allTexts = newRow.GetComponentsInChildren<Text>(true);
            Text labelText = null;
            Text valueText = null;

            if (allTexts.Length > 0) labelText = allTexts[0];
            if (allTexts.Length > 1) valueText = allTexts[1];

            Logger.LogInfo($"Found {allTexts.Length} Text components");

            // **移除本地化**
            foreach (var text in allTexts)
            {
                if (text == null) continue;

                var platformLocal = text.GetComponent<PlatformSpecificLocalisation>();
                if (platformLocal != null) DestroyImmediate(platformLocal);

                var autoLocal = text.GetComponent<AutoLocalizeTextUI>();
                if (autoLocal != null) DestroyImmediate(autoLocal);
            }

            // 更新显示
            void UpdateDisplay()
            {
                if (labelText != null) labelText.text = modName;
                if (valueText != null) valueText.text = currentState ? "ON" : "OFF";
            }

            UpdateDisplay();

            // ========== 首次点击时动态调整位置 ==========
            void AdjustTextPositionOnFirstInteraction()
            {
                // 检查这一行在本次界面打开后是否已调整过
                if (rowAdjustmentStatus.ContainsKey(modName) && rowAdjustmentStatus[modName])
                {
                    Logger.LogInfo($"[{modName}] Already adjusted in this session");
                    return;
                }

                Logger.LogInfo($"[{modName}] First click detected, adjusting positions...");

                if (labelText != null)
                {
                    var labelRT = labelText.GetComponent<RectTransform>();
                    if (labelRT != null)
                    {
                        Logger.LogInfo($"[{modName}] Label before: {labelRT.anchoredPosition}");

                        // 调整这个值来补偿上跳
                        float offsetY = -9f;

                        labelRT.anchoredPosition = new Vector2(
                            labelRT.anchoredPosition.x,
                            labelRT.anchoredPosition.y + offsetY
                        );

                        Logger.LogInfo($"[{modName}] Label after: {labelRT.anchoredPosition}");
                    }
                }

                if (valueText != null)
                {
                    var valueRT = valueText.GetComponent<RectTransform>();
                    if (valueRT != null)
                    {
                        Logger.LogInfo($"[{modName}] Value before: {valueRT.anchoredPosition}");

                        float offsetY = -10f;

                        valueRT.anchoredPosition = new Vector2(
                            valueRT.anchoredPosition.x,
                            valueRT.anchoredPosition.y + offsetY
                        );

                        Logger.LogInfo($"[{modName}] Value after: {valueRT.anchoredPosition}");
                    }
                }

                // 标记为已调整
                rowAdjustmentStatus[modName] = true;
                Logger.LogInfo($"[{modName}] ✅ Position adjustment completed");
            }
            // ========== 结束动态调整 ==========

            // **找到可交互对象**
            GameObject interactableObj = null;
            Animator[] cursors = null;

            foreach (Transform child in newRow.transform)
            {
                if (child.name.Contains("Option"))
                {
                    interactableObj = child.gameObject;
                    cursors = child.GetComponentsInChildren<Animator>(true);
                    Logger.LogInfo($"Found option object: {child.name}, cursors: {cursors.Length}");
                    break;
                }
            }

            if (interactableObj != null)
            {
                var selectable = interactableObj.AddComponent<Selectable>();
                selectable.transition = Selectable.Transition.None;
                selectable.navigation = originalNav;
                selectable.interactable = wasInteractable;
                Logger.LogInfo("Rebuilt Selectable with original config");

                var eventTrigger = interactableObj.AddComponent<EventTrigger>();

                // ========== ✅ 只在点击时调整 ==========
                var pointerClick = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                pointerClick.callback.AddListener((data) =>
                {
                    AdjustTextPositionOnFirstInteraction(); // 👈 首次点击时调整
                    currentState = !currentState;
                    UpdateDisplay();
                    onToggle?.Invoke(currentState);
                    Logger.LogInfo($"Toggled {modName} to {currentState}");
                });
                eventTrigger.triggers.Add(pointerClick);

                var submit = new EventTrigger.Entry { eventID = EventTriggerType.Submit };
                submit.callback.AddListener((data) =>
                {
                    AdjustTextPositionOnFirstInteraction(); // 👈 首次 Submit 时调整
                    currentState = !currentState;
                    UpdateDisplay();
                    onToggle?.Invoke(currentState);
                    Logger.LogInfo($"Toggled {modName} to {currentState} (Submit)");
                });
                eventTrigger.triggers.Add(submit);
                // ========== 结束点击调整 ==========

                // ========== Hover/Select 不调整位置，只控制光标动画 ==========
                var pointerEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                pointerEnter.callback.AddListener((data) =>
                {
                    // 移除了 AdjustTextPositionOnFirstInteraction()
                    Logger.LogInfo($"PointerEnter on {modName}");

                    selectable.OnPointerEnter(null);

                    if (cursors != null)
                    {
                        foreach (var cursor in cursors)
                        {
                            if (cursor != null && cursor.gameObject.name.Contains("Cursor"))
                            {
                                cursor.SetTrigger("show");
                                cursor.SetBool("selected", true);
                                Logger.LogInfo($"Triggered animator on {cursor.gameObject.name}");
                            }
                        }
                    }
                });
                eventTrigger.triggers.Add(pointerEnter);

                var pointerExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                pointerExit.callback.AddListener((data) =>
                {
                    Logger.LogInfo($"PointerExit on {modName}");
                    selectable.OnPointerExit(null);

                    if (cursors != null)
                    {
                        foreach (var cursor in cursors)
                        {
                            if (cursor != null && cursor.gameObject.name.Contains("Cursor"))
                            {
                                cursor.SetTrigger("hide");
                                cursor.SetBool("selected", false);
                                Logger.LogInfo($"Reset animator on {cursor.gameObject.name}");
                            }
                        }
                    }
                });
                eventTrigger.triggers.Add(pointerExit);

                var select = new EventTrigger.Entry { eventID = EventTriggerType.Select };
                select.callback.AddListener((data) =>
                {
                    // 移除了 AdjustTextPositionOnFirstInteraction()
                    Logger.LogInfo($"Select on {modName}");
                    if (cursors != null)
                    {
                        foreach (var cursor in cursors)
                        {
                            if (cursor != null && cursor.gameObject.name.Contains("Cursor"))
                            {
                                cursor.SetTrigger("show");
                                cursor.SetBool("selected", true);
                            }
                        }
                    }
                });
                eventTrigger.triggers.Add(select);

                var deselect = new EventTrigger.Entry { eventID = EventTriggerType.Deselect };
                deselect.callback.AddListener((data) =>
                {
                    Logger.LogInfo($"Deselect on {modName}");
                    if (cursors != null)
                    {
                        foreach (var cursor in cursors)
                        {
                            if (cursor != null && cursor.gameObject.name.Contains("Cursor"))
                            {
                                cursor.SetTrigger("hide");
                                cursor.SetBool("selected", false);
                            }
                        }
                    }
                });
                eventTrigger.triggers.Add(deselect);

                Logger.LogInfo($"Configured EventTrigger for {modName}");
            }
            else
            {
                Logger.LogError($"Cannot find interactable object for {modName}");
            }
        }

        // 辅助方法: 移除自动本地化
        private static void RemoveAutoLocalize(GameObject obj)
        {
            var autoLocal = obj.GetComponent<AutoLocalizeTextUI>();
            if (autoLocal != null)
            {
                try
                {
                    Destroy(autoLocal);
                    Logger.LogInfo($"Removed AutoLocalizeTextUI from {obj.name}");
                }
                catch (System.Exception e)
                {
                    Logger.LogWarning($"Failed to remove AutoLocalizeTextUI: {e.Message}");
                }
            }
        }

        // 清空 MenuButton 事件
        private static void ClearMenuButtonEvents(MenuButton btn)
        {
            if (btn == null) return;

            Logger.LogInfo($"Clearing MenuButton events on {btn.name}");

            // 清空 OnSubmitPressed UnityEvent
            try
            {
                if (btn.OnSubmitPressed != null)
                {
                    btn.OnSubmitPressed.RemoveAllListeners();
                }
                btn.OnSubmitPressed = new UnityEngine.Events.UnityEvent();
            }
            catch (System.Exception e)
            {
                Logger.LogWarning($"Failed to clear OnSubmitPressed: {e.Message}");
            }

            // 清空 EventTrigger
            var trigger = btn.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (trigger != null)
            {
                Logger.LogInfo($"Clearing EventTrigger on {btn.name}");
                trigger.triggers.Clear();
            }
        }

        // === 新增：Mod 数据源 ===
        private struct PluginItem
        {
            public string displayName;
            public bool enabled;
            public Action<bool> onToggle;
        }

        private static List<PluginItem> DiscoverLoadedPluginsForList()
        {
            var list = new List<PluginItem>();
            try
            {
                foreach (var kv in BepInEx.Bootstrap.Chainloader.PluginInfos)
                {
                    var pi = kv.Value;
                    if (pi?.Metadata == null) continue;
                    if (pi.Metadata.GUID == "com.yourname.silksongmodmenu") continue;

                    var name = string.IsNullOrEmpty(pi.Metadata.Name) ? pi.Metadata.GUID : pi.Metadata.Name;

                    list.Add(new PluginItem
                    {
                        displayName = name,
                        enabled = true, // 可接入配置保存真实状态
                        onToggle = v =>
                        {
                            Logger.LogInfo($"[Mod Toggle] {name} -> {(v ? "On" : "Off")}");
                            // TODO: 写配置/调用 API
                        }
                    });
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"DiscoverLoadedPluginsForList error: {e}");
            }

            return list.OrderBy(p => p.displayName).ToList();
        }

        // === 新增：创建 SectionHeader / 提示行 ===
        private static void CreateSectionHeader(string title)
        {
            var refText = modOptionsMenuScreen.GetComponentsInChildren<Text>(true)
                .OrderByDescending(t => t.fontSize).FirstOrDefault();

            GameObject header = new GameObject("SectionHeader", typeof(RectTransform), typeof(Text));
            header.transform.SetParent(modsContentParent, false);
            var txt = header.GetComponent<Text>();
            txt.text = title;
            txt.fontSize = refText ? Mathf.Max(22, refText.fontSize - 6) : 26;
            txt.color = Color.white;
            if (refText && refText.font) txt.font = refText.font;
        }

        private static void CreateHintRow(string hint)
        {
            var baseText = modOptionsMenuScreen.GetComponentsInChildren<Text>(true).FirstOrDefault();
            var go = new GameObject("HintRow", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(modsContentParent, false);
            var txt = go.GetComponent<Text>();
            txt.text = hint;
            txt.fontSize = baseText ? Mathf.Max(18, baseText.fontSize - 6) : 20;
            txt.color = new Color(1, 1, 1, 0.6f);
            if (baseText && baseText.font) txt.font = baseText.font;
        }

        // === 新增：创建单个 Mod 行 ===
        private static void CreateModRow(string label, bool initial, Action<bool> onChanged)
        {
            var row = UnityEngine.Object.Instantiate(rowPrototype, modsContentParent);
            row.name = "Row_" + label;
            row.SetActive(true);

            SafeStripDangerousOnly(row);

            var texts = row.GetComponentsInChildren<Text>(true).ToList();
            var labelText = texts.OrderByDescending(t => t.fontSize).FirstOrDefault();
            if (labelText != null) labelText.text = label;

            Button button = null;

            var menuSel = row.GetComponentsInChildren<MenuSelectable>(true).FirstOrDefault();
            if (menuSel != null)
            {
                var go = menuSel.gameObject;
                SafeStripDangerousOnly(go);
                button = go.GetComponent<Button>() ?? go.AddComponent<Button>();
            }

            if (button == null) button = row.GetComponentInChildren<Button>(true);

            if (button == null)
            {
                var right = new GameObject("RightButton", typeof(RectTransform), typeof(Image), typeof(Button));
                var rrt = (RectTransform)right.transform;
                rrt.SetParent(row.transform, false);
                rrt.anchorMin = new Vector2(1, 0.5f);
                rrt.anchorMax = new Vector2(1, 0.5f);
                rrt.pivot = new Vector2(1, 0.5f);
                rrt.sizeDelta = new Vector2(160, 40);
                rrt.anchoredPosition = new Vector2(-20, 0);
                button = right.GetComponent<Button>();

                var btnTextGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
                var trt = (RectTransform)btnTextGO.transform;
                trt.SetParent(right.transform, false);
                trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
                trt.pivot = new Vector2(0.5f, 0.5f);

                var anyText = row.GetComponentInChildren<Text>(true);
                var btxt = btnTextGO.GetComponent<Text>();
                if (anyText && anyText.font) btxt.font = anyText.font;
                btxt.fontSize = anyText ? Mathf.Max(20, anyText.fontSize - 2) : 22;
                btxt.color = Color.white;
                btxt.text = initial ? "On" : "Off";
            }

            bool state = initial;
            var rightText = button.GetComponentInChildren<Text>(true);
            void Refresh() { if (rightText) rightText.text = state ? "On" : "Off"; }
            Refresh();

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                state = !state;
                Refresh();
                try { onChanged?.Invoke(state); }
                catch (Exception e) { Logger.LogError($"Toggle handler error for {label}: {e}"); }
            });

            var nav = new Navigation { mode = Navigation.Mode.Automatic };
            button.navigation = nav;
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

            DiagnoseUIHierarchy(modOptionsMenuScreen.gameObject, "BEFORE SHOW");

            // 用原生隐藏 Extras，触发原生出场动画（不要传 disable）
            if (uiManager.extrasMenuScreen != null && uiManager.extrasMenuScreen.gameObject.activeSelf)
            {
                yield return uiManager.HideMenu(uiManager.extrasMenuScreen);
            }


            // 用原生 ShowMenu 展示，触发丝线进场动画
            yield return uiManager.ShowMenu(modOptionsMenuScreen);

            yield return new WaitForSeconds(0.1f); // 等动画稳定
            DiagnoseUIHierarchy(modOptionsMenuScreen.gameObject, "AFTER SHOW (animation done)");

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

        private static IEnumerator ReturnToExtrasMenuCoroutine(UIManager uiManager)
        {
            Logger.LogInfo("Starting return to Extras menu coroutine...");

            // 停止输入
            var inputHandler = Traverse.Create(uiManager).Field("ih").GetValue<InputHandler>();
            if (inputHandler != null)
            {
                inputHandler.StopUIInput();
            }

            // 隐藏Mod菜单
            if (modOptionsMenuScreen != null && modOptionsMenuScreen.gameObject.activeSelf)
            {
                Logger.LogInfo("Hiding mod options menu...");
                yield return uiManager.HideMenu(modOptionsMenuScreen);
            }

            // 重置状态
            isShowingModMenu = false;

            // 显示Extras菜单
            if (uiManager.extrasMenuScreen != null)
            {
                Logger.LogInfo("Showing extras menu...");
                yield return uiManager.ShowMenu(uiManager.extrasMenuScreen);

                // 确保可交互
                var canvasGroup = uiManager.extrasMenuScreen.ScreenCanvasGroup;
                if (canvasGroup != null)
                {
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                    canvasGroup.alpha = 1f;
                }

                // 设置默认高亮
                if (uiManager.extrasMenuScreen.defaultHighlight != null)
                {
                    uiManager.extrasMenuScreen.defaultHighlight.Select();
                }
                else
                {
                    uiManager.extrasMenuScreen.HighlightDefault();
                }
            }
            else
            {
                Logger.LogWarning("ExtrasMenuScreen is null, using GoToExtrasMenu...");
                uiManager.GoToExtrasMenu();
            }

            // 重启输入
            if (inputHandler != null)
            {
                inputHandler.StartUIInput();
            }

            Logger.LogInfo("Return to Extras menu completed");
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

        // 添加清理函数，在适当时候调用
        public static void CleanupModMenu()
        {
            try
            {
                Logger.LogInfo("Cleaning up mod menu references...");

                if (modOptionsButton != null && modOptionsButton.gameObject != null)
                {
                    Destroy(modOptionsButton.gameObject);
                }
                modOptionsButton = null;

                if (modOptionsMenuScreen != null && modOptionsMenuScreen.gameObject != null)
                {
                    Destroy(modOptionsMenuScreen.gameObject);
                }
                modOptionsMenuScreen = null;

                Logger.LogInfo("Mod menu cleanup completed");
            }
            catch (Exception e)
            {
                Logger.LogError($"CleanupModMenu error: {e}");
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

        private static void ManualPositionButton(GameObject button, GameObject referenceButton)
        {
            try
            {
                var buttonRect = button.GetComponent<RectTransform>();
                var referenceRect = referenceButton.GetComponent<RectTransform>();
                if (!buttonRect || !referenceRect) return;

                buttonRect.anchoredPosition = new Vector2(
                    referenceRect.anchoredPosition.x,
                    referenceRect.anchoredPosition.y - 100f
                );
                Logger.LogInfo($"Manually positioned button at: {buttonRect.anchoredPosition} (offset: -100f)");
            }
            catch (Exception e)
            {
                Logger.LogError($"Error in manual positioning: {e}");
            }
        }

        private static void FixButtonPosition(GameObject button)
        {
            try
            {
                var buttonRect = button.GetComponent<RectTransform>();
                if (!buttonRect) return;

                var siblings = button.transform.parent.GetComponentsInChildren<MenuButton>(true)
                    .Where(b => b.gameObject != button && b.gameObject.activeInHierarchy).ToArray();

                if (siblings.Length > 0)
                {
                    var siblingRect = siblings[0].GetComponent<RectTransform>();
                    if (siblingRect != null)
                    {
                        buttonRect.anchoredPosition = new Vector2(
                            siblingRect.anchoredPosition.x,
                            siblingRect.anchoredPosition.y - 100f
                        );
                        Logger.LogInfo($"Fixed button position: {buttonRect.anchoredPosition} (offset: -100f)");
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Error fixing button position: {e}");
            }
        }

        // 超强力 UI 诊断工具 - 递归分析整个菜单的所有可能影响位置的组件
        private static void DiagnoseUIHierarchy(GameObject root, string phase)
        {
            Logger.LogInfo("╔════════════════════════════════════════════════════════════════");
            Logger.LogInfo($"║ UI HIERARCHY DIAGNOSIS - {phase}");
            Logger.LogInfo("╠════════════════════════════════════════════════════════════════");

            // 1. 分析 ScrollRect 及其相关组件
            var scrollRects = root.GetComponentsInChildren<ScrollRect>(true);
            Logger.LogInfo($"║ Found {scrollRects.Length} ScrollRect(s)");

            foreach (var sr in scrollRects)
            {
                Logger.LogInfo("║ ┌─ ScrollRect Analysis ─────────────────────────────────────");
                Logger.LogInfo($"║ │ GameObject: {GetFullPath(sr.transform)}");
                Logger.LogInfo($"║ │ Active: {sr.gameObject.activeSelf}");
                Logger.LogInfo($"║ │ Enabled: {sr.enabled}");
                Logger.LogInfo($"║ │ Vertical: {sr.vertical}");
                Logger.LogInfo($"║ │ Horizontal: {sr.horizontal}");
                Logger.LogInfo($"║ │ VerticalNormalizedPosition: {sr.verticalNormalizedPosition:F4}");
                Logger.LogInfo($"║ │ HorizontalNormalizedPosition: {sr.horizontalNormalizedPosition:F4}");
                Logger.LogInfo($"║ │ Velocity: {sr.velocity}");
                Logger.LogInfo($"║ │ Inertia: {sr.inertia}");
                Logger.LogInfo($"║ │ DecelerationRate: {sr.decelerationRate}");
                Logger.LogInfo($"║ │ ScrollSensitivity: {sr.scrollSensitivity}");

                if (sr.content != null)
                {
                    Logger.LogInfo($"║ │ Content: {sr.content.name}");
                    Logger.LogInfo($"║ │ Content.anchoredPosition: {sr.content.anchoredPosition}");
                    Logger.LogInfo($"║ │ Content.sizeDelta: {sr.content.sizeDelta}");
                    Logger.LogInfo($"║ │ Content.pivot: {sr.content.pivot}");
                    Logger.LogInfo($"║ │ Content.anchorMin: {sr.content.anchorMin}");
                    Logger.LogInfo($"║ │ Content.anchorMax: {sr.content.anchorMax}");
                }
                else
                {
                    Logger.LogInfo($"║ │ Content: NULL");
                }

                if (sr.viewport != null)
                {
                    Logger.LogInfo($"║ │ Viewport: {sr.viewport.name}");
                    Logger.LogInfo($"║ │ Viewport.anchoredPosition: {sr.viewport.anchoredPosition}");
                    Logger.LogInfo($"║ │ Viewport.sizeDelta: {sr.viewport.sizeDelta}");
                }
                else
                {
                    Logger.LogInfo($"║ │ Viewport: NULL");
                }

                Logger.LogInfo("║ └────────────────────────────────────────────────────────────");
            }

            // 2. 分析所有 LayoutGroup 组件（修复后的版本）
            Logger.LogInfo("║");
            Logger.LogInfo("║ LayoutGroup Components:");

            var allLayoutGroups = root.GetComponentsInChildren<LayoutGroup>(true);
            Logger.LogInfo($"║ Found {allLayoutGroups.Length} LayoutGroup(s) total");

            foreach (var layout in allLayoutGroups)
            {
                AnalyzeLayoutGroupUnified(layout);
            }

            // 3. 分析所有 ContentSizeFitter
            Logger.LogInfo("║");
            Logger.LogInfo("║ ContentSizeFitter Components:");
            var sizeFitters = root.GetComponentsInChildren<ContentSizeFitter>(true);
            Logger.LogInfo($"║ Found {sizeFitters.Length} ContentSizeFitter(s)");

            foreach (var csf in sizeFitters)
            {
                Logger.LogInfo($"║ ├─ {GetFullPath(csf.transform)}");
                Logger.LogInfo($"║ │  ├─ Enabled: {csf.enabled}");
                Logger.LogInfo($"║ │  ├─ HorizontalFit: {csf.horizontalFit}");
                Logger.LogInfo($"║ │  └─ VerticalFit: {csf.verticalFit}");
            }

            // 4. 分析所有 RectTransform 的位置（只显示非零的）
            Logger.LogInfo("║");
            Logger.LogInfo("║ RectTransform Positions (non-zero only):");
            var allRects = root.GetComponentsInChildren<RectTransform>(true);

            foreach (var rt in allRects)
            {
                if (rt.anchoredPosition != Vector2.zero || rt.anchoredPosition3D.z != 0)
                {
                    Logger.LogInfo($"║ ├─ {GetFullPath(rt)}");
                    Logger.LogInfo($"║ │  ├─ anchoredPosition: {rt.anchoredPosition}");
                    Logger.LogInfo($"║ │  ├─ anchoredPosition3D: {rt.anchoredPosition3D}");
                    Logger.LogInfo($"║ │  ├─ localPosition: {rt.localPosition}");
                    Logger.LogInfo($"║ │  ├─ sizeDelta: {rt.sizeDelta}");
                    Logger.LogInfo($"║ │  ├─ pivot: {rt.pivot}");
                    Logger.LogInfo($"║ │  ├─ anchorMin: {rt.anchorMin}");
                    Logger.LogInfo($"║ │  └─ anchorMax: {rt.anchorMax}");
                }
            }

            // 5. 分析所有可能影响布局的自定义组件
            Logger.LogInfo("║");
            Logger.LogInfo("║ Custom Layout Components:");

            var allComponents = root.GetComponentsInChildren<MonoBehaviour>(true);
            var layoutRelatedTypes = new[] { "Layout", "Position", "Scroll", "Fitter", "Align" };

            foreach (var comp in allComponents)
            {
                if (comp == null) continue;
                var typeName = comp.GetType().Name;

                if (layoutRelatedTypes.Any(keyword => typeName.Contains(keyword)))
                {
                    Logger.LogInfo($"║ ├─ {typeName} on {GetFullPath(comp.transform)}");
                    Logger.LogInfo($"║ │  └─ Enabled: {comp.enabled}");
                }
            }

            // 6. 分析 Canvas 和 CanvasScaler
            Logger.LogInfo("║");
            Logger.LogInfo("║ Canvas Components:");
            var canvases = root.GetComponentsInChildren<Canvas>(true);

            foreach (var canvas in canvases)
            {
                Logger.LogInfo($"║ ├─ Canvas on {GetFullPath(canvas.transform)}");
                Logger.LogInfo($"║ │  ├─ RenderMode: {canvas.renderMode}");
                Logger.LogInfo($"║ │  ├─ SortingOrder: {canvas.sortingOrder}");

                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    Logger.LogInfo($"║ │  ├─ CanvasScaler.uiScaleMode: {scaler.uiScaleMode}");
                    Logger.LogInfo($"║ │  ├─ CanvasScaler.scaleFactor: {scaler.scaleFactor}");
                    Logger.LogInfo($"║ │  └─ CanvasScaler.referenceResolution: {scaler.referenceResolution}");
                }
            }

            Logger.LogInfo("╚════════════════════════════════════════════════════════════════");
        }


        // 辅助方法：分析 LayoutGroup
        private static void AnalyzeLayoutGroup(HorizontalOrVerticalLayoutGroup layout, string typeName)
        {
            Logger.LogInfo($"║ ├─ {typeName} on {GetFullPath(layout.transform)}");
            Logger.LogInfo($"║ │  ├─ Enabled: {layout.enabled}");
            Logger.LogInfo($"║ │  ├─ Padding: L:{layout.padding.left} R:{layout.padding.right} T:{layout.padding.top} B:{layout.padding.bottom}");
            Logger.LogInfo($"║ │  ├─ Spacing: {layout.spacing}");
            Logger.LogInfo($"║ │  ├─ ChildAlignment: {layout.childAlignment}");
            Logger.LogInfo($"║ │  ├─ ChildControlWidth: {layout.childControlWidth}");
            Logger.LogInfo($"║ │  ├─ ChildControlHeight: {layout.childControlHeight}");
            Logger.LogInfo($"║ │  ├─ ChildForceExpandWidth: {layout.childForceExpandWidth}");
            Logger.LogInfo($"║ │  └─ ChildForceExpandHeight: {layout.childForceExpandHeight}");
        }

        // 辅助方法：统一分析所有 LayoutGroup
        private static void AnalyzeLayoutGroupUnified(LayoutGroup layout)
        {
            string typeName = layout.GetType().Name;
            Logger.LogInfo($"║ ├─ {typeName} on {GetFullPath(layout.transform)}");
            Logger.LogInfo($"║ │  ├─ Enabled: {layout.enabled}");
            Logger.LogInfo($"║ │  ├─ Padding: L:{layout.padding.left} R:{layout.padding.right} T:{layout.padding.top} B:{layout.padding.bottom}");
            Logger.LogInfo($"║ │  ├─ ChildAlignment: {layout.childAlignment}");

            // 根据具体类型显示额外信息
            if (layout is VerticalLayoutGroup vLayout)
            {
                Logger.LogInfo($"║ │  ├─ Spacing: {vLayout.spacing}");
                Logger.LogInfo($"║ │  ├─ ChildControlWidth: {vLayout.childControlWidth}");
                Logger.LogInfo($"║ │  ├─ ChildControlHeight: {vLayout.childControlHeight}");
                Logger.LogInfo($"║ │  ├─ ChildForceExpandWidth: {vLayout.childForceExpandWidth}");
                Logger.LogInfo($"║ │  └─ ChildForceExpandHeight: {vLayout.childForceExpandHeight}");
            }
            else if (layout is HorizontalLayoutGroup hLayout)
            {
                Logger.LogInfo($"║ │  ├─ Spacing: {hLayout.spacing}");
                Logger.LogInfo($"║ │  ├─ ChildControlWidth: {hLayout.childControlWidth}");
                Logger.LogInfo($"║ │  ├─ ChildControlHeight: {hLayout.childControlHeight}");
                Logger.LogInfo($"║ │  ├─ ChildForceExpandWidth: {hLayout.childForceExpandWidth}");
                Logger.LogInfo($"║ │  └─ ChildForceExpandHeight: {hLayout.childForceExpandHeight}");
            }
            else if (layout is GridLayoutGroup gridLayout)
            {
                Logger.LogInfo($"║ │  ├─ CellSize: {gridLayout.cellSize}");
                Logger.LogInfo($"║ │  ├─ Spacing: {gridLayout.spacing}");
                Logger.LogInfo($"║ │  ├─ StartCorner: {gridLayout.startCorner}");
                Logger.LogInfo($"║ │  ├─ StartAxis: {gridLayout.startAxis}");
                Logger.LogInfo($"║ │  ├─ Constraint: {gridLayout.constraint}");
                Logger.LogInfo($"║ │  └─ ConstraintCount: {gridLayout.constraintCount}");
            }
        }

        // 辅助方法：获取完整路径
        private static string GetFullPath(Transform transform)
        {
            if (transform == null) return "NULL";

            string path = transform.name;
            Transform current = transform.parent;

            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }


    }
}