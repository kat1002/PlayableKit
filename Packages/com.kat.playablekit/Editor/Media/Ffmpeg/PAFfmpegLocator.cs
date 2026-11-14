#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;

namespace PlayableKit.Editor.Media
{
    /// <summary>
    /// Finds and remembers the ffmpeg / ffprobe executables. The path is shared by every PlayableKit media tool
    /// (EditorPrefs key kept from the original Fake-Video tools, so a saved path keeps working).
    /// </summary>
    public static class PAFfmpegLocator
    {
        private const string PathPreferenceKey = "PlayableVideoConverter.FfmpegPath";

        private static readonly string[] CommonFolders =
        {
            @"C:\ffmpeg\bin",
            @"C:\Program Files\ffmpeg\bin",
            @"C:\ProgramData\chocolatey\bin",
            "/opt/homebrew/bin",
            "/usr/local/bin",
            "/usr/bin"
        };

        private static bool IsWindows
        {
            get { return Environment.OSVersion.Platform == PlatformID.Win32NT; }
        }

        private static string ExeSuffix
        {
            get { return IsWindows ? ".exe" : string.Empty; }
        }

        /// <summary>Saved path, else an auto-detected one, else the bare command name (resolved through PATH).</summary>
        public static string FfmpegPath
        {
            get
            {
                string saved = EditorPrefs.GetString(PathPreferenceKey, string.Empty);
                if (!string.IsNullOrEmpty(saved))
                {
                    return saved;
                }
                string detected = Detect();
                return string.IsNullOrEmpty(detected) ? "ffmpeg" : detected;
            }
            set { EditorPrefs.SetString(PathPreferenceKey, value == null ? string.Empty : value.Trim()); }
        }

        /// <summary>ffprobe next to ffmpeg when ffmpeg is a full path, else "ffprobe" from PATH.</summary>
        public static string FfprobePath
        {
            get
            {
                string ffmpeg = FfmpegPath;
                if (Path.IsPathRooted(ffmpeg))
                {
                    string candidate = Path.Combine(Path.GetDirectoryName(ffmpeg) ?? string.Empty, "ffprobe" + ExeSuffix);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                return "ffprobe";
            }
        }

        /// <summary>Looks in PATH, then in common install folders (choco, scoop, winget, Homebrew). Null when not found.</summary>
        public static string Detect()
        {
            string exe = "ffmpeg" + ExeSuffix;

            string pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            string[] pathFolders = pathVariable.Split(Path.PathSeparator);
            for (int i = 0; i < pathFolders.Length; i++)
            {
                string found = Existing(pathFolders[i], exe);
                if (found != null)
                {
                    return found;
                }
            }

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] userFolders =
            {
                Path.Combine(home, "scoop", "shims"),
                Path.Combine(home, "AppData", "Local", "Microsoft", "WinGet", "Links")
            };
            for (int i = 0; i < userFolders.Length; i++)
            {
                string found = Existing(userFolders[i], exe);
                if (found != null)
                {
                    return found;
                }
            }

            for (int i = 0; i < CommonFolders.Length; i++)
            {
                string found = Existing(CommonFolders[i], exe);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        /// <summary>Runs <c>ffmpeg -version</c>. Returns the first line on success, null on failure.</summary>
        public static string Verify(out string error)
        {
            PAProcessResult result = PAFfmpegProcess.Run(FfmpegPath, "-hide_banner -version", 10000);
            if (!result.Succeeded)
            {
                error = result.ErrorSummary;
                return null;
            }

            PAProcessResult probe = PAFfmpegProcess.Run(FfprobePath, "-hide_banner -version", 10000);
            if (!probe.Succeeded)
            {
                error = "ffmpeg works, but ffprobe was not found next to it or in PATH.";
                return null;
            }

            error = null;
            string output = result.Output ?? string.Empty;
            int newLine = output.IndexOf('\n');
            return newLine > 0 ? output.Substring(0, newLine).Trim() : output.Trim();
        }

        private static string Existing(string folder, string exe)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return null;
            }
            try
            {
                string candidate = Path.Combine(folder.Trim(), exe);
                return File.Exists(candidate) ? candidate : null;
            }
            catch (ArgumentException)
            {
                return null; // malformed PATH entry
            }
        }
    }
}
#endif
