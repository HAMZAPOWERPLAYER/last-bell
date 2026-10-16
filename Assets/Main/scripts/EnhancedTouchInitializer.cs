using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;

namespace NeKoRoSYS.InputHandling
{
    public static class EnhancedTouchInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
        }
    }
}