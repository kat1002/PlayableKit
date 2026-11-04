using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MyLunaHelper : MonoBehaviour
{

   public void OnGameEnd()
    {
//#if UNITY_EDITOR
//        Debug.Log("[LUNA] OnGameEnd");
//#else
//        Luna.Unity.LifeCycle.GameEnded();
//#endif
    }    

    public void OnClickInstall()
    {
//#if UNITY_EDITOR
//        Debug.Log("[LUNA] OnClickInstall");
//#else
//        Luna.Unity.Playable.InstallFullGame();
//#endif
    }
}
