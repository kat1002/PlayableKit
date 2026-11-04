using System.Collections.Generic;
using BlackGemsGlobal.SeatAway.GamePlayEvent;
using UnityEngine;

public class ScreenOrientationChecker : MonoBehaviour
{
    [SerializeField] TScreen _currentScreen;
    
    public TScreen currentScreen => _currentScreen;

    public bool isChange;


    [SerializeField] private List<GameObject> obj_Hori;
    [SerializeField] private List<GameObject> obj_Verti;


    private void Awake()
    {
        GameEventManager.RegisterEvent(GameEventManager.EventId.LandscapeScreen, OnHori);
        GameEventManager.RegisterEvent(GameEventManager.EventId.VerticalScreen, OnVerti);
    }
    private void OnDestroy()
    {
        GameEventManager.UnRegisterEvent(GameEventManager.EventId.LandscapeScreen, OnHori);
        GameEventManager.UnRegisterEvent(GameEventManager.EventId.VerticalScreen, OnVerti);
    }

    private void Start()
    {
        OnLoadScreen();
    }

    private void OnHori()
    {
        obj_Hori.ForEach(n => n.SetActive(true));
        obj_Verti.ForEach(n => n.SetActive(false));

        if (Camera.main != null) Camera.main.fieldOfView = 65;
    }
    private void OnVerti()
    {
        obj_Hori.ForEach(n => n.SetActive(false));
        obj_Verti.ForEach(n => n.SetActive(true));

        if (Camera.main != null) Camera.main.fieldOfView = 80;
    }

    public void OnLoadScreen()
    {
        isChange = true;
    }

    private void Update()
    {
        if (isChange)
            OnChangeScreen();
    }

    private void OnChangeScreen()
    {
        TScreen newScreen = GetCurrentScreen();

        if (newScreen != _currentScreen)
        {
            OnScreenChanged(newScreen);

            _currentScreen = newScreen;
        }
    }

    private TScreen GetCurrentScreen()
    {
        if (Screen.width > Screen.height)
        {
            return TScreen.LandscapeScreen;
        }
        else
        {
            return TScreen.VerticalScreen;
        }
    }

    private void OnScreenChanged(TScreen screen)
    {
        if (screen == TScreen.LandscapeScreen)
        {
            GameEventManager.RaisedEvent(GameEventManager.EventId.LandscapeScreen);
        }
        else
        {
            GameEventManager.RaisedEvent(GameEventManager.EventId.VerticalScreen);
        }
    }

}
public enum TScreen
{
    None,
    LandscapeScreen,
    VerticalScreen
}
