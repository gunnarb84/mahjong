using UnityEngine;

namespace Mahjong.Game
{
    /// <summary>
    /// Baut die Szene zur Laufzeit selbst auf, damit jede (auch leere) Szene
    /// funktioniert: einfach Play druecken. Erzeugt den GameManager, falls
    /// keiner existiert — danach richtet der GameManager Kamera/Licht ein.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Bootstrap()
        {
            if (Object.FindFirstObjectByType<GameManager>() != null)
            {
                return;
            }

            var go = new GameObject("~Mahjong");
            go.AddComponent<GameManager>();
        }
    }
}