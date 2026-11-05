using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LunaTools
{
    /// <summary>
    /// Reflection bridge into Luna's precompiled EditorScripts.dll (obfuscated names) so the Luna
    /// Assets page can re-read / force-save luna.json. Guarded by a DLL SHA-256; on any mismatch it
    /// falls back to disk-only work + script reload. Editor only.
    /// </summary>
    public static class LunaConfigBridge
    {
        const string Tag = "[LunaSync]";
        const string AssemblyName = "EditorScripts";
        const string ExpectedSha256 = "a8d1a6f5c87dbe81b53bcc310cc13fbf4cb31b62c2e7c7b05a34136f8b38421f";

        // Obfuscated member names (Luna 7.2.0).
        const string ConfigTypeName = " ‭";    // static config holder
        const string CachedTextField = "‐⁚";   // private static string (luna.json text)
        const string DataField = "‐⁙";         // private static LunaData
        const string ParseMethod = "⁕";             // bool ()  parse from cached text
        const string SaveMethod = "⁘";              // void ()  serialize + write if changed
        const string AssetsMethod = "‭";            // UnityAssets ()  export/excludes/includes
        const string TrackerTypeName = " ⁊";   // change tracker
        const string TrackerMethod = " ";           // void (bool) assetsModified
        const string WindowTypeName = "LunaExportWindow";
        const string WindowRebuildMethod = " ";     // public static void ()

        static bool _resolved;
        static string _bindError;
        static FieldInfo _cacheField, _dataField;
        static MethodInfo _parse, _save, _assets, _tracker, _rebuild;
        static Type _windowType;

        [Serializable] class CfgRoot { public CfgUnity unity; }
        [Serializable] class CfgUnity { public CfgAssets assets; }
        [Serializable] class CfgAssets { public List<string> excludes; public List<string> includes; }

        public static bool IsBound { get { Resolve(); return _bindError == null; } }
        public static string BindError { get { Resolve(); return _bindError; } }
        public static Type LunaWindowType { get { Resolve(); return _windowType; } }

        public static string LunaJsonPath
        {
            get { return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "luna.json"); }
        }

        static string ProjectRoot { get { return Directory.GetParent(Application.dataPath).FullName; } }
        static string BackupPath { get { return Path.Combine(ProjectRoot, "Library/LunaAssetsSync/luna.json.bak"); } }

        // ---- resolve -------------------------------------------------------------------------

        static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            try
            {
                Assembly asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == AssemblyName);
                if (asm == null) { _bindError = "assembly '" + AssemblyName + "' not loaded"; return; }

                string loc = asm.Location;
                if (string.IsNullOrEmpty(loc) || !File.Exists(loc)) { _bindError = "assembly file not found: " + loc; return; }
                string hash = Sha256(loc);
                if (!string.Equals(hash, ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    _bindError = "DLL hash mismatch (" + hash + ")";
                    return;
                }

                const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
                Type[] types = SafeTypes(asm);

                // Several types can share a name across namespaces: pick the one that has the members.
                Type cfg = types.FirstOrDefault(t => t.Name == ConfigTypeName && t.GetField(CachedTextField, All) != null && t.GetMethod(SaveMethod, All) != null);
                if (cfg == null) { _bindError = "config type not found"; return; }
                _cacheField = cfg.GetField(CachedTextField, All);
                _dataField = cfg.GetField(DataField, All);
                _parse = cfg.GetMethod(ParseMethod, All, null, Type.EmptyTypes, null);
                _save = cfg.GetMethod(SaveMethod, All, null, Type.EmptyTypes, null);
                _assets = cfg.GetMethod(AssetsMethod, All, null, Type.EmptyTypes, null);
                if (_cacheField == null || _cacheField.FieldType != typeof(string) || !_cacheField.IsStatic) { _bindError = "cached text field missing"; return; }
                if (_dataField == null || !_dataField.IsStatic) { _bindError = "LunaData field missing"; return; }
                if (_parse == null || !_parse.IsStatic) { _bindError = "parse method missing"; return; }
                if (_save == null || !_save.IsStatic) { _bindError = "save method missing"; return; }
                if (_assets == null || !_assets.IsStatic) { _bindError = "assets accessor missing"; return; }

                Type trk = types.FirstOrDefault(t => t.Name == TrackerTypeName && t.GetMethod(TrackerMethod, All, null, new[] { typeof(bool) }, null) != null);
                if (trk == null) { _bindError = "tracker type/method not found"; return; }
                _tracker = trk.GetMethod(TrackerMethod, All, null, new[] { typeof(bool) }, null);

                _windowType = types.FirstOrDefault(t => t.Name == WindowTypeName && typeof(EditorWindow).IsAssignableFrom(t));
                if (_windowType == null) { _bindError = "window type not found"; return; }
                _rebuild = _windowType.GetMethod(WindowRebuildMethod, All, null, Type.EmptyTypes, null);
                if (_rebuild == null || !_rebuild.IsStatic) { _bindError = "window rebuild method missing"; return; }
            }
            catch (Exception e)
            {
                _bindError = "resolve failed: " + e.Message;
                Debug.LogException(e);
            }
        }

        static Type[] SafeTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null).ToArray(); }
        }

        static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
        }

        // ---- public API ----------------------------------------------------------------------

        public static bool Refresh()
        {
            Resolve();
            try
            {
                if (_bindError != null)
                {
                    Debug.LogWarning(Tag + " not bound (" + _bindError + "), reloading scripts instead.");
                    EditorUtility.RequestScriptReload();
                    return false;
                }
                _cacheField.SetValue(null, null);
                _parse.Invoke(null, null);
                _rebuild.Invoke(null, null);
                RepaintLunaWindow();
                Debug.Log(Tag + " Refreshed Luna config from luna.json.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(Tag + " Refresh failed: " + Unwrap(e));
                return false;
            }
        }

        public static bool Apply()
        {
            Resolve();
            try
            {
                Backup();
                if (_bindError != null)
                {
                    Debug.LogWarning(Tag + " not bound (" + _bindError + "), deleting LunaTemp/luna-cache.json and reloading scripts.");
                    string cache = Path.Combine(ProjectRoot, "LunaTemp/luna-cache.json");
                    if (File.Exists(cache)) File.Delete(cache);
                    EditorUtility.RequestScriptReload();
                    return false;
                }

                object assets = _assets.Invoke(null, null);
                List<string> excludes = ListField(assets, "excludes");
                List<string> includes = ListField(assets, "includes");
                Dedupe(excludes);
                Dedupe(includes);

                _cacheField.SetValue(null, null);
                _save.Invoke(null, null);
                _tracker.Invoke(null, new object[] { true });

                CfgAssets disk = ReadDiskAssets();
                bool ok = disk != null
                    && SameSet(disk.excludes, excludes)
                    && SameSet(disk.includes, includes);
                if (ok) Debug.Log(Tag + " Applied. luna.json matches memory (excludes " + excludes.Count + ", includes " + includes.Count + ").");
                else Debug.LogError(Tag + " Applied but luna.json does NOT match memory (excludes/includes differ).");
                RepaintLunaWindow();
                return ok;
            }
            catch (Exception e)
            {
                Debug.LogError(Tag + " Apply failed: " + Unwrap(e));
                return false;
            }
        }

        public static bool IsDiskOutOfSync()
        {
            Resolve();
            if (_bindError != null) return false;
            try
            {
                string path = LunaJsonPath;
                if (!File.Exists(path)) return false;
                string disk = Norm(File.ReadAllText(path));
                string cached = _cacheField.GetValue(null) as string;
                if (string.IsNullOrEmpty(cached)) return false; // not loaded yet, nothing stale
                return disk != Norm(cached);
            }
            catch { return false; }
        }

        public static string Diagnose()
        {
            Resolve();
            var sb = new StringBuilder();
            sb.AppendLine(Tag + " Luna bridge diagnose");
            sb.AppendLine("  bound: " + (_bindError == null));
            if (_bindError != null) sb.AppendLine("  error: " + _bindError);
            sb.AppendLine("  expected sha256: " + ExpectedSha256);
            Assembly asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == AssemblyName);
            if (asm != null && !string.IsNullOrEmpty(asm.Location) && File.Exists(asm.Location))
                sb.AppendLine("  actual sha256:   " + Sha256(asm.Location));
            sb.AppendLine("  cached text field: " + (_cacheField != null));
            sb.AppendLine("  LunaData field:    " + (_dataField != null));
            sb.AppendLine("  parse:             " + (_parse != null));
            sb.AppendLine("  save:              " + (_save != null));
            sb.AppendLine("  assets accessor:   " + (_assets != null));
            sb.AppendLine("  tracker(bool):     " + (_tracker != null));
            sb.AppendLine("  window type:       " + (_windowType != null ? _windowType.FullName : "null"));
            sb.AppendLine("  window rebuild:    " + (_rebuild != null));
            sb.AppendLine("  luna.json: " + LunaJsonPath + " (exists " + File.Exists(LunaJsonPath) + ")");
            if (_bindError == null) sb.AppendLine("  disk out of sync: " + IsDiskOutOfSync());
            return sb.ToString();
        }

        public static void RepaintLunaWindow()
        {
            Resolve();
            if (_windowType == null) return;
            foreach (UnityEngine.Object o in Resources.FindObjectsOfTypeAll(_windowType))
                if (o is EditorWindow w) w.Repaint();
        }

        // ---- helpers -------------------------------------------------------------------------

        static void Backup()
        {
            string src = LunaJsonPath;
            if (!File.Exists(src)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(BackupPath));
            File.Copy(src, BackupPath, true);
        }

        static List<string> ListField(object obj, string name)
        {
            FieldInfo f = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null) throw new MissingFieldException(obj.GetType().Name, name);
            var list = f.GetValue(obj) as List<string>;
            if (list == null) { list = new List<string>(); f.SetValue(obj, list); }
            return list;
        }

        static void Dedupe(List<string> list)
        {
            var seen = new HashSet<string>();
            var keep = list.Where(s => seen.Add(s)).ToList();
            if (keep.Count == list.Count) return;
            list.Clear();
            list.AddRange(keep);
        }

        static CfgAssets ReadDiskAssets()
        {
            var root = JsonUtility.FromJson<CfgRoot>(File.ReadAllText(LunaJsonPath));
            return root != null && root.unity != null ? root.unity.assets : null;
        }

        static bool SameSet(List<string> a, List<string> b)
        {
            return new HashSet<string>(a ?? new List<string>()).SetEquals(b ?? new List<string>());
        }

        static string Norm(string s) { return s.Replace("\r\n", "\n"); }

        static string Unwrap(Exception e)
        {
            return e is TargetInvocationException && e.InnerException != null ? e.InnerException.ToString() : e.ToString();
        }
    }
}
