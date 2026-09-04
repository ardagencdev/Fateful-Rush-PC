using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelProgressionManager : MonoBehaviour
{
    public static LevelProgressionManager Instance { get; private set; }

    [Header("Levels")]
    public LevelConfig[] levels;

    [Header("Scenes")]
    public string gameSceneName = "a";
    public string mainMenuSceneName = "MainMenu";

    private int currentLevelIndex;

    public int CurrentLevelIndex => currentLevelIndex;

    public LevelConfig CurrentLevel
    {
        get
        {
            if (!IsValidLevelIndex(currentLevelIndex))
                return null;

            return levels[currentLevelIndex];
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartLevelMode()
    {
        if (!HasValidLevels())
        {
            Debug.LogError(
                "[LevelProgressionManager] Level Mode başlatılamadı. " +
                "Levels dizisi boş veya geçerli LevelConfig içermiyor.",
                this
            );

            return;
        }

        currentLevelIndex = FindNextValidLevelIndex(0);

        if (currentLevelIndex < 0)
        {
            Debug.LogError(
                "[LevelProgressionManager] Başlatılabilecek geçerli bir level bulunamadı.",
                this
            );

            return;
        }

        LoadCurrentLevel();
    }

    public void LoadNextLevel()
    {
        if (!HasValidLevels())
        {
            Debug.LogError(
                "[LevelProgressionManager] Sonraki level yüklenemedi. " +
                "Levels dizisi boş veya geçersiz.",
                this
            );

            return;
        }

        int nextLevelIndex =
            FindNextValidLevelIndex(currentLevelIndex + 1);

        if (nextLevelIndex < 0)
        {
            CompleteLevelMode();
            return;
        }

        currentLevelIndex = nextLevelIndex;
        LoadCurrentLevel();
    }

    private void LoadCurrentLevel()
    {
        LevelConfig level = CurrentLevel;

        if (level == null)
        {
            Debug.LogError(
                $"[LevelProgressionManager] Geçersiz level indexi: " +
                $"{currentLevelIndex}",
                this
            );

            return;
        }

        if (!CanLoadScene(gameSceneName))
        {
            Debug.LogError(
                $"[LevelProgressionManager] Oyun sahnesi yüklenemiyor: " +
                $"'{gameSceneName}'. Build Profiles içindeki " +
                $"Scene List'i kontrol et.",
                this
            );

            return;
        }

        SelectedLevelData.SetMission(level);

        Debug.Log(
            $"[LevelProgressionManager] Level yükleniyor: " +
            $"{level.levelNumber} - {level.levelName} | " +
            $"Index: {currentLevelIndex}",
            this
        );

        LoadSceneWithTransition(gameSceneName);
    }

    private void CompleteLevelMode()
    {
        Debug.Log(
            "[LevelProgressionManager] Bütün levellar tamamlandı.",
            this
        );

        if (!CanLoadScene(mainMenuSceneName))
        {
            Debug.LogError(
                $"[LevelProgressionManager] Ana menü sahnesi yüklenemiyor: " +
                $"'{mainMenuSceneName}'. Build Profiles içindeki " +
                $"Scene List'i kontrol et.",
                this
            );

            return;
        }

        LoadSceneWithTransition(mainMenuSceneName);
    }

    private void LoadSceneWithTransition(string sceneName)
    {
        Time.timeScale = 1f;

        if (SceneTransition.Instance != null)
        {
            SceneTransition.Instance.LoadSceneWithFade(sceneName);
            return;
        }

        Debug.LogWarning(
            $"[LevelProgressionManager] SceneTransition bulunamadı. " +
            $"'{sceneName}' sahnesi doğrudan yükleniyor.",
            this
        );

        SceneManager.LoadScene(sceneName);
    }

    private bool HasValidLevels()
    {
        if (levels == null || levels.Length == 0)
            return false;

        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i] != null)
                return true;
        }

        return false;
    }

    private bool IsValidLevelIndex(int index)
    {
        return levels != null &&
               index >= 0 &&
               index < levels.Length &&
               levels[index] != null;
    }

    private int FindNextValidLevelIndex(int startIndex)
    {
        if (levels == null)
            return -1;

        int safeStartIndex = Mathf.Max(0, startIndex);

        for (int i = safeStartIndex; i < levels.Length; i++)
        {
            if (levels[i] != null)
                return i;
        }

        return -1;
    }

    private bool CanLoadScene(string sceneName)
    {
        return !string.IsNullOrWhiteSpace(sceneName) &&
               Application.CanStreamedLevelBeLoaded(sceneName);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(gameSceneName))
        {
            Debug.LogWarning(
                "[LevelProgressionManager] Game Scene Name boş.",
                this
            );
        }

        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            Debug.LogWarning(
                "[LevelProgressionManager] Main Menu Scene Name boş.",
                this
            );
        }
    }
#endif
}