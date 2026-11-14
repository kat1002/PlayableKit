#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using UnityEditor;

namespace PlayableKit.Editor.Media
{
    public enum PAEncodeStatus { Pending, Encoding, Done, Warning, Failed, Canceled }

    /// <summary>
    /// Encodes one video: plan → two-pass encode (+ decode check) → verify → optional re-encode to fit the budget → import.
    /// Drive it with <see cref="Poll"/> from EditorApplication.update.
    /// </summary>
    public sealed class PAVideoEncodeTask
    {
        private const int MaxBudgetRetries = 2;

        public readonly string InputPath;
        public readonly PAMediaInfo Source;

        private PAVideoEncodePlan _plan;
        private PAFfmpegJob _job;
        private string _passLogBase;
        private int _retries;

        public PAEncodeStatus Status { get; private set; }
        public string Message { get; private set; }
        public PAVideoEncodePlan Plan { get { return _plan; } }

        public PAVideoEncodeTask(string inputPath, PAMediaInfo source)
        {
            InputPath = inputPath;
            Source = source;
        }

        public string FileName
        {
            get { return Path.GetFileName(InputPath); }
        }

        public bool IsFinished
        {
            get { return Status != PAEncodeStatus.Pending && Status != PAEncodeStatus.Encoding; }
        }

        public float Progress
        {
            get { return Status == PAEncodeStatus.Encoding && _job != null ? _job.Progress : (IsFinished ? 1f : 0f); }
        }

        public string StepLabel
        {
            get { return _job == null ? string.Empty : _job.CurrentLabel + (_retries > 0 ? " (refit " + _retries + ")" : string.Empty); }
        }

        public string[] LogLines()
        {
            return _job == null ? new string[0] : _job.LogLines();
        }

        public void Start(PAVideoEncodeSettings settings)
        {
            // Settings are copied so edits in the window do not change a running encode.
            var snapshot = new PAVideoEncodeSettings();
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(settings), snapshot);

            _plan = new PAVideoEncodePlan(InputPath, Source, snapshot);
            string problem;
            if (!_plan.IsValid(out problem))
            {
                Finish(PAEncodeStatus.Failed, problem);
                return;
            }

            string folder = Path.GetDirectoryName(_plan.VideoPath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            _passLogBase = Path.Combine(Path.GetTempPath(), "pa_x264_" + System.Guid.NewGuid().ToString("N"));
            RunEncode();
        }

        public void Cancel()
        {
            if (_job != null && _job.IsRunning) _job.Cancel();
            else if (Status == PAEncodeStatus.Pending) Status = PAEncodeStatus.Canceled;
        }

        public void Poll()
        {
            if (Status != PAEncodeStatus.Encoding || _job == null || _job.Poll()) return;

            switch (_job.State)
            {
                case PAJobState.Canceled:
                    DeleteOutputs();
                    Finish(PAEncodeStatus.Canceled, "Canceled.");
                    break;
                case PAJobState.Failed:
                    DeleteOutputs();
                    Finish(PAEncodeStatus.Failed, _job.Error);
                    break;
                default:
                    OnEncoded();
                    break;
            }
        }

        private void RunEncode()
        {
            Status = PAEncodeStatus.Encoding;
            _job = new PAFfmpegJob(_plan.BuildSteps(_passLogBase), Source.DurationSeconds);
            _job.Start();
        }

        private void OnEncoded()
        {
            PAVerifyResult verify = PAVideoOutputVerifier.Verify(_plan);
            if (!verify.Passed)
            {
                DeleteOutputs();
                Finish(PAEncodeStatus.Failed, verify.Message);
                return;
            }

            if (verify.OverBudget && _plan.Settings.autoFitBudget && _retries < MaxBudgetRetries
                && _plan.RefitBitrate(verify.VideoBytes, verify.AudioBytes))
            {
                _retries++;
                DeleteOutputs();
                RunEncode();
                return;
            }

            CleanupPassLogs();
            PAMediaAssets.ImportVideo(_plan.VideoPath, _plan.Settings.audioMode == PAVideoAudioMode.EmbeddedAac);
            if (_plan.AudioPath != null) PAMediaAssets.ImportMusic(_plan.AudioPath);

            string size = (verify.TotalBytes / 1000000d).ToString("0.00", CultureInfo.InvariantCulture) + " MB";
            string summary = size + " @ " + _plan.VideoBitrateKbps + " kbps. " + (verify.Message ?? string.Empty);
            if (verify.OverBudget)
            {
                Finish(PAEncodeStatus.Warning, "Over the " + _plan.Settings.budgetMb.ToString("0.0#", CultureInfo.InvariantCulture)
                                               + " MB budget: " + summary);
            }
            else
            {
                Finish(PAEncodeStatus.Done, summary);
            }
        }

        private void Finish(PAEncodeStatus status, string message)
        {
            CleanupPassLogs();
            Status = status;
            Message = message;
        }

        private void DeleteOutputs()
        {
            if (_plan == null) return;
            PAMediaAssets.DeleteIfExists(_plan.VideoPath);
            PAMediaAssets.DeleteIfExists(_plan.AudioPath);
        }

        private void CleanupPassLogs()
        {
            if (string.IsNullOrEmpty(_passLogBase)) return;
            string folder = Path.GetDirectoryName(_passLogBase);
            string prefix = Path.GetFileName(_passLogBase);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;

            string[] files = Directory.GetFiles(folder, prefix + "*");
            for (int i = 0; i < files.Length; i++) PAMediaAssets.DeleteIfExists(files[i]);
        }
    }
}
#endif
