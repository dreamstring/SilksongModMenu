using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Linq;

[BepInPlugin("com.yourname.silksongmodmenu", "Silksong Mod Menu", "1.0.0")]
public class SilksongModMenu : BaseUnityPlugin
{
    private static MenuScreen modOptionsMenuScreen;
    private static bool isShowingModMenu = false;
    internal new static ManualLogSource Logger;

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
        __instance.StartCoroutine(AddModButtonCoroutine(__instance));
    }

    private static IEnumerator AddModButtonCoroutine(UIManager uiManager)
    {
        yield return new WaitForSeconds(0.2f);
        yield return null;

        try
        {
            MenuScreen extrasMenu = uiManager.extrasMenuScreen;
            if (extrasMenu == null)
            {
                Logger.LogError("Extras menu screen is null");
                yield break;
            }

            Logger.LogInfo($"Found extras menu: {extrasMenu.name}");

            // 检查是否已存在Mod按钮
            Transform existingButton = extrasMenu.transform.Find("ModOptionsButton");
            if (existingButton != null)
            {
                Logger.LogInfo("Mod button already exists, ensuring it's active");
                existingButton.gameObject.SetActive(true);
                FixButtonPosition(existingButton.gameObject);
                yield break;
            }

            // 查找所有MenuButton组件
            MenuButton[] allButtons = extrasMenu.GetComponentsInChildren<MenuButton>(true);
            Logger.LogInfo($"Found {allButtons.Length} MenuButton components in extras menu");

            if (allButtons.Length == 0)
            {
                Logger.LogError("No MenuButton components found in extras menu");
                yield break;
            }

            // 找到Credits按钮
            MenuButton creditsButton = null;
            foreach (MenuButton button in allButtons)
            {
                Text refTextComponent = button.GetComponentInChildren<Text>();
                if (refTextComponent != null && refTextComponent.text.ToLower().Contains("credits"))
                {
                    creditsButton = button;
                    Logger.LogInfo($"Found Credits button: {button.name}");
                    break;
                }
            }

            // 如果没有找到Credits按钮，使用第一个按钮
            if (creditsButton == null)
            {
                creditsButton = allButtons[0];
                Logger.LogInfo($"Using first button as template: {creditsButton.name}");
            }

            // 复制按钮
            GameObject modButtonObj = Object.Instantiate(creditsButton.gameObject, creditsButton.transform.parent);
            modButtonObj.name = "ModOptionsButton";
            Logger.LogInfo("Cloned button successfully");

            // 确保按钮激活
            modButtonObj.SetActive(true);

            // 设置按钮位置 - 在Credits按钮前面
            int creditsIndex = creditsButton.transform.GetSiblingIndex();
            modButtonObj.transform.SetSiblingIndex(creditsIndex);
            Logger.LogInfo($"Placed Mod button at index {creditsIndex}");

            // 获取MenuButton组件
            MenuButton modButton = modButtonObj.GetComponent<MenuButton>();
            if (modButton == null)
            {
                Logger.LogError("No MenuButton component found on mod button");
                yield break;
            }

            // 完全重置按钮组件 - 确保事件被正确劫持
            ResetButtonComponents(modButtonObj, creditsButton.gameObject);

            // 修改按钮文本
            Text textComponent = modButtonObj.GetComponentInChildren<Text>();
            if (textComponent != null)
            {
                textComponent.text = "Mod Options";
                Logger.LogInfo($"Button text set to: {textComponent.text}");
            }

            // 清除原有事件并设置新的点击事件
            modButton.OnSubmitPressed = new UnityEngine.Events.UnityEvent();
            modButton.OnSubmitPressed.AddListener(() => {
                Logger.LogInfo("=== Mod Options button clicked! ===");
                OnModButtonClicked(uiManager);
            });

            // 重置导航设置
            Navigation navigation = new Navigation();
            navigation.mode = Navigation.Mode.Automatic;
            modButton.navigation = navigation;

            // 手动设置位置 - 使用固定偏移量
            ManualPositionButton(modButtonObj, creditsButton.gameObject);

            // 强制重新构建布局
            LayoutRebuilder.ForceRebuildLayoutImmediate(modButton.transform.parent as RectTransform);
            Logger.LogInfo("Layout rebuilt");

            Logger.LogInfo("Mod button added to Extras menu successfully!");
        }
        catch (System.Exception e)
        {
            Logger.LogError($"Error adding mod button: {e}");
            Logger.LogError($"Stack trace: {e.StackTrace}");
        }
    }

    // 完全重置按钮组件，确保不继承原有功能
    private static void ResetButtonComponents(GameObject newButton, GameObject templateButton)
    {
        try
        {
            Logger.LogInfo("Resetting button components...");

            // 移除所有可能的事件监听器
            var components = newButton.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component is Behaviour behaviour && component != newButton.transform)
                {
                    // 保留必要的组件，移除其他可能的事件组件
                    if (!(behaviour is MenuButton) &&
                        !(behaviour is CanvasRenderer) &&
                        !(behaviour is RectTransform) &&
                        !(behaviour is Image) &&
                        !(behaviour is Text))
                    {
                        Object.Destroy(behaviour);
                        Logger.LogInfo($"Removed component: {behaviour.GetType().Name}");
                    }
                }
            }

            // 确保MenuButton组件正确设置
            MenuButton menuButton = newButton.GetComponent<MenuButton>();
            if (menuButton != null)
            {
                menuButton.OnSubmitPressed = new UnityEngine.Events.UnityEvent();
                menuButton.interactable = true;
                Logger.LogInfo("Reset MenuButton component");
            }

            // 确保按钮尺寸正确
            RectTransform rectTransform = newButton.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                RectTransform templateRect = templateButton.GetComponent<RectTransform>();
                if (templateRect != null)
                {
                    rectTransform.anchoredPosition = templateRect.anchoredPosition;
                    rectTransform.sizeDelta = templateRect.sizeDelta;
                    rectTransform.anchorMin = templateRect.anchorMin;
                    rectTransform.anchorMax = templateRect.anchorMax;
                    rectTransform.pivot = templateRect.pivot;
                    Logger.LogInfo("Reset RectTransform properties");
                }
            }

            // 移除本地化组件
            AutoLocalizeTextUI localizeText = newButton.GetComponentInChildren<AutoLocalizeTextUI>();
            if (localizeText != null)
            {
                Object.Destroy(localizeText);
                Logger.LogInfo("Removed AutoLocalizeTextUI component");
            }

            Logger.LogInfo("Button components reset successfully");
        }
        catch (System.Exception e)
        {
            Logger.LogError($"Error resetting button components: {e}");
        }
    }

    // 手动定位按钮 - 使用固定偏移量
    private static void ManualPositionButton(GameObject button, GameObject referenceButton)
    {
        try
        {
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            RectTransform referenceRect = referenceButton.GetComponent<RectTransform>();

            if (buttonRect == null || referenceRect == null)
            {
                Logger.LogError("Cannot manually position - missing RectTransform components");
                return;
            }

            // 使用固定偏移量 -100f
            buttonRect.anchoredPosition = new Vector2(
                referenceRect.anchoredPosition.x,
                referenceRect.anchoredPosition.y - 100f
            );

            Logger.LogInfo($"Manually positioned button at: {buttonRect.anchoredPosition} (offset: -100f)");
        }
        catch (System.Exception e)
        {
            Logger.LogError($"Error in manual positioning: {e}");
        }
    }

    // 修复按钮位置
    private static void FixButtonPosition(GameObject button)
    {
        try
        {
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            if (buttonRect == null) return;

            // 查找其他按钮作为参考
            MenuButton[] siblings = button.transform.parent.GetComponentsInChildren<MenuButton>()
                .Where(b => b.gameObject != button && b.gameObject.activeInHierarchy)
                .ToArray();

            if (siblings.Length > 0)
            {
                RectTransform siblingRect = siblings[0].GetComponent<RectTransform>();
                if (siblingRect != null)
                {
                    // 使用固定偏移量 -100f
                    buttonRect.anchoredPosition = new Vector2(
                        siblingRect.anchoredPosition.x,
                        siblingRect.anchoredPosition.y - 100f
                    );
                    Logger.LogInfo($"Fixed button position: {buttonRect.anchoredPosition} (offset: -100f)");
                }
            }
        }
        catch (System.Exception e)
        {
            Logger.LogError($"Error fixing button position: {e}");
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

        MenuScreen extrasMenu = uiManager.extrasMenuScreen;
        if (extrasMenu != null)
        {
            Transform modButton = extrasMenu.transform.Find("ModOptionsButton");
            if (modButton != null)
            {
                Logger.LogInfo("Ensuring Mod button is active after returning from content menu");
                modButton.gameObject.SetActive(true);

                // 确保按钮位置正确
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

        Logger.LogInfo("Mod Options button was clicked!");
        ShowModOptionsMenu(uiManager);
    }

    private static void ShowModOptionsMenu(UIManager uiManager)
    {
        Logger.LogInfo("Showing Mod Options menu...");

        // 如果菜单不存在，创建它
        if (modOptionsMenuScreen == null)
        {
            CreateModOptionsMenu(uiManager);
        }

        // 显示菜单
        uiManager.StartCoroutine(ShowModMenuCoroutine(uiManager));
    }

    private static void CreateModOptionsMenu(UIManager uiManager)
    {
        Logger.LogInfo("Creating Mod Options menu...");

        // 使用gameOptionsMenuScreen作为模板
        MenuScreen templateMenu = uiManager.gameOptionsMenuScreen;
        if (templateMenu == null)
        {
            Logger.LogError("Cannot find gameOptionsMenuScreen template");
            return;
        }

        // 克隆模板
        GameObject modMenuObj = Object.Instantiate(templateMenu.gameObject, templateMenu.transform.parent);
        modMenuObj.name = "ModOptionsMenuScreen";
        modOptionsMenuScreen = modMenuObj.GetComponent<MenuScreen>();

        if (modOptionsMenuScreen == null)
        {
            Logger.LogError("Failed to get MenuScreen component from cloned object");
            return;
        }

        // 设置CanvasGroup - 确保正确设置
        CanvasGroup canvasGroup = modOptionsMenuScreen.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = modMenuObj.AddComponent<CanvasGroup>();
        }
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // 修改标题
        UpdateMenuTitle(modMenuObj);

        // 设置返回按钮 - 完全劫持事件
        if (modOptionsMenuScreen.backButton != null)
        {
            // 完全重置返回按钮
            ResetButtonComponents(modOptionsMenuScreen.backButton.gameObject, modOptionsMenuScreen.backButton.gameObject);

            modOptionsMenuScreen.backButton.OnSubmitPressed = new UnityEngine.Events.UnityEvent();
            modOptionsMenuScreen.backButton.OnSubmitPressed.AddListener(() => {
                Logger.LogInfo("Returning from Mod Options");
                HideModOptionsMenu(uiManager);
            });

            // 设置默认高亮为返回按钮
            modOptionsMenuScreen.defaultHighlight = modOptionsMenuScreen.backButton;
        }

        // 清理内容区域，但保留一个选项作为占位符
        SetupPlaceholderContent(modMenuObj);

        // 初始隐藏
        modMenuObj.SetActive(false);
        Logger.LogInfo("Mod Options menu created using gameOptions template");
    }

    private static void UpdateMenuTitle(GameObject menuObj)
    {
        // 查找标题文本
        Text[] allTexts = menuObj.GetComponentsInChildren<Text>(true);
        foreach (Text text in allTexts)
        {
            // 查找游戏选项相关的标题
            if (text.text.ToLower().Contains("game") ||
                text.text.ToLower().Contains("options") ||
                IsTitleText(text))
            {
                text.text = "MOD OPTIONS";
                // 移除本地化
                AutoLocalizeTextUI localize = text.GetComponent<AutoLocalizeTextUI>();
                if (localize != null) Object.Destroy(localize);
                Logger.LogInfo($"Updated menu title to: {text.text}");
                break;
            }
        }
    }

    private static void SetupPlaceholderContent(GameObject menuObj)
    {
        // 找到内容区域
        Transform contentParent = FindContentParent(menuObj);
        if (contentParent == null)
        {
            Logger.LogError("Cannot find content parent for placeholder");
            return;
        }

        // 找到第一个选项按钮作为模板
        MenuButton[] optionButtons = contentParent.GetComponentsInChildren<MenuButton>(true);
        if (optionButtons.Length == 0)
        {
            Logger.LogWarning("No option buttons found in template");
            return;
        }

        // 保留第一个按钮作为占位符，修改其他按钮
        MenuButton placeholderButton = optionButtons[0];

        // 重置第一个按钮
        ResetButtonComponents(placeholderButton.gameObject, placeholderButton.gameObject);

        // 修改文本为占位符
        Text textComponent = placeholderButton.GetComponentInChildren<Text>();
        if (textComponent != null)
        {
            textComponent.text = "Mod list will be displayed here";
            Logger.LogInfo("Set placeholder button text");
        }

        // 禁用交互但保持可见
        placeholderButton.interactable = false;

        // 删除其他选项按钮
        for (int i = 1; i < optionButtons.Length; i++)
        {
            if (optionButtons[i] != null && optionButtons[i].gameObject != placeholderButton.gameObject)
            {
                Object.Destroy(optionButtons[i].gameObject);
            }
        }

        Logger.LogInfo("Setup placeholder content using existing template button");
    }

    private static Transform FindContentParent(GameObject menuObj)
    {
        // 查找可能的容器 - 游戏选项菜单通常使用VerticalLayoutGroup
        var layoutGroups = menuObj.GetComponentsInChildren<VerticalLayoutGroup>(true);
        foreach (var layout in layoutGroups)
        {
            if (layout.transform.childCount > 1) // 可能是内容容器
            {
                return layout.transform;
            }
        }

        // 备用：查找包含多个按钮的父级
        var buttons = menuObj.GetComponentsInChildren<MenuButton>(true);
        if (buttons.Length > 0)
        {
            Transform commonParent = buttons[0].transform.parent;
            // 检查是否所有按钮都在同一个父级下
            if (buttons.All(btn => btn.transform.parent == commonParent))
            {
                return commonParent;
            }
        }

        return menuObj.transform;
    }

    private static bool IsTitleText(Text textComponent)
    {
        RectTransform rect = textComponent.GetComponent<RectTransform>();
        if (rect != null)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float maxY = corners.Max(corner => corner.y);

            if (maxY > Screen.height * 0.7f) // 标题通常在顶部
            {
                return true;
            }
        }
        return false;
    }

    private static IEnumerator ShowModMenuCoroutine(UIManager uiManager)
    {
        if (modOptionsMenuScreen == null) yield break;

        isShowingModMenu = true;

        // 停止UI输入
        var inputHandler = Traverse.Create(uiManager).Field("ih").GetValue<InputHandler>();
        if (inputHandler != null)
        {
            inputHandler.StopUIInput();
        }

        // 隐藏Extras菜单 - 使用UIManager的方法
        if (uiManager.extrasMenuScreen != null && uiManager.extrasMenuScreen.gameObject.activeSelf)
        {
            yield return uiManager.HideMenu(uiManager.extrasMenuScreen, disable: true);
        }

        // 显示Mod菜单
        modOptionsMenuScreen.gameObject.SetActive(true);

        // 确保CanvasGroup正确设置
        CanvasGroup modCanvasGroup = modOptionsMenuScreen.GetComponent<CanvasGroup>();
        if (modCanvasGroup != null)
        {
            modCanvasGroup.alpha = 0f;
            modCanvasGroup.interactable = false;
            modCanvasGroup.blocksRaycasts = false;

            // 淡入效果
            yield return uiManager.FadeInCanvasGroup(modCanvasGroup);

            // 确保在淡入后启用交互
            modCanvasGroup.interactable = true;
            modCanvasGroup.blocksRaycasts = true;
        }

        // 设置默认高亮 - 确保使用正确的backButton
        if (modOptionsMenuScreen.backButton != null)
        {
            // 等待一帧确保UI已更新
            yield return null;
            modOptionsMenuScreen.backButton.Select();
        }
        else
        {
            modOptionsMenuScreen.HighlightDefault();
        }

        // 恢复UI输入
        if (inputHandler != null)
        {
            inputHandler.StartUIInput();
        }

        Logger.LogInfo("Mod Options menu shown successfully");
    }

    private static void HideModOptionsMenu(UIManager uiManager)
    {
        if (modOptionsMenuScreen != null && modOptionsMenuScreen.gameObject.activeSelf)
        {
            uiManager.StartCoroutine(HideModMenuCoroutine(uiManager));
        }
    }

    private static IEnumerator HideModMenuCoroutine(UIManager uiManager)
    {
        var inputHandler = Traverse.Create(uiManager).Field("ih").GetValue<InputHandler>();
        if (inputHandler != null)
        {
            inputHandler.StopUIInput();
        }

        if (modOptionsMenuScreen != null)
        {
            CanvasGroup modCanvasGroup = modOptionsMenuScreen.GetComponent<CanvasGroup>();
            if (modCanvasGroup != null)
            {
                // 禁用交互
                modCanvasGroup.interactable = false;
                modCanvasGroup.blocksRaycasts = false;

                // 淡出效果
                yield return uiManager.FadeOutCanvasGroup(modCanvasGroup);
            }
            modOptionsMenuScreen.gameObject.SetActive(false);
        }

        // 显示Extras菜单
        yield return uiManager.GoToExtrasMenu();

        if (inputHandler != null)
        {
            inputHandler.StartUIInput();
        }

        isShowingModMenu = false;
    }
}