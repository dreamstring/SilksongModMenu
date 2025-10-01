using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
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
            modsContentParent = FindContentParent(menuObj);
            if (modsContentParent == null)
            {
                Logger.LogError("Content parent not found.");
                return;
            }

            // 清空已有行（避免残留原设置项），但不要破坏父布局组件
            for (int i = modsContentParent.childCount - 1; i >= 0; i--)
            {
                var child = modsContentParent.GetChild(i).gameObject;
                Destroy(child);
            }

            // 新的 rowPrototype：语言选项那一行的父容器
            var gmo = menuObj.GetComponentInChildren<GameMenuOptions>(true);
            if (gmo != null && gmo.languageOption != null)
            {
                var p = gmo.languageOption.transform.parent;
                if (p != null)
                {
                    rowPrototype = p.gameObject;
                }
            }

            // 如果拿不到，兜底用通用 MenuSelectable 行或合成
            if (rowPrototype == null)
            {
                var anySel = menuObj.GetComponentsInChildren<MenuSelectable>(true).FirstOrDefault();
                if (anySel != null)
                {
                    var p = anySel.transform.parent;
                    rowPrototype = p ? p.gameObject : anySel.gameObject;
                }
            }
            if (rowPrototype == null)
            {
                rowPrototype = SynthesizeSimpleRow(modsContentParent);
            }

            // 关键：不要禁用 rowPrototype 上的脚本，也不要 Sanitize。
            rowPrototype.SetActive(false);
            if (rowPrototype.transform.parent != modsContentParent)
                rowPrototype.transform.SetParent(modsContentParent, false);
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

        // === 新增：构建 Mod 列表 ===
        private static void BuildModsList()
        {
            if (modOptionsMenuScreen == null) return;
            if (modsContentParent == null || rowPrototype == null)
            {
                Logger.LogWarning("BuildModsList: content or rowPrototype missing, trying SetupModsContent again.");
                SetupModsContent(modOptionsMenuScreen.gameObject);
                if (modsContentParent == null || rowPrototype == null)
                {
                    Logger.LogError("BuildModsList failed: content parent or row prototype is null.");
                    return;
                }
            }

            // 清除旧行（保留原型）
            for (int i = modsContentParent.childCount - 1; i >= 0; i--)
            {
                var child = modsContentParent.GetChild(i).gameObject;
                if (child == rowPrototype) continue;
                Destroy(child);
            }

            var mods = DiscoverLoadedPluginsForList();
            if (mods.Count == 0)
            {
                CreateHintRow("No mods detected.");
            }
            else
            {
                foreach (var m in mods)
                    CreateModRow_UseLanguageStyle(m.displayName, m.enabled, m.onToggle);
            }

            var rt = modsContentParent as RectTransform;
            if (rt) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        private static void CreateModRow_UseLanguageStyle(string label, bool initial, Action<bool> onChanged)
        {
            var row = UnityEngine.Object.Instantiate(rowPrototype, modsContentParent);
            row.name = "Row_" + label;
            row.SetActive(true);

            // 更精确的文本识别方法
            Text leftLabel = null;
            Text rightValue = null;

            var texts = row.GetComponentsInChildren<Text>(true);
            if (texts != null && texts.Length > 0)
            {
                // 方法1：按层级顺序识别（通常第一个是标签，第二个是值）
                if (texts.Length >= 2)
                {
                    leftLabel = texts[0];
                    rightValue = texts[1];
                }
                else if (texts.Length == 1)
                {
                    leftLabel = texts[0];
                }

                // 方法2：如果方法1不准确，按RectTransform的实际屏幕位置判断
                if (texts.Length >= 2)
                {
                    var sortedByX = texts.OrderBy(t => {
                        var rt = t.GetComponent<RectTransform>();
                        if (rt != null)
                        {
                            Vector3[] corners = new Vector3[4];
                            rt.GetWorldCorners(corners);
                            return corners[0].x; // 左下角的x坐标
                        }
                        return 0f;
                    }).ToArray();

                    leftLabel = sortedByX[0];
                    rightValue = sortedByX[sortedByX.Length - 1];
                }
            }

            // 设置文本内容
            if (leftLabel != null)
            {
                leftLabel.text = label;
                // 移除标签的本地化
                var loc = leftLabel.GetComponent<AutoLocalizeTextUI>();
                if (loc) Destroy(loc);
            }

            if (rightValue != null)
            {
                rightValue.text = initial ? "On" : "Off";
                // 移除值的本地化
                var loc = rightValue.GetComponent<AutoLocalizeTextUI>();
                if (loc) Destroy(loc);
            }

            // 交互逻辑保持不变...
            var selectable = row.GetComponentInChildren<Selectable>(true);
            if (selectable != null)
            {
                var btn = row.GetComponentInChildren<Button>(true);
                if (btn == null)
                {
                    btn = row.GetComponent<Button>();
                    if (btn == null) btn = row.AddComponent<Button>();
                    if (btn.transition == Selectable.Transition.None)
                        btn.transition = Selectable.Transition.ColorTint;
                }

                bool state = initial;
                void Refresh()
                {
                    if (rightValue) rightValue.text = state ? "On" : "Off";
                }
                Refresh();

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    state = !state;
                    Refresh();
                    try { onChanged?.Invoke(state); }
                    catch (Exception e) { Logger.LogError($"Toggle handler error for {label}: {e}"); }
                });

                if (selectable.navigation.mode != Navigation.Mode.Explicit)
                {
                    var nav = new Navigation { mode = Navigation.Mode.Explicit };
                    selectable.navigation = nav;
                }

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

            // 用原生隐藏 Extras，触发原生出场动画（不要传 disable）
            if (uiManager.extrasMenuScreen != null && uiManager.extrasMenuScreen.gameObject.activeSelf)
            {
                yield return uiManager.HideMenu(uiManager.extrasMenuScreen);
            }

            // 构建列表（不要破坏原生组件）
            BuildModsList();

            // 用原生 ShowMenu 展示，触发丝线进场动画
            yield return uiManager.ShowMenu(modOptionsMenuScreen);

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
    }
}