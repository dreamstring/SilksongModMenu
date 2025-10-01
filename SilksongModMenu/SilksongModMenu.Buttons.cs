// SilksongModMenu.Buttons.cs
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ModUINamespace
{
    public partial class SilksongModMenu
    {
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
    }
}
