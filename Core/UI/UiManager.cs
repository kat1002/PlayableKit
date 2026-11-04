using BlackGemsGlobal.SeatAway.GamePlayEvent;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CTGame
{
    public class UiManager : MonoBehaviour
    {
        public GameObject ctaButton;
        private void Awake()
        {
            GameEventManager.RegisterEvent(GameEventManager.EventId.OnActiveCTA, OnCTAActive);
            GameEventManager.RegisterEvent(GameEventManager.EventId.OnDeactiveCTA, OnCTADeactive);
        }
        private void OnDestroy()
        {
            GameEventManager.UnRegisterEvent(GameEventManager.EventId.OnActiveCTA, OnCTAActive);
            GameEventManager.UnRegisterEvent(GameEventManager.EventId.OnDeactiveCTA, OnCTADeactive);
        }
        
        private void OnCTAActive()
        {
            ctaButton.SetActive(true);
        }
        
        private void OnCTADeactive()
        {
            ctaButton.SetActive(false);
        }

    }
}