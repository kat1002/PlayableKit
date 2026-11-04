using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Editor-only: removes every UGUI component that uses DrivenRectTransformTracker (its Clear call logs a
/// warning in Luna) from the ACTIVE scene, baking what they drove into plain RectTransform values:
///  - ContentSizeFitter / LayoutGroup (H/V/Grid): destroyed, rects of the group and its direct children baked
///  - AspectRatioFitter: replaced by PAAspectFitter
///  - ScrollRect: replaced by PADragScroll (PAStorePanel.shopScroll is re-pointed)
/// Works on inactive objects too (inactive ancestors are temporarily activated and restored afterwards).
/// Undo-able (one group). Marks the scene dirty but never saves. Safe to run twice.
/// </summary>
public static class PALunaUiSanitizer
{
    class Ctx
    {
        public StringBuilder log = new StringBuilder();
        public int layoutsRemoved, fittersRemoved, rectsBaked, aspectConverted, scrollsConverted, scrollbarsRemoved,
            panelsFixed, graphicsAdded, warnings, errors, changes;

        public void Line(string s) { log.AppendLine(s); }
        public void Warn(string s) { warnings++; log.AppendLine("WARN: " + s); }
        public void Error(string s) { errors++; log.AppendLine("ERROR: " + s); }
    }

    // Rect snapshot stored as anchors + offsets: offsets do not depend on the pivot, so it round-trips exactly
    class RectData
    {
        public Vector2 aMin, aMax, offMin, offMax, pivot;

        public static RectData Read(RectTransform rt)
        {
            RectData d = new RectData();
            d.aMin = rt.anchorMin;
            d.aMax = rt.anchorMax;
            d.offMin = rt.offsetMin;
            d.offMax = rt.offsetMax;
            d.pivot = rt.pivot;
            return d;
        }

        public RectData Clone()
        {
            return (RectData)MemberwiseClone();
        }

        public bool Same(RectTransform rt)
        {
            return Near(aMin, rt.anchorMin) && Near(aMax, rt.anchorMax) && Near(offMin, rt.offsetMin)
                   && Near(offMax, rt.offsetMax) && Near(pivot, rt.pivot);
        }

        public void Apply(RectTransform rt)
        {
            rt.pivot = pivot;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
        }

        static bool Near(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) < 0.0001f && Mathf.Abs(a.y - b.y) < 0.0001f;
        }
    }

    // ------------------------------------------------------------------ menu

    [MenuItem("Tools/PA/Sanitize UI For Luna")]
    public static void Sanitize()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("PALunaUiSanitizer: stop play mode first.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("PALunaUiSanitizer: no valid active scene.");
            return;
        }

        Ctx cx = new Ctx();
        cx.Line("=== PALunaUiSanitizer: scene '" + scene.name + "' ===");

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Sanitize UI For Luna");
        int undoGroup = Undo.GetCurrentGroup();

        List<GameObject> activated = new List<GameObject>();
        try
        {
            List<ScrollRect> scrolls = FindInScene<ScrollRect>(scene);
            List<LayoutGroup> groups = FindInScene<LayoutGroup>(scene);
            List<ContentSizeFitter> csfs = FindInScene<ContentSizeFitter>(scene);
            List<AspectRatioFitter> arfs = FindInScene<AspectRatioFitter>(scene);

            cx.Line("found: ScrollRect=" + scrolls.Count + " LayoutGroup=" + groups.Count
                    + " ContentSizeFitter=" + csfs.Count + " AspectRatioFitter=" + arfs.Count);

            List<Component> all = new List<Component>();
            all.AddRange(scrolls.ConvertAll<Component>(c => c));
            all.AddRange(groups.ConvertAll<Component>(c => c));
            all.AddRange(csfs.ConvertAll<Component>(c => c));
            all.AddRange(arfs.ConvertAll<Component>(c => c));

            if (all.Count > 0)
            {
                ActivateAncestors(cx, activated, all);
                RebuildAll(cx, all);

                BakeLayouts(cx, groups, csfs);
                ConvertAspectFitters(cx, arfs);
                ConvertScrollRects(cx, scene, scrolls);
            }

            FixStorePanels(cx, scene);
            Verify(cx, scene);
        }
        catch (Exception e)
        {
            cx.Error("exception: " + e);
            Debug.LogException(e);
        }
        finally
        {
            RestoreActive(cx, activated);
            Undo.CollapseUndoOperations(undoGroup);
            if (cx.changes > 0) EditorSceneManager.MarkSceneDirty(scene);
        }

        cx.Line("");
        cx.Line("layout groups removed=" + cx.layoutsRemoved + ", size fitters removed=" + cx.fittersRemoved
                + ", rects baked=" + cx.rectsBaked + ", aspect fitters converted=" + cx.aspectConverted
                + ", scroll rects converted=" + cx.scrollsConverted + ", scrollbars removed=" + cx.scrollbarsRemoved
                + ", raycast graphics added=" + cx.graphicsAdded + ", store panels re-pointed=" + cx.panelsFixed
                + ", warnings=" + cx.warnings + ", errors=" + cx.errors);
        cx.Line(cx.changes > 0 ? "Scene marked dirty - review it, then save (Ctrl+S)." : "No changes made.");

        string text = cx.log.ToString();
        if (cx.errors > 0) Debug.LogError(text);
        else if (cx.warnings > 0) Debug.LogWarning(text);
        else Debug.Log(text);
    }

    // ------------------------------------------------------------------ find / activate / rebuild

    static List<T> FindInScene<T>(Scene scene) where T : Component
    {
        List<T> list = new List<T>();
        T[] found = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < found.Length; i++)
        {
            T c = found[i];
            if (c == null) continue;
            if (EditorUtility.IsPersistent(c)) continue;
            GameObject go = c.gameObject;
            if (!go.scene.IsValid() || go.scene != scene) continue;
            if ((go.hideFlags & HideFlags.HideInHierarchy) != 0) continue;
            list.Add(c);
        }

        // Deterministic order (parents before children when paths nest)
        list.Sort((a, b) => string.CompareOrdinal(PathOf(a.transform), PathOf(b.transform)));
        return list;
    }

    static void ActivateAncestors(Ctx cx, List<GameObject> activated, List<Component> comps)
    {
        for (int i = 0; i < comps.Count; i++)
        {
            if (comps[i] == null) continue;
            for (Transform t = comps[i].transform; t != null; t = t.parent)
            {
                if (t.gameObject.activeSelf) continue;
                t.gameObject.SetActive(true);
                activated.Add(t.gameObject);
                cx.Line("temporarily activated: " + PathOf(t));
            }
        }
    }

    static void RestoreActive(Ctx cx, List<GameObject> activated)
    {
        for (int i = activated.Count - 1; i >= 0; i--)
        {
            GameObject go = activated[i];
            if (go == null) continue;
            try
            {
                go.SetActive(false);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
        if (activated.Count > 0) cx.Line("restored active state of " + activated.Count + " object(s)");
        activated.Clear();
    }

    static void RebuildAll(Ctx cx, List<Component> comps)
    {
        HashSet<RectTransform> tops = new HashSet<RectTransform>();
        for (int i = 0; i < comps.Count; i++)
        {
            if (comps[i] == null) continue;
            RectTransform top = comps[i].transform as RectTransform;
            if (top == null) continue;
            while (top.parent is RectTransform) top = (RectTransform)top.parent;
            tops.Add(top);
        }

        Canvas.ForceUpdateCanvases();
        foreach (RectTransform top in tops)
        {
            try
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(top);
                cx.Line("rebuilt layout under: " + PathOf(top));
            }
            catch (Exception e)
            {
                cx.Warn("layout rebuild failed under " + PathOf(top) + ": " + e.Message);
            }
        }
        Canvas.ForceUpdateCanvases();
    }

    // ------------------------------------------------------------------ (b) layouts

    static void BakeLayouts(Ctx cx, List<LayoutGroup> groups, List<ContentSizeFitter> csfs)
    {
        Dictionary<RectTransform, RectData> rects = new Dictionary<RectTransform, RectData>();
        List<RectTransform> order = new List<RectTransform>();

        // Phase 1: record (layouts are freshly rebuilt)
        for (int i = 0; i < groups.Count; i++)
        {
            LayoutGroup g = groups[i];
            if (g == null) continue;
            if (IsLocked(g)) continue;
            if (!g.enabled)
            {
                cx.Line("disabled group, rect baking skipped: " + PathOf(g.transform));
                continue;
            }

            RectTransform rt = g.transform as RectTransform;
            if (rt == null) continue;
            Record(rects, order, rt);

            HorizontalOrVerticalLayoutGroup hv = g as HorizontalOrVerticalLayoutGroup;
            bool vertical = g is VerticalLayoutGroup;
            bool horizontal = g is HorizontalLayoutGroup;

            for (int c = 0; c < rt.childCount; c++)
            {
                RectTransform child = rt.GetChild(c) as RectTransform;
                if (child == null) continue;
                Record(rects, order, child);

                if (hv == null) continue;
                RectData stretched = null;

                if (vertical && (hv.childControlWidth || hv.childForceExpandWidth))
                {
                    float inner = rt.rect.width - g.padding.left - g.padding.right;
                    if (Mathf.Abs(child.rect.width - inner) < 0.5f)
                    {
                        stretched = RectData.Read(child);
                        stretched.aMin.x = 0f;
                        stretched.aMax.x = 1f;
                        stretched.offMin.x = g.padding.left;
                        stretched.offMax.x = -g.padding.right;
                    }
                }
                else if (horizontal && (hv.childControlHeight || hv.childForceExpandHeight))
                {
                    float inner = rt.rect.height - g.padding.top - g.padding.bottom;
                    if (Mathf.Abs(child.rect.height - inner) < 0.5f)
                    {
                        stretched = RectData.Read(child);
                        stretched.aMin.y = 0f;
                        stretched.aMax.y = 1f;
                        stretched.offMin.y = g.padding.bottom;
                        stretched.offMax.y = -g.padding.top;
                    }
                }

                if (stretched != null)
                {
                    rects[child] = stretched;
                    cx.Line("  child keeps stretch across the controlled axis: " + PathOf(child));
                }
            }
        }

        for (int i = 0; i < csfs.Count; i++)
        {
            ContentSizeFitter f = csfs[i];
            if (f == null || IsLocked(f)) continue;
            if (!f.enabled)
            {
                cx.Line("disabled size fitter, rect baking skipped: " + PathOf(f.transform));
                continue;
            }
            RectTransform rt = f.transform as RectTransform;
            if (rt != null) Record(rects, order, rt);
        }

        // Phase 2: destroy size fitters first, then layout groups
        for (int i = 0; i < csfs.Count; i++)
        {
            ContentSizeFitter f = csfs[i];
            if (f == null) continue;
            if (IsLocked(f))
            {
                cx.Error("ContentSizeFitter belongs to a prefab instance, not removed: " + PathOf(f.transform));
                continue;
            }
            string p = PathOf(f.transform);
            Undo.DestroyObjectImmediate(f);
            cx.fittersRemoved++;
            cx.changes++;
            cx.Line("removed ContentSizeFitter: " + p);
        }

        for (int i = 0; i < groups.Count; i++)
        {
            LayoutGroup g = groups[i];
            if (g == null) continue;
            if (IsLocked(g))
            {
                cx.Error("LayoutGroup belongs to a prefab instance, not removed: " + PathOf(g.transform));
                continue;
            }
            string p = PathOf(g.transform) + " (" + g.GetType().Name + ")";
            Undo.DestroyObjectImmediate(g);
            cx.layoutsRemoved++;
            cx.changes++;
            cx.Line("removed LayoutGroup: " + p);
        }

        // Phase 3: re-apply (destroying the drivers may have reverted driven values)
        for (int i = 0; i < order.Count; i++)
        {
            RectTransform rt = order[i];
            if (rt == null) continue;
            RectData d = rects[rt];
            bool same = d.Same(rt);
            Undo.RecordObject(rt, "Bake layout");
            d.Apply(rt);
            EditorUtility.SetDirty(rt);
            cx.rectsBaked++;
            cx.Line("baked rect" + (same ? "" : " (restored)") + ": " + PathOf(rt)
                    + " anchors " + rt.anchorMin + "-" + rt.anchorMax + " pos " + rt.anchoredPosition
                    + " size " + rt.sizeDelta);
        }
    }

    static void Record(Dictionary<RectTransform, RectData> rects, List<RectTransform> order, RectTransform rt)
    {
        if (rects.ContainsKey(rt)) return;
        rects[rt] = RectData.Read(rt);
        order.Add(rt);
    }

    // ------------------------------------------------------------------ (c) aspect fitters

    static void ConvertAspectFitters(Ctx cx, List<AspectRatioFitter> arfs)
    {
        // Parents first: a child fitter reads its parent's final rect
        List<AspectRatioFitter> list = new List<AspectRatioFitter>(arfs);
        list.RemoveAll(a => a == null);
        list.Sort((a, b) => Depth(a.transform).CompareTo(Depth(b.transform)));

        for (int i = 0; i < list.Count; i++)
        {
            AspectRatioFitter arf = list[i];
            if (arf == null) continue;
            string path = PathOf(arf.transform);
            if (IsLocked(arf))
            {
                cx.Error("AspectRatioFitter belongs to a prefab instance, not replaced: " + path);
                continue;
            }

            GameObject go = arf.gameObject;
            RectTransform rt = go.transform as RectTransform;
            int unityMode = (int)arf.aspectMode;
            float ratio = arf.aspectRatio;
            bool enabled = arf.enabled;

            Undo.DestroyObjectImmediate(arf);
            cx.fittersRemoved++;

            PAAspectFitter fit = go.GetComponent<PAAspectFitter>();
            if (fit == null) fit = Undo.AddComponent<PAAspectFitter>(go);

            Undo.RecordObject(fit, "Convert AspectRatioFitter");
            fit.m_Mode = PAAspectFitter.FromUnity(unityMode);
            fit.m_Ratio = ratio;
            fit.enabled = enabled;
            EditorUtility.SetDirty(fit);

            if (rt != null)
            {
                Undo.RecordObject(rt, "Apply PAAspectFitter");
                fit.Apply(true);
                EditorUtility.SetDirty(rt);
            }

            cx.aspectConverted++;
            cx.changes++;
            cx.Line("AspectRatioFitter -> PAAspectFitter: " + path + " mode " + fit.m_Mode + " ratio " + ratio
                    + (enabled ? "" : " (disabled)"));
        }
    }

    // ------------------------------------------------------------------ (d) scroll rects

    static void ConvertScrollRects(Ctx cx, Scene scene, List<ScrollRect> scrolls)
    {
        List<ScrollRect> list = new List<ScrollRect>(scrolls);
        list.RemoveAll(s => s == null);
        if (list.Count == 0) return;

        HashSet<ScrollRect> doomed = new HashSet<ScrollRect>(list);
        LogScrollReferences(cx, scene, doomed);

        for (int i = 0; i < list.Count; i++)
        {
            ScrollRect scroll = list[i];
            if (scroll == null) continue;
            try
            {
                ConvertScroll(cx, scroll);
            }
            catch (Exception e)
            {
                cx.Error("failed to convert ScrollRect " + PathOf(scroll.transform) + ": " + e);
            }
        }
    }

    static void ConvertScroll(Ctx cx, ScrollRect scroll)
    {
        GameObject go = scroll.gameObject;
        string path = PathOf(go.transform);

        if (IsLocked(scroll))
        {
            cx.Error("ScrollRect belongs to a prefab instance, not replaced: " + path);
            return;
        }

        RectTransform content = scroll.content;
        if (content == null)
        {
            cx.Error("ScrollRect has no content, skipped: " + path);
            return;
        }

        RectTransform viewport = scroll.viewport;
        if (viewport == null)
        {
            Transform v = go.transform.Find("Viewport");
            if (v != null) viewport = v as RectTransform;
        }
        if (viewport == null)
        {
            viewport = go.transform as RectTransform;
            cx.Warn("ScrollRect has no viewport, using its own rect: " + path);
        }

        float deceleration = scroll.decelerationRate;
        bool inertia = scroll.inertia;
        Scrollbar hBar = scroll.horizontalScrollbar;
        Scrollbar vBar = scroll.verticalScrollbar;

        // Size source: "shelfs" child, else the first child
        RectTransform sizeSource = null;
        Transform named = content.Find("shelfs");
        if (named != null) sizeSource = named as RectTransform;
        if (sizeSource == null)
        {
            for (int i = 0; i < content.childCount; i++)
            {
                RectTransform c = content.GetChild(i) as RectTransform;
                if (c != null)
                {
                    sizeSource = c;
                    break;
                }
            }
        }
        if (sizeSource == null) cx.Warn("no size source (shelfs / first child) under content: " + path);

        // Raycast target so empty areas still start a drag (a ScrollRect object normally has an Image)
        Graphic graphic = go.GetComponent<Graphic>();
        if (graphic == null)
        {
            Image img = Undo.AddComponent<Image>(go);
            img.color = new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = true;
            EditorUtility.SetDirty(img);
            cx.graphicsAdded++;
            cx.Line("added transparent raycast Image: " + path);
        }
        else if (!graphic.raycastTarget)
        {
            cx.Warn("Graphic on scroll object has Raycast Target off, drags on empty areas will not scroll: " + path);
        }

        // --- geometry: size source left-anchored with pivot x 0, content (0,0)-(0,1) pivot (0,0.5) at x 0
        float contentWidth = content.rect.width;
        if (sizeSource != null)
        {
            if (sizeSource.parent != content)
            {
                cx.Warn("size source is not a direct child of content, geometry not converted: "
                        + PathOf(sizeSource));
            }
            else
            {
                Vector3[] corners = new Vector3[4];
                sizeSource.GetWorldCorners(corners);
                Vector3 bl = content.InverseTransformPoint(corners[0]);
                Vector3 tr = content.InverseTransformPoint(corners[2]);
                float cl = content.rect.xMin;
                float sLeft = bl.x - cl;
                float sRight = tr.x - cl;

                bool needsConvert = !(Mathf.Approximately(sizeSource.anchorMin.x, 0f)
                                      && Mathf.Approximately(sizeSource.anchorMax.x, 0f)
                                      && Mathf.Approximately(sizeSource.pivot.x, 0f));
                if (needsConvert)
                {
                    Undo.RecordObject(sizeSource, "Convert shelf anchors");
                    Vector2 sd = sizeSource.sizeDelta;
                    Vector2 ap = sizeSource.anchoredPosition;
                    sizeSource.pivot = new Vector2(0f, sizeSource.pivot.y);
                    sizeSource.anchorMin = new Vector2(0f, sizeSource.anchorMin.y);
                    sizeSource.anchorMax = new Vector2(0f, sizeSource.anchorMax.y);
                    sizeSource.sizeDelta = new Vector2(sRight - sLeft, sd.y);
                    sizeSource.anchoredPosition = new Vector2(sLeft, ap.y);
                    EditorUtility.SetDirty(sizeSource);
                    cx.Line("adjusted size source to left anchor/pivot x 0 (x " + sLeft + ", width "
                            + (sRight - sLeft) + "): " + PathOf(sizeSource));
                }

                contentWidth = sizeSource.anchoredPosition.x + sizeSource.rect.width;
            }
        }

        RectTransform parent = content.parent as RectTransform;
        Undo.RecordObject(content, "Convert scroll content");
        if (parent != null)
        {
            Rect pr = parent.rect;
            float bottom = pr.yMin + content.anchorMin.y * pr.height + content.offsetMin.y;
            float top = pr.yMin + content.anchorMax.y * pr.height + content.offsetMax.y;

            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.offsetMin = new Vector2(0f, bottom - pr.yMin);
            content.offsetMax = new Vector2(contentWidth, top - pr.yMax);
        }
        else
        {
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.anchoredPosition = new Vector2(0f, content.anchoredPosition.y);
            content.sizeDelta = new Vector2(contentWidth, content.sizeDelta.y);
        }
        EditorUtility.SetDirty(content);
        cx.Line("content set to anchors (0,0)-(0,1), pivot (0,0.5), x 0, width " + contentWidth + ": "
                + PathOf(content));

        // Other children whose width followed the old stretched content
        for (int i = 0; i < content.childCount; i++)
        {
            RectTransform c = content.GetChild(i) as RectTransform;
            if (c == null || c == sizeSource) continue;
            if (!Mathf.Approximately(c.anchorMin.x, c.anchorMax.x))
                cx.Warn("content child has x-stretch anchors, its width now follows the new content width: "
                        + PathOf(c));
        }

        // --- PADragScroll
        PADragScroll drag = go.GetComponent<PADragScroll>();
        if (drag == null) drag = Undo.AddComponent<PADragScroll>(go);
        Undo.RecordObject(drag, "Convert ScrollRect");
        drag.m_Viewport = viewport;
        drag.m_Content = content;
        drag.m_SizeSource = sizeSource;
        drag.m_Deceleration = deceleration;
        drag.m_Inertia = inertia;
        EditorUtility.SetDirty(drag);

        // --- destroy the ScrollRect (Mask / RectMask2D on the viewport stay untouched)
        Undo.DestroyObjectImmediate(scroll);
        cx.scrollsConverted++;
        cx.changes++;
        cx.Line("ScrollRect -> PADragScroll: " + path + " (decel " + deceleration + ", inertia " + inertia + ")");

        if (hBar != null) DestroyScrollbar(cx, hBar);
        if (vBar != null && vBar != hBar) DestroyScrollbar(cx, vBar);
    }

    static void DestroyScrollbar(Ctx cx, Scrollbar bar)
    {
        if (bar == null) return;
        string p = PathOf(bar.transform);
        if (IsLocked(bar))
        {
            cx.Warn("Scrollbar belongs to a prefab instance, left in place: " + p);
            return;
        }
        Undo.DestroyObjectImmediate(bar);
        cx.scrollbarsRemoved++;
        cx.changes++;
        cx.Line("removed orphaned Scrollbar component: " + p);
    }

    // Serialized references to a ScrollRect cannot be re-pointed to a PADragScroll generically, so report them
    static void LogScrollReferences(Ctx cx, Scene scene, HashSet<ScrollRect> doomed)
    {
        List<MonoBehaviour> behaviours = FindInScene<MonoBehaviour>(scene);
        for (int i = 0; i < behaviours.Count; i++)
        {
            MonoBehaviour mb = behaviours[i];
            if (mb == null || mb is ScrollRect) continue;
            try
            {
                SerializedObject so = new SerializedObject(mb);
                SerializedProperty it = so.GetIterator();
                bool enter = true;
                while (it.NextVisible(enter))
                {
                    enter = true;
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    ScrollRect target = it.objectReferenceValue as ScrollRect;
                    if (target != null && doomed.Contains(target))
                    {
                        cx.Warn(mb.GetType().Name + "." + it.propertyPath + " on " + PathOf(mb.transform)
                                + " references ScrollRect " + PathOf(target.transform)
                                + "; re-assign it manually if the field type is still ScrollRect");
                    }
                }
            }
            catch (Exception e)
            {
                cx.Warn("could not inspect " + mb.GetType().Name + " on " + PathOf(mb.transform) + ": " + e.Message);
            }
        }
    }

    // PAStorePanel.shopScroll is now a PADragScroll: the old ScrollRect reference deserializes as null
    static void FixStorePanels(Ctx cx, Scene scene)
    {
        List<PAStorePanel> panels = FindInScene<PAStorePanel>(scene);
        for (int i = 0; i < panels.Count; i++)
        {
            PAStorePanel panel = panels[i];
            if (panel == null) continue;
            if (panel.shopScroll != null) continue;

            PADragScroll pick = null;
            for (Transform t = panel.transform; t != null && pick == null; t = t.parent)
            {
                PADragScroll[] found = t.GetComponentsInChildren<PADragScroll>(true);
                if (found.Length == 0) continue;
                pick = found[0];
                if (found.Length > 1)
                    cx.Warn("several PADragScroll under " + PathOf(t) + ", picked " + PathOf(pick.transform)
                            + " for " + PathOf(panel.transform));
            }

            if (pick == null)
            {
                cx.Error("PAStorePanel.shopScroll is empty and no PADragScroll was found near: "
                         + PathOf(panel.transform));
                continue;
            }

            Undo.RecordObject(panel, "Re-point shopScroll");
            panel.shopScroll = pick;
            EditorUtility.SetDirty(panel);
            cx.panelsFixed++;
            cx.changes++;
            cx.Line("PAStorePanel.shopScroll -> " + PathOf(pick.transform) + " on " + PathOf(panel.transform));
        }
    }

    // ------------------------------------------------------------------ (e) verify

    static void Verify(Ctx cx, Scene scene)
    {
        List<string> remaining = new List<string>();
        Collect<ScrollRect>(scene, remaining);
        Collect<LayoutGroup>(scene, remaining);
        Collect<ContentSizeFitter>(scene, remaining);
        Collect<AspectRatioFitter>(scene, remaining);

        if (remaining.Count == 0)
        {
            cx.Line("OK: no ScrollRect / LayoutGroup / ContentSizeFitter / AspectRatioFitter left in the scene.");
            return;
        }

        StringBuilder sb = new StringBuilder("PALunaUiSanitizer: components still present:");
        for (int i = 0; i < remaining.Count; i++) sb.Append("\n  ").Append(remaining[i]);
        cx.Error(sb.ToString());
    }

    static void Collect<T>(Scene scene, List<string> into) where T : Component
    {
        List<T> list = FindInScene<T>(scene);
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null) into.Add(typeof(T).Name + " (" + list[i].GetType().Name + "): " + PathOf(list[i].transform));
        }
    }

    // ------------------------------------------------------------------ helpers

    static bool IsLocked(Component c)
    {
        return PrefabUtility.IsPartOfPrefabInstance(c) && PrefabUtility.GetCorrespondingObjectFromSource(c) != null;
    }

    static int Depth(Transform t)
    {
        int d = 0;
        while (t.parent != null)
        {
            d++;
            t = t.parent;
        }
        return d;
    }

    static string PathOf(Transform t)
    {
        if (t == null) return "<null>";
        StringBuilder sb = new StringBuilder(t.name);
        for (Transform p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
        return sb.ToString();
    }
}
