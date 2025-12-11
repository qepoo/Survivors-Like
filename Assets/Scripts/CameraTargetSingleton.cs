using UnityEngine;

public class CameraTargetSingleton : MonoBehaviour
{
    public static CameraTargetSingleton Instance;

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogWarning("Multiple Instances of CameraTargetSingleton were detected. Destroying new instance: ", Instance);
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
}
