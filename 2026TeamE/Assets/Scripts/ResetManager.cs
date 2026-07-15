using UnityEngine;
using UnityEngine.InputSystem;

public class ResetManager : MonoBehaviour
{
    public static void ResetAll(bool resetUpgrades = false, bool regenerateTerrain = false)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StopLoopSE();
            SoundManager.Instance.StopSE();
        }

        if (CheckpointManager.Instance != null)
        {
            CheckpointManager.Instance.ResetCheckpoint();
        }

        if (ItemInventoryManager.Instance != null)
        {
            ItemInventoryManager.Instance.ClearItems();
        }
        ItemInventoryManager.ClearCollectedData();

        if (VoxelTerrain.Instance != null)
        {
            VoxelTerrain.Instance.ResetRuntime(regenerateTerrain);
        }

        if (HapticsManager.Instance != null)
        {
            HapticsManager.Instance.Stop();
        }
        else
        {
            var pad = Gamepad.current;
            if (pad != null)
            {
                pad.SetMotorSpeeds(0f, 0f);
            }
        }

        if (resetUpgrades)
        {
            UpgradeManager.ResetUpgrades();
        }
    }
}
