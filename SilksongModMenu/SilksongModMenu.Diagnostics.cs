using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace ModUINamespace
{
    public partial class SilksongModMenu
    {
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

        private static string ColorToString(Color c)
        {
            return $"RGBA({c.r:F2}, {c.g:F2}, {c.b:F2}, {c.a:F2})";
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

        /// <summary>
        /// 诊断:查找游戏中所有的 Scrollbar
        /// </summary>
        private static void DiagnoseAllScrollbars()
        {
            Logger.LogInfo("╔════════════════════════════════════════════════════════════════");
            Logger.LogInfo("║ DIAGNOSING ALL SCROLLBARS IN SCENE");
            Logger.LogInfo("╚════════════════════════════════════════════════════════════════");

            var allScrollbars = Resources.FindObjectsOfTypeAll<Scrollbar>();
            Logger.LogInfo($"Found {allScrollbars.Length} Scrollbar(s) in scene");

            foreach (var scrollbar in allScrollbars)
            {
                if (scrollbar == null) continue;

                Logger.LogInfo("╔════════════════════════════════════════════════════════════════");
                Logger.LogInfo($"║ Scrollbar: {scrollbar.name}");
                Logger.LogInfo($"║   ├─ Path: {GetFullPath(scrollbar.transform)}"); // ✅ 修改这里
                Logger.LogInfo($"║   ├─ Active: {scrollbar.gameObject.activeInHierarchy}");
                Logger.LogInfo($"║   ├─ Direction: {scrollbar.direction}");
                Logger.LogInfo($"║   ├─ Size: {scrollbar.size}");
                Logger.LogInfo($"║   ├─ Value: {scrollbar.value}");
                Logger.LogInfo($"║   └─ Handle: {scrollbar.handleRect?.name ?? "null"}");

                // 分析父对象
                var scrollRect = scrollbar.GetComponentInParent<ScrollRect>();
                if (scrollRect != null)
                {
                    Logger.LogInfo($"║   └─ Parent ScrollRect: {scrollRect.name}");
                }

                // 分析子对象
                var children = scrollbar.GetComponentsInChildren<Transform>(true);
                Logger.LogInfo($"║   └─ Children: {children.Length}");
                foreach (var child in children)
                {
                    if (child == scrollbar.transform) continue;
                    Logger.LogInfo($"║       ├─ {child.name}");
                }

                Logger.LogInfo("╚════════════════════════════════════════════════════════════════");
            }

            // 也查找所有 ScrollRect
            var allScrollRects = Resources.FindObjectsOfTypeAll<ScrollRect>();
            Logger.LogInfo($"\nFound {allScrollRects.Length} ScrollRect(s) in scene");

            foreach (var scrollRect in allScrollRects)
            {
                if (scrollRect == null) continue;

                Logger.LogInfo("╔════════════════════════════════════════════════════════════════");
                Logger.LogInfo($"║ ScrollRect: {scrollRect.name}");
                Logger.LogInfo($"║   ├─ Path: {GetFullPath(scrollRect.transform)}"); // ✅ 修改这里
                Logger.LogInfo($"║   ├─ Active: {scrollRect.gameObject.activeInHierarchy}");
                Logger.LogInfo($"║   ├─ Horizontal: {scrollRect.horizontal}");
                Logger.LogInfo($"║   ├─ Vertical: {scrollRect.vertical}");
                Logger.LogInfo($"║   ├─ Content: {scrollRect.content?.name ?? "null"}");
                Logger.LogInfo($"║   ├─ Viewport: {scrollRect.viewport?.name ?? "null"}");
                Logger.LogInfo($"║   ├─ Horizontal Scrollbar: {scrollRect.horizontalScrollbar?.name ?? "null"}");
                Logger.LogInfo($"║   └─ Vertical Scrollbar: {scrollRect.verticalScrollbar?.name ?? "null"}");
                Logger.LogInfo("╚════════════════════════════════════════════════════════════════");
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
