using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Linq;

namespace SilksongModManagerMod
{
    public static class ModOptionsMenu
    {
        private static MenuScreen modOptionsMenuScreen;
        private static UIManager uiManager;

        public static void Initialize(UIManager manager)
        {
            uiManager = manager;
        }

        public static void Show()
        {
            if (uiManager == null)
            {
                SilksongModMenu.Logger.LogError("UIManager not initialized for ModOptionsMenu");
                return;
            }

            // 如果界面不存在，则创建
            if (modOptionsMenuScreen == null)
            {
                CreateModOptionsMenu();
            }

            // 使用UIManager的菜单显示系统
            uiManager.StartCoroutine(ShowModMenuCoroutine());
        }

        private static void CreateModOptionsMenu()
        {
            SilksongModMenu.Logger.LogInfo("Creating Mod Options menu...");

            // 使用游戏现有的MenuScreen模板创建
            // 克隆一个现有的菜单屏幕作为模板
            MenuScreen templateMenu = uiManager.gameOptionsMenuScreen;
            if (templateMenu == null)
            {
                SilksongModMenu.Logger.LogError("Cannot find template menu for Mod Options");
                return;
            }

            // 克隆模板
            GameObject modMenuObj = Object.Instantiate(templateMenu.gameObject, templateMenu.transform.parent);
            modMenuObj.name = "ModOptionsMenuScreen";

            // 获取MenuScreen组件
            modOptionsMenuScreen = modMenuObj.GetComponent<MenuScreen>();
            if (modOptionsMenuScreen == null)
            {
                SilksongModMenu.Logger.LogError("Failed to get MenuScreen component from cloned object");
                return;
            }

            // 设置CanvasGroup
            CanvasGroup canvasGroup = modOptionsMenuScreen.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            // 修改标题
            UpdateMenuTitle(modMenuObj);

            // 设置返回按钮行为
            SetupBackButton();

            // 初始隐藏
            modMenuObj.SetActive(false);

            SilksongModMenu.Logger.LogInfo("Mod Options menu created successfully");
        }

        private static void UpdateMenuTitle(GameObject menuObj)
        {
            // 查找并修改标题文本
            Text[] allTexts = menuObj.GetComponentsInChildren<Text>(true);
            foreach (Text text in allTexts)
            {
                // 根据文本内容或位置判断是否为标题
                if (text.text.ToLower().Contains("option") ||
                    text.fontSize > 20 || // 标题通常字体较大
                    IsTitleText(text))
                {
                    text.text = "MOD OPTIONS";
                    SilksongModMenu.Logger.LogInfo($"Updated menu title to: {text.text}");
                    break;
                }
            }
        }

        private static bool IsTitleText(Text textComponent)
        {
            // 通过位置判断 - 标题通常在顶部
            RectTransform rect = textComponent.GetComponent<RectTransform>();
            if (rect != null)
            {
                Vector3[] corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                float maxY = corners.Max(corner => corner.y);

                // 如果这个文本在屏幕上半部分，可能是标题
                if (maxY > Screen.height * 0.6f)
                {
                    return true;
                }
            }
            return false;
        }

        private static void SetupBackButton()
        {
            if (modOptionsMenuScreen.backButton != null)
            {
                // 清除原有事件
                modOptionsMenuScreen.backButton.OnSubmitPressed = new UnityEngine.Events.UnityEvent();

                // 设置新的返回事件 - 返回到Extras菜单
                modOptionsMenuScreen.backButton.OnSubmitPressed.AddListener(() => {
                    SilksongModMenu.Logger.LogInfo("Mod Options back button clicked");
                    uiManager.GoToExtrasMenu();
                });

                SilksongModMenu.Logger.LogInfo("Back button setup complete");
            }
            else
            {
                SilksongModMenu.Logger.LogWarning("No back button found in Mod Options menu");
            }
        }

        private static IEnumerator ShowModMenuCoroutine()
        {
            // 直接使用UIManager现有的菜单切换逻辑
            // 这会自动处理输入控制

            // 隐藏当前菜单（Extras菜单）
            yield return uiManager.HideMenu(uiManager.extrasMenuScreen, disable: false);

            // 显示Mod选项菜单
            modOptionsMenuScreen.gameObject.SetActive(true);

            // 使用UIManager的淡入系统
            CanvasGroup modCanvasGroup = modOptionsMenuScreen.GetComponent<CanvasGroup>();
            if (modCanvasGroup != null)
            {
                yield return uiManager.FadeInCanvasGroup(modCanvasGroup);
            }
            else
            {
                // 备用显示方式
                modOptionsMenuScreen.gameObject.SetActive(true);
                yield return new WaitForSeconds(0.1f);
            }

            // 设置默认高亮
            modOptionsMenuScreen.HighlightDefault();

            SilksongModMenu.Logger.LogInfo("Mod Options menu shown successfully");
        }

        public static void Hide()
        {
            if (modOptionsMenuScreen != null && modOptionsMenuScreen.gameObject.activeSelf)
            {
                uiManager.StartCoroutine(HideModMenuCoroutine());
            }
        }

        private static IEnumerator HideModMenuCoroutine()
        {
            yield return new WaitForSeconds(0.1f);

            if (modOptionsMenuScreen != null)
            {
                CanvasGroup modCanvasGroup = modOptionsMenuScreen.GetComponent<CanvasGroup>();
                if (modCanvasGroup != null)
                {
                    yield return uiManager.FadeOutCanvasGroup(modCanvasGroup);
                }
                modOptionsMenuScreen.gameObject.SetActive(false);
            }
        }
    }
}