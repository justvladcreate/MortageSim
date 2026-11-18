using UnityEngine;
using UnityEngine.SceneManagement;

namespace MortgageSim
{
    /// <summary>Запускает игру в SampleScene без изменения самой сцены.</summary>
    public static class GameBootstrap
    {
        private const string SceneName = "SampleScene";

        // Подписка живёт между загрузками сцен: после «В главное меню» сцена
        // перезагружается, и игру нужно собрать заново.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            TryBoot(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryBoot(scene);
        }

        private static void TryBoot(Scene scene)
        {
            if (!scene.IsValid() || scene.name != SceneName) return;
            if (Object.FindAnyObjectByType<GameManager>() != null) return;

            var root = new GameObject("MortgageSim_Game");
            var manager = root.AddComponent<GameManager>();
            manager.Boot();
        }
    }
}
