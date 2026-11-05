using System.Collections.Generic;
using UnityEngine;

namespace PlayableKit.UI
{
    public class PAScreenOrientationChecker : MonoBehaviour
    {
        [SerializeField] private TScreen _currentScreen;

        public TScreen currentScreen => _currentScreen;

        public bool isChange = true;

        [SerializeField] private List<GameObject> obj_Hori;
        [SerializeField] private List<GameObject> obj_Verti;

        [Header("Optional Camera FOV")]
        [SerializeField] private bool adjustCameraFov = false;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float landscapeFov = 65f;
        [SerializeField] private float verticalFov = 80f;

        private void Awake()
        {
            if (adjustCameraFov && targetCamera == null)
            {
                targetCamera = Camera.main;
            }
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
            SetListActive(obj_Hori, true);
            SetListActive(obj_Verti, false);

            if (adjustCameraFov && targetCamera != null)
            {
                targetCamera.fieldOfView = landscapeFov;
            }
        }

        private void OnVerti()
        {
            SetListActive(obj_Hori, false);
            SetListActive(obj_Verti, true);

            if (adjustCameraFov && targetCamera != null)
            {
                targetCamera.fieldOfView = verticalFov;
            }
        }

        private static void SetListActive(List<GameObject> list, bool active)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                {
                    list[i].SetActive(active);
                }
            }
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

        public static TScreen GetCurrentScreen()
        {
            return Screen.width > Screen.height ? TScreen.LandscapeScreen : TScreen.VerticalScreen;
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
}
