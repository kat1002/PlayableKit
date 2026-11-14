#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace PlayableKit.Editor.Media
{
    /// <summary>One external command in a <see cref="PAFfmpegJob"/>.</summary>
    public sealed class PAFfmpegStep
    {
        public readonly string Label;
        public readonly string Executable;
        public readonly string Arguments;
        /// <summary>Share of the job's progress bar (any positive scale; weights are normalized).</summary>
        public readonly float Weight;

        public PAFfmpegStep(string label, string executable, string arguments, float weight)
        {
            Label = label;
            Executable = executable;
            Arguments = arguments;
            Weight = weight <= 0f ? 1f : weight;
        }
    }

    public enum PAJobState { Idle, Running, Succeeded, Failed, Canceled }

    /// <summary>
    /// Runs ffmpeg steps one after another without blocking the Editor. Call <see cref="Poll"/> from
    /// EditorApplication.update. Progress comes from <c>-progress pipe:1</c> (<c>out_time_us</c>) against the media duration.
    /// </summary>
    public sealed class PAFfmpegJob
    {
        private const int MaxLogLines = 60;

        private readonly List<PAFfmpegStep> _steps;
        private readonly double _durationSeconds;
        private readonly float _totalWeight;
        private readonly object _lock = new object();
        private readonly StringBuilder _stderr = new StringBuilder();
        private readonly Queue<string> _log = new Queue<string>();

        private Process _process;
        private int _stepIndex = -1;
        private double _stepSeconds;
        private float _doneWeight;
        private bool _cancelRequested;

        public PAJobState State { get; private set; }
        public string Error { get; private set; }

        public PAFfmpegJob(List<PAFfmpegStep> steps, double durationSeconds)
        {
            _steps = steps;
            _durationSeconds = durationSeconds;
            for (int i = 0; i < steps.Count; i++)
            {
                _totalWeight += steps[i].Weight;
            }
        }

        public bool IsRunning
        {
            get { return State == PAJobState.Running; }
        }

        public string CurrentLabel
        {
            get { return _stepIndex >= 0 && _stepIndex < _steps.Count ? _steps[_stepIndex].Label : string.Empty; }
        }

        /// <summary>0..1 across all steps.</summary>
        public float Progress
        {
            get
            {
                if (State == PAJobState.Succeeded) return 1f;
                if (_stepIndex < 0 || _totalWeight <= 0f) return 0f;

                double stepFraction;
                lock (_lock)
                {
                    stepFraction = _durationSeconds > 0d ? Math.Min(1d, _stepSeconds / _durationSeconds) : 0d;
                }
                return (_doneWeight + (float)stepFraction * _steps[_stepIndex].Weight) / _totalWeight;
            }
        }

        public string[] LogLines()
        {
            lock (_lock)
            {
                return _log.ToArray();
            }
        }

        public void Start()
        {
            State = PAJobState.Running;
            StartStep(0);
        }

        public void Cancel()
        {
            _cancelRequested = true;
            if (_process != null)
            {
                PAFfmpegProcess.Kill(_process);
                _process.WaitForExit(2000);
            }
        }

        /// <summary>Advances to the next step when the current one exits. Returns true while the job is running.</summary>
        public bool Poll()
        {
            if (State != PAJobState.Running) return false;
            if (_process == null || !_process.HasExited) return true;

            _process.WaitForExit(); // flush async readers
            int exitCode = _process.ExitCode;
            DisposeProcess();

            if (_cancelRequested)
            {
                State = PAJobState.Canceled;
                return false;
            }
            if (exitCode != 0)
            {
                string stderr;
                lock (_lock)
                {
                    stderr = _stderr.ToString();
                }
                Fail(_steps[_stepIndex].Label + " failed (exit " + exitCode + "): " + PAFfmpegProcess.Tail(stderr, 4));
                return false;
            }

            _doneWeight += _steps[_stepIndex].Weight;
            if (_stepIndex + 1 >= _steps.Count)
            {
                State = PAJobState.Succeeded;
                return false;
            }
            StartStep(_stepIndex + 1);
            return State == PAJobState.Running;
        }

        private void StartStep(int index)
        {
            _stepIndex = index;
            lock (_lock)
            {
                _stepSeconds = 0d;
                _stderr.Length = 0;
            }

            PAFfmpegStep step = _steps[index];
            AddLog("> " + step.Label);
            try
            {
                _process = PAFfmpegProcess.Start(step.Executable, step.Arguments);
            }
            catch (Win32Exception e)
            {
                Fail("Cannot start '" + step.Executable + "': " + e.Message);
                return;
            }

            _process.OutputDataReceived += OnOutput;
            _process.ErrorDataReceived += OnError;
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        private void OnOutput(object sender, DataReceivedEventArgs e)
        {
            string line = e.Data;
            if (string.IsNullOrEmpty(line)) return;

            // out_time_us is microseconds; older builds only print out_time_ms, which is also microseconds.
            const string us = "out_time_us=";
            const string ms = "out_time_ms=";
            string value = null;
            if (line.StartsWith(us, StringComparison.Ordinal)) value = line.Substring(us.Length);
            else if (line.StartsWith(ms, StringComparison.Ordinal)) value = line.Substring(ms.Length);
            if (value == null) return;

            long micros;
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out micros) && micros >= 0)
            {
                lock (_lock)
                {
                    _stepSeconds = micros / 1000000d;
                }
            }
        }

        private void OnError(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data)) return;
            lock (_lock)
            {
                _stderr.AppendLine(e.Data);
            }
            AddLog(e.Data);
        }

        private void AddLog(string line)
        {
            lock (_lock)
            {
                _log.Enqueue(line);
                while (_log.Count > MaxLogLines) _log.Dequeue();
            }
        }

        private void Fail(string message)
        {
            Error = message;
            AddLog(message);
            State = PAJobState.Failed;
            DisposeProcess();
        }

        private void DisposeProcess()
        {
            if (_process == null) return;
            _process.OutputDataReceived -= OnOutput;
            _process.ErrorDataReceived -= OnError;
            _process.Dispose();
            _process = null;
        }
    }
}
#endif
