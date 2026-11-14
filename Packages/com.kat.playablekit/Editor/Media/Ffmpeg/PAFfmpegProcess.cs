#if UNITY_EDITOR
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace PlayableKit.Editor.Media
{
    /// <summary>Outcome of a finished external process.</summary>
    public struct PAProcessResult
    {
        public int ExitCode;
        public string Output;
        public string Error;
        public bool TimedOut;

        public bool Succeeded
        {
            get { return ExitCode == 0 && !TimedOut; }
        }

        /// <summary>Last few lines of stderr (ffmpeg prints the real cause at the end), or a fallback message.</summary>
        public string ErrorSummary
        {
            get
            {
                if (TimedOut)
                {
                    return "Timed out.";
                }
                return PAFfmpegProcess.Tail(string.IsNullOrEmpty(Error) ? Output : Error, 4);
            }
        }
    }

    /// <summary>Synchronous process runs plus the small helpers every ffmpeg command line needs.</summary>
    public static class PAFfmpegProcess
    {
        public static string NullDevice
        {
            get { return Environment.OSVersion.Platform == PlatformID.Win32NT ? "NUL" : "/dev/null"; }
        }

        /// <summary>Runs a short command (probe, version, single frame) and waits for it.</summary>
        public static PAProcessResult Run(string executable, string arguments, int timeoutMs)
        {
            var output = new StringBuilder();
            var error = new StringBuilder();
            Process process;
            try
            {
                process = Start(executable, arguments);
            }
            catch (Win32Exception e)
            {
                return new PAProcessResult { ExitCode = -1, Error = "Cannot start '" + executable + "': " + e.Message };
            }

            using (process)
            {
                process.OutputDataReceived += (s, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (error) error.AppendLine(e.Data); };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(timeoutMs))
                {
                    Kill(process);
                    return new PAProcessResult { ExitCode = -1, TimedOut = true };
                }
                process.WaitForExit(); // flush async readers

                lock (output) lock (error)
                {
                    return new PAProcessResult { ExitCode = process.ExitCode, Output = output.ToString(), Error = error.ToString() };
                }
            }
        }

        internal static Process Start(string executable, string arguments)
        {
            var info = new ProcessStartInfo(executable, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            var process = new Process { StartInfo = info, EnableRaisingEvents = true };
            process.Start();
            return process;
        }

        internal static void Kill(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (InvalidOperationException)
            {
                // already exited
            }
        }

        /// <summary>Quotes one command-line argument (Windows CommandLineToArgvW rules, also fine for Mono on macOS/Linux).</summary>
        public static string Quote(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            var builder = new StringBuilder("\"");
            int backslashes = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }
                if (c == '"')
                {
                    builder.Append('\\', backslashes * 2 + 1).Append('"');
                }
                else
                {
                    builder.Append('\\', backslashes).Append(c);
                }
                backslashes = 0;
            }
            builder.Append('\\', backslashes * 2).Append('"');
            return builder.ToString();
        }

        public static string Tail(string text, int lines)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "Unknown error.";
            }
            string[] all = text.Trim().Split('\n');
            int start = Math.Max(0, all.Length - lines);
            return string.Join("\n", all, start, all.Length - start).Trim();
        }
    }
}
#endif
