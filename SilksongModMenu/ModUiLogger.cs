using GlobalEnums;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ModNamespace
{
    public static class ModUiLogger
    {
        private static bool _printedOnce = false;

        // 在 ConfigureMenu 后调用，只打印一次，避免刷屏
        public static void TryLogGameOptionsTreeOnce()
        {
            if (_printedOnce) return;
            _printedOnce = true;
            try
            {
                LogGameOptionsTree();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ModUI] TryLogGameOptionsTreeOnce failed: {e}");
            }
        }

        public static void LogGameOptionsTree()
        {
            var ui = UIManager.instance;
            if (ui == null)
            {
                Debug.LogWarning("[ModUI] UIManager.instance is null.");
                return;
            }
            var screen = ui.gameOptionsMenuScreen;
            if (screen == null)
            {
                Debug.LogWarning("[ModUI] gameOptionsMenuScreen is null.");
                return;
            }

            var root = screen.gameObject;
            Debug.Log($"[ModUI] GameOptionsMenuScreen root: {root.name} path={GetPath(root.transform)}");

            var cg = screen.ScreenCanvasGroup;
            Debug.Log($"[ModUI] ScreenCanvasGroup: {(cg ? cg.name : "null")}");

            var list = root.GetComponent<MenuButtonList>();
            Debug.Log($"[ModUI] MenuButtonList: {(list ? list.name : "null")}");

            PrintChildrenWithTags(root.transform, 0, useUnityLog: true);
        }

        // 手动 Dump 当前活动菜单（供你随时调用）
        public static void DumpCurrentMenuTree()
        {
            var ui = UIManager.instance;
            if (ui == null)
            {
                Debug.LogWarning("[ModUI] UIManager.instance is null.");
                return;
            }
            var menu = GetActiveMenuScreen(ui);
            if (menu == null)
            {
                Debug.LogWarning("[ModUI] No active MenuScreen found to dump.");
                return;
            }
            Debug.Log($"[ModUI] Active Menu: {menu.name} path={GetPath(menu.transform)}");
            PrintChildrenWithTags(menu.transform, 0, useUnityLog: true);
        }

        public static MenuScreen GetActiveMenuScreen(UIManager ui)
        {
            switch (ui.menuState)
            {
                case MainMenuState.OPTIONS_MENU: return ui.optionsMenuScreen;
                case MainMenuState.GAME_OPTIONS_MENU: return ui.gameOptionsMenuScreen;
                case MainMenuState.EXTRAS_MENU: return ui.extrasMenuScreen;
                case MainMenuState.EXTRAS_CONTENT_MENU: return ui.extrasContentMenuScreen;
                case MainMenuState.AUDIO_MENU: return ui.audioMenuScreen;
                case MainMenuState.VIDEO_MENU: return ui.videoMenuScreen;
                default: return null;
            }
        }

        public static void PrintChildrenWithTags(Transform t, int depth, bool useUnityLog = false)
        {
            string indent = new string(' ', depth * 2);
            var go = t.gameObject;

            var ms = go.GetComponent<MenuSetting>();
            var mh = go.GetComponent<MenuOptionHorizontal>();
            var txt = go.GetComponent<UnityEngine.UI.Text>();
            var rt = go.GetComponent<RectTransform>();

            string tags = "";
            if (ms) tags += "[MenuSetting] ";
            if (mh) tags += "[MenuOptionHorizontal] ";
            if (txt) tags += "[Text] ";
            if (rt) tags += "[Rect] ";

            string line = $"{indent}[ModUI] {go.name} {tags} path={GetPath(t)}";
            if (useUnityLog) Debug.Log(line);
            else BepInEx.Logging.Logger.CreateLogSource("ModUI").LogInfo(line);

            for (int i = 0; i < t.childCount; i++)
            {
                PrintChildrenWithTags(t.GetChild(i), depth + 1, useUnityLog);
            }
        }

        public static string GetPath(Transform t)
        {
            StringBuilder sb = new StringBuilder(t.name);
            var p = t.parent;
            while (p != null)
            {
                sb.Insert(0, p.name + "/");
                p = p.parent;
            }
            return sb.ToString();
        }
    }
}
