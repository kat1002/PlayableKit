using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GangterCity.PlayableSpider.Editor
{
    internal static class PlayableSpiderLunaPhysicsPatch
    {
        private const string PhysicsFileName = "physics3d-0.js";
        private const string LunaTempFolderName = "LunaTemp";
        private const double AutoPatchIntervalSeconds = 5d;
        private const string MeshShapeAaMarker = "xt.prototype.Aa=function(t){var i=this.l.min,n=this.l.max";
        private const string MeshShapeAaNeedle =
            "xt.prototype.T=function(){throw Error(\"Not implemented for the mesh shape\")},xt.prototype.Ba=function(t,i){";
        private const string MeshShapeAaReplacement =
            "xt.prototype.T=function(){throw Error(\"Not implemented for the mesh shape\")},xt.prototype.Aa=function(t){var i=this.l.min,n=this.l.max,h=Math.max(.0001,.5*Math.abs(n.x-i.x)),s=Math.max(.0001,.5*Math.abs(n.y-i.y)),e=Math.max(.0001,.5*Math.abs(n.z-i.z)),o=(n.x+i.x)/2,a=(n.y+i.y)/2,r=(n.z+i.z)/2;U(t);var l=8*h*s*e,u=l/3;h*=h,s*=s,e*=e,t.g.set((s+e)*u,0,0,0,(h+e)*u,0,0,0,(h+s)*u),t.h.set(o,a,r),t.i=l},xt.prototype.Ba=function(t,i){";

        private static double _nextAutoPatchTime;

        private enum PatchStatus
        {
            Patched,
            AlreadyPatched,
            NoMatch
        }

        [InitializeOnLoadMethod]
        private static void RegisterAutoPatch()
        {
            EditorApplication.update -= AutoPatchUpdate;
            EditorApplication.update += AutoPatchUpdate;
            EditorApplication.delayCall += PatchOnDelayCall;
        }

        [MenuItem("Tools/Playable Spider/Patch Luna Physics Mesh Inertia")]
        private static void PatchMenu()
        {
            PatchKnownFiles(true);
        }

        public static int PatchKnownFiles(bool logResults = true)
        {
            var lunaTempRoot = Path.Combine(GetProjectRoot(), LunaTempFolderName);
            if (!Directory.Exists(lunaTempRoot))
            {
                if (logResults)
                {
                    Debug.Log("PlayableSpiderLunaPhysicsPatch: no LunaTemp folder found to patch.");
                }

                return 0;
            }

            var physicsFiles = Directory.GetFiles(lunaTempRoot, PhysicsFileName, SearchOption.AllDirectories);
            var patchedCount = 0;
            var alreadyPatchedCount = 0;
            var skippedCount = 0;

            for (var index = 0; index < physicsFiles.Length; index++)
            {
                var filePath = physicsFiles[index];
                try
                {
                    switch (TryPatchFile(filePath))
                    {
                        case PatchStatus.Patched:
                            patchedCount++;
                            break;
                        case PatchStatus.AlreadyPatched:
                            alreadyPatchedCount++;
                            break;
                        default:
                            skippedCount++;
                            break;
                    }
                }
                catch (Exception exception)
                {
                    skippedCount++;
                    if (logResults)
                    {
                        Debug.LogWarning(
                            $"PlayableSpiderLunaPhysicsPatch: failed to patch '{filePath}': {exception.Message}");
                    }
                }
            }

            if (logResults || patchedCount > 0)
            {
                Debug.Log(
                    "PlayableSpiderLunaPhysicsPatch: " +
                    $"patched={patchedCount}, alreadyPatched={alreadyPatchedCount}, skipped={skippedCount}.");
            }

            return patchedCount;
        }

        private static void PatchOnDelayCall()
        {
            PatchKnownFiles(false);
        }

        private static void AutoPatchUpdate()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup < _nextAutoPatchTime)
            {
                return;
            }

            _nextAutoPatchTime = EditorApplication.timeSinceStartup + AutoPatchIntervalSeconds;
            PatchKnownFiles(false);
        }

        private static PatchStatus TryPatchFile(string filePath)
        {
            var contents = File.ReadAllText(filePath);
            if (contents.IndexOf(MeshShapeAaMarker, StringComparison.Ordinal) >= 0)
            {
                return PatchStatus.AlreadyPatched;
            }

            var needleIndex = contents.IndexOf(MeshShapeAaNeedle, StringComparison.Ordinal);
            if (needleIndex < 0)
            {
                return PatchStatus.NoMatch;
            }

            var patchedContents = contents.Replace(MeshShapeAaNeedle, MeshShapeAaReplacement);
            if (ReferenceEquals(contents, patchedContents) || contents == patchedContents)
            {
                return PatchStatus.NoMatch;
            }

            File.WriteAllText(filePath, patchedContents, new UTF8Encoding(false));
            return PatchStatus.Patched;
        }

        private static string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }
    }
}
