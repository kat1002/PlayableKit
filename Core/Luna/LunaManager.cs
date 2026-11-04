using BlackGemsGlobal.SeatAway.GamePlayEvent;
using Playable.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LunaManager : MonoBehaviour
{
    public static LunaManager instance;
    [SerializeField] private float currentEndTime = 15;
    private bool isStart;
    private bool isEnded;
    private void Awake()
    {
        instance = this;
        PlayableKit.Analytics.PlayableAnalytics.AddSink(Shared.UI.Adapters.LunaAnalyticsSink.Instance);
    }

    private void OnDestroy()
    {
        PlayableKit.Analytics.PlayableAnalytics.RemoveSink(Shared.UI.Adapters.LunaAnalyticsSink.Instance);
    }

    /// <summary>
    /// Overrides the auto-store delay. Must be called before <see cref="StartGame"/> (the timer is armed there).
    /// Ignored when there is no instance or seconds is not positive.
    /// </summary>
    public static void SetEndTime(float seconds)
    {
        if (instance != null && seconds > 0f)
        {
            instance.currentEndTime = seconds;
        }
    }

    public void StartGame()
    {
        if (!isStart)
        {
            isStart = true;
            Luna.Unity.LifeCycle.GameStarted();
            DOVirtual.DelayedCall(currentEndTime, GoToStore);
        }

    }

    public void GoToStore()
    {
        // Report the game as started (once) without arming the auto-store timer: the store is opening anyway.
        if (!isStart)
        {
            isStart = true;
            Luna.Unity.LifeCycle.GameStarted();
        }

        DOVirtual.DelayedCall(0.1f, OpenStore);
    }

    private void OpenStore()
    {
        // GameEnded is a one-shot lifecycle event; the store itself may be opened again
        // (e.g. after the player returns from it).
        if (!isEnded)
        {
            isEnded = true;
            Luna.Unity.LifeCycle.GameEnded();
        }
        Luna.Unity.Playable.InstallFullGame();
    }
}
