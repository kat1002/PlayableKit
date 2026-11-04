using System.Collections;
using UnityEngine;
using PlayableTemplate.Core;

public class LunaManager : MonoBehaviour
{
    public static LunaManager instance;

    [LunaPlaygroundField("Safety Timeout", fieldSection: "Lifecycle")]
    [SerializeField, Tooltip("Hard cap: force the end card after this many seconds no matter what.")]
    public float safetyTimeout = 45f;

    private bool _started;
    private bool _ended;
    private bool _ctaClicked;
    private Coroutine _safetyRoutine;

    private void Awake()
    {
        instance = this;
    }

    public void StartGame()
    {
        if (_started) return;
        _started = true;
        CallGameStarted();
        _safetyRoutine = StartCoroutine(SafetyNet());
    }

    /// <summary>Report the outcome to ad networks. Safe to call multiple times.</summary>
    public void EndGame()
    {
        if (_ended) return;
        _ended = true;

        if (_safetyRoutine != null)
        {
            StopCoroutine(_safetyRoutine);
            _safetyRoutine = null;
        }

        CallGameEnded();
    }

    /// <summary>Hand off to the store from a CTA or an automatic game policy.</summary>
    public void GoToStore()
    {
        if (!_started) StartGame();
        EndGame();
        _ctaClicked = true;
        CallInstallFullGame();
    }

    /// <summary>Player came back from the store (or wherever the CTA sent them).</summary>
    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && _ctaClicked && GameManagerBase.Instance != null)
            GameManagerBase.Instance.ReshowEndCard();
    }

    private IEnumerator SafetyNet()
    {
        yield return new WaitForSecondsRealtime(safetyTimeout);
        if (!_ended)
        {
            if (GameManagerBase.Instance != null)
                GameManagerBase.Instance.ForceEnd(EndReason.Timeout);
            else
                EndGame();
        }
    }

    public static void TrackEvent(string eventName)
    {
        try
        {
            Luna.Unity.Analytics.LogEvent(eventName, 0);
        }
        catch (System.Exception)
        {
            // Editor fallback
        }
    }

    private static void CallGameStarted()
    {
        try
        {
            Luna.Unity.LifeCycle.GameStarted();
        }
        catch (System.Exception)
        {
            Debug.Log("[LunaManager] GameStarted (editor fallback)");
        }
    }

    private static void CallGameEnded()
    {
        try
        {
            Luna.Unity.LifeCycle.GameEnded();
        }
        catch (System.Exception)
        {
            Debug.Log("[LunaManager] GameEnded (editor fallback)");
        }
    }

    private static void CallInstallFullGame()
    {
        try
        {
            Luna.Unity.Playable.InstallFullGame();
        }
        catch (System.Exception)
        {
            Debug.Log("[LunaManager] InstallFullGame (editor fallback)");
        }
    }

    private void OnDestroy()
    {
        if (_safetyRoutine != null)
        {
            StopCoroutine(_safetyRoutine);
            _safetyRoutine = null;
        }
        Time.timeScale = 1f;
        if (instance == this) instance = null;
    }
}
