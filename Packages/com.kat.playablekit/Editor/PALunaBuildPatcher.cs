#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PlayableKit.Editor
{
    /// <summary>
    /// Patches generated Luna build output in LunaTemp (mesh-shape inertia in the physics runtime,
    /// native video looping in UnityEngine.js). Re-applies whenever Luna rewrites a file.
    /// </summary>
    [InitializeOnLoad]
    internal static class PALunaBuildPatcher
    {
        private const string LunaTempFolder = "LunaTemp";
        private const double PollSeconds = 5d;

        private static readonly string[] FileNames =
        {
            "physics3d-0.js", "UnityEngine.js", "UnityEngine.js", "UnityEngine.js"
        };

        private static readonly string[] Labels =
        {
            "physics mesh-shape inertia", "video loop setter", "video listener", "video end-event"
        };

        private static readonly string[] Needles =
        {
            "xt.prototype.T=function(){throw Error(\"Not implemented for the mesh shape\")},xt.prototype.Ba=function(t,i){",
            "RAb$:{get:function(){if(TRACE){TRACE(\"UnityEngine.Video.VideoPlayer#isLooping#get\",this)}return this.yAb$},set:function(t){if(TRACE){TRACE(\"UnityEngine.Video.VideoPlayer#isLooping#set\",this)}this.yAb$=t}}",
            "t.addEventListener(\"play\",Bridge.fn.cacheBind(this,this.bAb$));t.addEventListener(\"ended\",Bridge.fn.cacheBind(this,this.cAb$));t.preload=\"auto\"}",
            "cAb$:function(){if(TRACE){TRACE(\"UnityEngine.Video.VideoPlayer#InvokeEndEvent\",this)}!Bridge.staticEquals(this.HAb$,null)?this.HAb$(this):null;if(this.RAb$){this.Play()}}"
        };

        private static readonly string[] Replacements =
        {
            "xt.prototype.T=function(){throw Error(\"Not implemented for the mesh shape\")},xt.prototype.Aa=function(t){var i=this.l.min,n=this.l.max,h=Math.max(.0001,.5*Math.abs(n.x-i.x)),s=Math.max(.0001,.5*Math.abs(n.y-i.y)),e=Math.max(.0001,.5*Math.abs(n.z-i.z)),o=(n.x+i.x)/2,a=(n.y+i.y)/2,r=(n.z+i.z)/2;U(t);var l=8*h*s*e,u=l/3;h*=h,s*=s,e*=e,t.g.set((s+e)*u,0,0,0,(h+e)*u,0,0,0,(h+s)*u),t.h.set(o,a,r),t.i=l},xt.prototype.Ba=function(t,i){",
            "RAb$:{get:function(){if(TRACE){TRACE(\"UnityEngine.Video.VideoPlayer#isLooping#get\",this)}return this.yAb$},set:function(t){if(TRACE){TRACE(\"UnityEngine.Video.VideoPlayer#isLooping#set\",this)}this.yAb$=t;if(this.DAb$!=null){this.DAb$.loop=t}}}",
            "t.addEventListener(\"play\",Bridge.fn.cacheBind(this,this.bAb$));t.addEventListener(\"ended\",Bridge.fn.cacheBind(this,this.cAb$));t.autoplay=this.xAb$;t.loop=this.yAb$;t.preload=\"auto\"}",
            "cAb$:function(){if(TRACE){TRACE(\"UnityEngine.Video.VideoPlayer#InvokeEndEvent\",this)}!Bridge.staticEquals(this.HAb$,null)?this.HAb$(this):null;if(this.RAb$&&(!this.DAb$||!this.DAb$.loop)){if(this.DAb$!=null){try{this.DAb$.currentTime=0}catch(t){}}this.Play()}}"
        };

        // A file containing the marker already has the patch.
        private static readonly string[] Markers =
        {
            "xt.prototype.Aa=function(t){var i=this.l.min,n=this.l.max",
            "RAb$:{get:function(){if(TRACE){TRACE(\"UnityEngine.Video.VideoPlayer#isLooping#get\",this)}return this.yAb$},set:function(t){if(TRACE){TRACE(\"UnityEngine.Video.VideoPlayer#isLooping#set\",this)}this.yAb$=t;if(this.DAb$!=null){this.DAb$.loop=t}}}",
            "t.addEventListener(\"play\",Bridge.fn.cacheBind(this,this.bAb$));t.addEventListener(\"ended\",Bridge.fn.cacheBind(this,this.cAb$));t.autoplay=this.xAb$;t.loop=this.yAb$;t.preload=\"auto\"}",
            "cAb$:function(){if(TRACE){TRACE(\"UnityEngine.Video.VideoPlayer#InvokeEndEvent\",this)}!Bridge.staticEquals(this.HAb$,null)?this.HAb$(this):null;if(this.RAb$&&(!this.DAb$||!this.DAb$.loop)){if(this.DAb$!=null){try{this.DAb$.currentTime=0}catch(t){}}this.Play()}}"
        };

        private static readonly Dictionary<string, DateTime> LastSeenWrite = new Dictionary<string, DateTime>();
        private static double _nextPollTime;

        static PALunaBuildPatcher()
        {
            EditorApplication.update += Poll;
        }

        [MenuItem("Tools/PlayableKit/Luna/Apply Build Patches Now", priority = 21)]
        private static void ApplyNow()
        {
            LastSeenWrite.Clear();
            int count = PatchAll();
            Debug.Log("[PlayableKit] Luna build patches: " + count + " patch(es) applied.");
        }

        private static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.timeSinceStartup < _nextPollTime) return;

            _nextPollTime = EditorApplication.timeSinceStartup + PollSeconds;
            PatchAll();
        }

        /// <summary>Patches every changed file under LunaTemp. Returns the number of patches applied.</summary>
        private static int PatchAll()
        {
            string root = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), LunaTempFolder);
            if (!Directory.Exists(root)) return 0;

            int applied = 0;
            for (int i = 0; i < FileNames.Length; i++)
            {
                if (IsFirstOfName(i)) applied += PatchFilesNamed(root, FileNames[i]);
            }
            return applied;
        }

        private static bool IsFirstOfName(int index)
        {
            for (int i = 0; i < index; i++)
            {
                if (FileNames[i] == FileNames[index]) return false;
            }
            return true;
        }

        private static int PatchFilesNamed(string root, string fileName)
        {
            int applied = 0;
            string[] paths = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
            for (int p = 0; p < paths.Length; p++)
            {
                string path = paths[p];
                DateTime written = File.GetLastWriteTimeUtc(path);
                DateTime seen;
                if (LastSeenWrite.TryGetValue(path, out seen) && written <= seen) continue;

                try
                {
                    string contents = File.ReadAllText(path);
                    string updated = contents;
                    for (int i = 0; i < FileNames.Length; i++)
                    {
                        if (FileNames[i] != fileName) continue;
                        if (updated.IndexOf(Markers[i], StringComparison.Ordinal) >= 0) continue;
                        if (updated.IndexOf(Needles[i], StringComparison.Ordinal) < 0) continue;

                        updated = updated.Replace(Needles[i], Replacements[i]);
                        applied++;
                        Debug.Log("[PlayableKit] Patched " + Labels[i] + ": " + path);
                    }

                    if (updated != contents)
                    {
                        File.WriteAllText(path, updated, new UTF8Encoding(false));
                        written = File.GetLastWriteTimeUtc(path);
                    }
                    LastSeenWrite[path] = written;
                }
                catch (IOException) { }                // file still being written: retry next tick
                catch (UnauthorizedAccessException) { } // same
            }
            return applied;
        }
    }
}
#endif
