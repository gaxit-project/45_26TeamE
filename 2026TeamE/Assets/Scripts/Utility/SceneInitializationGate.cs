using UnityEngine;














public static class SceneInitializationGate
{
    private static int pendingCount;

    
    public static bool IsBusy => pendingCount > 0;

    
    public static void Begin()
    {
        pendingCount++;
    }

    
    public static void End()
    {
        if (pendingCount > 0)
        {
            pendingCount--;
        }
    }

    
    
    
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Reset()
    {
        pendingCount = 0;
    }
}
