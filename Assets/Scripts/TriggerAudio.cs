using UnityEngine;

public class TriggerAudio : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip clip;

    void OnTriggerEnter(Collider other)
    {
        if (audioSource == null || clip == null)
            return;

        Transform t = other.transform;
        bool esJugador = false;
        while (t != null)
        {
            if (t.name.Contains("XR Origin"))
            {
                esJugador = true;
                break;
            }
            t = t.parent;
        }

        if (esJugador)
            audioSource.PlayOneShot(clip);
    }
}
