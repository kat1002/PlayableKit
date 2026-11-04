using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundEnifi : MonoBehaviour
{
    public AudioSource audioSource;
    public void Play()
    {
        if (audioSource == null) return;
        audioSource.Play();
    }
    public void Stop()
    {
        if (audioSource == null) return;
        audioSource.Stop();
    }

}
