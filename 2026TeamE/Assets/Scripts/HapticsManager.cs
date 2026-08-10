using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class HapticsManager : MonoBehaviour
{
    public static HapticsManager Instance { get; private set; }

    private Coroutine pulseCoroutine;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayContinuous(float low, float high)
    {
        foreach (var pad in Gamepad.all)
        {
            pad?.SetMotorSpeeds(low, high);
        }
    }

    public void PlayPulse(float low, float high, float duration)
    {
        if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
        pulseCoroutine = StartCoroutine(PulseRoutine(low, high, duration));
    }

    private IEnumerator PulseRoutine(float low, float high, float duration)
    {
        PlayContinuous(low, high);
        yield return new WaitForSeconds(duration);
        Stop();
        pulseCoroutine = null;
    }

    public void Stop()
    {
        foreach (var pad in Gamepad.all)
        {
            pad?.SetMotorSpeeds(0f, 0f);
        }
    }

    void OnDisable() => Stop();
    void OnApplicationQuit() => Stop();
}
