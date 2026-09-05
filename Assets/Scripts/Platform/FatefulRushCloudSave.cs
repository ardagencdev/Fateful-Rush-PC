using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

#if UNITY_ANDROID && !UNITY_EDITOR
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using GooglePlayGames.BasicApi.SavedGame;
#endif

/// <summary>
/// Google Play Games Saved Games integration for Fateful Rush progression.
///
/// Design goals:
/// - A reinstall must be able to restore progression after PGS authentication.
/// - Local and cloud data are merged instead of blindly overwriting one another.
/// - Old save generations are never allowed to resurrect deprecated progress.
/// - Cloud writes are debounced and never happen every frame / during gameplay loops.
/// - Failure of the cloud service must never block normal gameplay.
/// </summary>
public sealed class FatefulRushCloudSave : MonoBehaviour
{
    private const string SaveFileName = "FatefulRush_Save_v1";
    private const string LocalRevisionKey = "FatefulRush_CloudRevisionUtcTicks";
    private const float UploadDebounceSeconds = 2.0f;

    private static readonly string[] MonotonicIntKeys =
    {
        "UnlockedLevel",

        "Stats_TotalRuns",
        "Stats_TotalWins",
        "Stats_TotalDeaths",
        "Stats_TotalCoins",
        "Stats_TotalCoinValue",
        "Stats_NormalCoins",
        "Stats_GoldCoins",
        "Stats_RareCoins",
        "Stats_DashUses",
        "Stats_CloneUses",
        "Stats_SlowBuffUses",
        "Stats_ArmorBuffUses",
        "Stats_ArmorKills",
        "Stats_ArmorEnemyKills",
        "Stats_SpaceBombTriggers",
        "Stats_TotalScore",
        "Stats_CompletedScoreTotal",
        "Stats_ScoreRuns",
        "Stats_BestRunScore",
        "Stats_MostCoinsInRun",
        "Stats_BestWinStreak",
        "Stats_HighestCombo",
        "Stats_LongestComboChain",
        "Stats_MaxComboReached",
        "Stats_ComboBonusScore",
        "Stats_NearMisses",
        "Stats_BestNearMissStreak",
        "Stats_MagnetCoins",
        "Stats_BeaconsDestroyed",
        "Stats_HuntersStunned",
        "Stats_BossEncounters",
        "Stats_BossSplits",
        "Stats_BossAoeEvades",
        "Stats_MiniBossAoeEvades",
        "Stats_Mode_Score_Runs",
        "Stats_Mode_Score_Wins",
        "Stats_Mode_Survival_Runs",
        "Stats_Mode_Survival_Wins",
        "Stats_Mode_TimedScore_Runs",
        "Stats_Mode_TimedScore_Wins",
        "Stats_Death_STALKER",
        "Stats_Death_HUNTER",
        "Stats_Death_BLASTER",
        "Stats_Death_LASER_BULLET",
        "Stats_Death_LASER_WALL",
        "Stats_Death_BOSS",
        "Stats_Death_MINI_BOSS",
        "Stats_Death_SPACE_BOMB",
        "Stats_Death_TIME_EXPIRED",
        "Stats_Death_UNKNOWN"
    };

    private static readonly string[] LatestIntKeys =
    {
        "Stats_CurrentWinStreak",
        "FatefulRush_SignalStable"
    };

    private static readonly string[] MaxFloatKeys =
    {
        "Stats_TotalPlayTime",
        "Stats_LongestRunTime"
    };

    private static readonly string[] LatestStringKeys =
    {
        PlayerSkinCatalog.SelectedSkinKey
    };

    private static FatefulRushCloudSave instance;

    private bool initialSyncCompleted;

#if UNITY_ANDROID && !UNITY_EDITOR
    private bool initialSyncRequested;
    private bool operationInFlight;
    private bool uploadRequested;
    private float uploadNotBeforeRealtime;
    private Action pendingInitialSyncCallback;
#endif

    public static bool InitialSyncCompleted =>
        instance != null && instance.initialSyncCompleted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private static FatefulRushCloudSave EnsureInstance()
    {
        if (instance != null)
            return instance;

        FatefulRushCloudSave existing =
            FindAnyObjectByType<FatefulRushCloudSave>();

        if (existing != null)
        {
            instance = existing;
            return instance;
        }

        GameObject root = new GameObject("FatefulRushCloudSave");
        instance = root.AddComponent<FatefulRushCloudSave>();
        DontDestroyOnLoad(root);
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        TryStartInitialSyncIfSafe();

        if (!uploadRequested ||
            operationInFlight ||
            !initialSyncCompleted ||
            Time.realtimeSinceStartup < uploadNotBeforeRealtime ||
            !IsSafeForCloudWork())
        {
            return;
        }

        if (!IsPlatformAuthenticated())
            return;

        uploadRequested = false;
        UploadCurrentSaveInternal();
#endif
    }

    /// <summary>
    /// Called immediately after a successful PGS authentication.
    /// The callback is always invoked, even if cloud save fails, so achievements/UI
    /// can continue to work without being coupled to Saved Games availability.
    /// </summary>
    public static void SyncAfterAuthentication(Action onFinished)
    {
        EnsureInstance().SyncAfterAuthenticationInternal(onFinished);
    }

    /// <summary>
    /// Marks the current local progression as changed and schedules one cloud write.
    /// Multiple calls close together collapse into a single upload.
    /// </summary>
    public static void RequestUpload()
    {
        TouchLocalRevision();

#if UNITY_ANDROID && !UNITY_EDITOR
        FatefulRushCloudSave manager = EnsureInstance();
        manager.uploadRequested = true;
        manager.uploadNotBeforeRealtime =
            Time.realtimeSinceStartup + UploadDebounceSeconds;
#else
        // Cloud upload is Android-only; keep the method harmless in Editor/other builds.
        EnsureInstance();
#endif
    }

    private void SyncAfterAuthenticationInternal(Action onFinished)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (initialSyncCompleted)
        {
            InvokeSafely(onFinished);
            return;
        }

        pendingInitialSyncCallback += onFinished;
        initialSyncRequested = true;
        TryStartInitialSyncIfSafe();
#else
        initialSyncCompleted = true;
        InvokeSafely(onFinished);
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void TryStartInitialSyncIfSafe()
    {
        if (!initialSyncRequested ||
            initialSyncCompleted ||
            operationInFlight ||
            !IsPlatformAuthenticated() ||
            !IsSafeForCloudWork())
        {
            return;
        }

        initialSyncRequested = false;
        operationInFlight = true;

        try
        {
            PlayGamesPlatform.Instance.SavedGame
                .OpenWithAutomaticConflictResolution(
                    SaveFileName,
                    DataSource.ReadCacheOrNetwork,
                    ConflictResolutionStrategy.UseLongestPlaytime,
                    HandleInitialSaveOpened
                );
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[CloudSave] Saved Game acilamadi; local save kullanilacak: " +
                exception.Message
            );

            CompleteInitialSync(false);
        }
    }

    private static bool IsSafeForCloudWork()
    {
        // Initial restore / upload may allocate JSON and invoke Play Games SDK
        // callbacks. Never deliberately start that work while a run is active.
        return !GameStateManager.IsGameplayStarted;
    }

    private void HandleInitialSaveOpened(
        SavedGameRequestStatus status,
        ISavedGameMetadata metadata)
    {
        if (status != SavedGameRequestStatus.Success || metadata == null)
        {
            Debug.LogWarning(
                "[CloudSave] Saved Game open basarisiz: " + status
            );

            CompleteInitialSync(false);
            return;
        }

        try
        {
            PlayGamesPlatform.Instance.SavedGame.ReadBinaryData(
                metadata,
                (readStatus, data) =>
                    HandleInitialSaveRead(
                        metadata,
                        readStatus,
                        data
                    )
            );
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[CloudSave] Cloud veri okunamadi: " + exception.Message
            );

            CompleteInitialSync(false);
        }
    }

    private void HandleInitialSaveRead(
        ISavedGameMetadata metadata,
        SavedGameRequestStatus status,
        byte[] data)
    {
        if (status != SavedGameRequestStatus.Success)
        {
            Debug.LogWarning(
                "[CloudSave] Cloud read basarisiz; local save korunuyor: " +
                status
            );

            CompleteInitialSync(false);
            return;
        }

        CloudSnapshot local = BuildLocalSnapshot();
        CloudSnapshot cloud =
            data != null && data.Length > 0
                ? Deserialize(data)
                : null;

        CloudSnapshot merged = MergeSnapshots(local, cloud);
        ApplySnapshotToLocal(merged);

        // The local copy is already usable at this point. We still commit the
        // merged result so both devices converge on the same canonical state.
        CommitSnapshot(
            metadata,
            merged,
            commitStatus =>
            {
                if (commitStatus != SavedGameRequestStatus.Success)
                {
                    Debug.LogWarning(
                        "[CloudSave] Ilk merge cloud'a yazilamadi: " +
                        commitStatus
                    );
                }

                CompleteInitialSync(true);
            }
        );
    }

    private void UploadCurrentSaveInternal()
    {
        if (operationInFlight || !IsPlatformAuthenticated())
            return;

        operationInFlight = true;

        try
        {
            PlayGamesPlatform.Instance.SavedGame
                .OpenWithAutomaticConflictResolution(
                    SaveFileName,
                    DataSource.ReadCacheOrNetwork,
                    ConflictResolutionStrategy.UseLongestPlaytime,
                    HandleUploadSaveOpened
                );
        }
        catch (Exception exception)
        {
            operationInFlight = false;
            uploadRequested = true;
            uploadNotBeforeRealtime = Time.realtimeSinceStartup + 10f;

            Debug.LogWarning(
                "[CloudSave] Upload icin Saved Game acilamadi: " +
                exception.Message
            );
        }
    }

    private void HandleUploadSaveOpened(
        SavedGameRequestStatus status,
        ISavedGameMetadata metadata)
    {
        if (status != SavedGameRequestStatus.Success || metadata == null)
        {
            operationInFlight = false;
            uploadRequested = true;
            uploadNotBeforeRealtime = Time.realtimeSinceStartup + 10f;

            Debug.LogWarning(
                "[CloudSave] Upload open basarisiz: " + status
            );

            return;
        }

        try
        {
            PlayGamesPlatform.Instance.SavedGame.ReadBinaryData(
                metadata,
                (readStatus, data) =>
                    HandleUploadSaveRead(
                        metadata,
                        readStatus,
                        data
                    )
            );
        }
        catch (Exception exception)
        {
            operationInFlight = false;
            uploadRequested = true;
            uploadNotBeforeRealtime = Time.realtimeSinceStartup + 10f;

            Debug.LogWarning(
                "[CloudSave] Upload oncesi cloud read basarisiz: " +
                exception.Message
            );
        }
    }

    private void HandleUploadSaveRead(
        ISavedGameMetadata metadata,
        SavedGameRequestStatus status,
        byte[] data)
    {
        if (status != SavedGameRequestStatus.Success)
        {
            operationInFlight = false;
            uploadRequested = true;
            uploadNotBeforeRealtime = Time.realtimeSinceStartup + 10f;

            Debug.LogWarning(
                "[CloudSave] Upload oncesi cloud read basarisiz: " +
                status
            );

            return;
        }

        CloudSnapshot local = BuildLocalSnapshot();
        CloudSnapshot cloud =
            data != null && data.Length > 0
                ? Deserialize(data)
                : null;

        CloudSnapshot merged = MergeSnapshots(local, cloud);
        ApplySnapshotToLocal(merged);

        CommitSnapshot(
            metadata,
            merged,
            commitStatus =>
            {
                operationInFlight = false;

                if (commitStatus != SavedGameRequestStatus.Success)
                {
                    uploadRequested = true;
                    uploadNotBeforeRealtime =
                        Time.realtimeSinceStartup + 10f;

                    Debug.LogWarning(
                        "[CloudSave] Upload commit basarisiz: " +
                        commitStatus
                    );
                }
            }
        );
    }

    private void CommitSnapshot(
        ISavedGameMetadata metadata,
        CloudSnapshot snapshot,
        Action<SavedGameRequestStatus> onFinished)
    {
        if (metadata == null)
        {
            operationInFlight = false;
            InvokeSafely(
                onFinished,
                SavedGameRequestStatus.BadInputError
            );
            return;
        }

        snapshot.saveGeneration = ReleaseSaveMigration.CurrentSaveGeneration;
        snapshot.revisionUtcTicks = DateTime.UtcNow.Ticks;

        SetLocalRevision(snapshot.revisionUtcTicks);

        byte[] bytes = Serialize(snapshot);

        float totalPlayTime = GetFloat(
            snapshot,
            "Stats_TotalPlayTime",
            0f
        );

        SavedGameMetadataUpdate metadataUpdate =
            new SavedGameMetadataUpdate.Builder()
                .WithUpdatedDescription(
                    "Fateful Rush progression - " +
                    DateTime.UtcNow.ToString("u")
                )
                .WithUpdatedPlayedTime(
                    TimeSpan.FromSeconds(
                        Math.Max(0d, totalPlayTime)
                    )
                )
                .Build();

        try
        {
            PlayGamesPlatform.Instance.SavedGame.CommitUpdate(
                metadata,
                metadataUpdate,
                bytes,
                (status, committedMetadata) =>
                    InvokeSafely(onFinished, status)
            );
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[CloudSave] CommitUpdate exception: " + exception.Message
            );

            InvokeSafely(
                onFinished,
                SavedGameRequestStatus.InternalError
            );
        }
    }

    private static bool IsPlatformAuthenticated()
    {
        try
        {
            return PlayGamesPlatform.Instance != null &&
                   PlayGamesPlatform.Instance.IsAuthenticated();
        }
        catch
        {
            return false;
        }
    }
    private void CompleteInitialSync(bool cloudWasReachable)
    {
        operationInFlight = false;
        initialSyncRequested = false;
        initialSyncCompleted = true;

        Action callback = pendingInitialSyncCallback;
        pendingInitialSyncCallback = null;

        InvokeSafely(callback);

#if DEVELOPMENT_BUILD
        Debug.Log(
            "[CloudSave] Initial sync complete. CloudReachable=" +
            cloudWasReachable
        );
#endif
    }
#endif

    private static CloudSnapshot BuildLocalSnapshot()
    {
        CloudSnapshot snapshot = new CloudSnapshot
        {
            schemaVersion = 1,
            saveGeneration = ReleaseSaveMigration.CurrentSaveGeneration,
            revisionUtcTicks = GetLocalRevision()
        };

        AddInt(snapshot, "UnlockedLevel", PlayerPrefs.GetInt("UnlockedLevel", 1));

        for (int level = StatsManager.FirstLevelNumber;
             level <= StatsManager.LastLevelNumber;
             level++)
        {
            string completedKey = "CompletedLevel_" + level;
            AddInt(
                snapshot,
                completedKey,
                PlayerPrefs.GetInt(completedKey, 0)
            );

            string bestTimeKey = "BestTime_Level_" + level;
            AddFloat(
                snapshot,
                bestTimeKey,
                PlayerPrefs.GetFloat(bestTimeKey, -1f)
            );
        }

        AddFloat(
            snapshot,
            "BestTime_DevRoom",
            PlayerPrefs.GetFloat("BestTime_DevRoom", -1f)
        );

        for (int i = 0; i < MonotonicIntKeys.Length; i++)
        {
            string key = MonotonicIntKeys[i];

            if (key == "UnlockedLevel")
                continue;

            AddInt(
                snapshot,
                key,
                PlayerPrefs.GetInt(key, 0)
            );
        }

        for (int i = 0; i < LatestIntKeys.Length; i++)
        {
            string key = LatestIntKeys[i];
            AddInt(
                snapshot,
                key,
                PlayerPrefs.GetInt(key, 0)
            );
        }

        for (int i = 0; i < MaxFloatKeys.Length; i++)
        {
            string key = MaxFloatKeys[i];
            AddFloat(
                snapshot,
                key,
                PlayerPrefs.GetFloat(key, 0f)
            );
        }

        for (int i = 0; i < LatestStringKeys.Length; i++)
        {
            string key = LatestStringKeys[i];
            AddString(
                snapshot,
                key,
                PlayerPrefs.GetString(key, string.Empty)
            );
        }

        return snapshot;
    }

    private static CloudSnapshot MergeSnapshots(
        CloudSnapshot local,
        CloudSnapshot cloud)
    {
        if (local == null)
            local = new CloudSnapshot();

        if (!IsCloudSnapshotCompatible(cloud))
        {
            CloudSnapshot localOnly = CloneSnapshot(local);
            localOnly.saveGeneration = ReleaseSaveMigration.CurrentSaveGeneration;

            if (localOnly.revisionUtcTicks <= 0)
                localOnly.revisionUtcTicks = DateTime.UtcNow.Ticks;

            return localOnly;
        }

        CloudSnapshot merged = new CloudSnapshot
        {
            schemaVersion = Math.Max(local.schemaVersion, cloud.schemaVersion),
            saveGeneration = ReleaseSaveMigration.CurrentSaveGeneration,
            revisionUtcTicks = Math.Max(
                local.revisionUtcTicks,
                cloud.revisionUtcTicks
            )
        };

        // Mission progression can only move forward.
        AddInt(
            merged,
            "UnlockedLevel",
            Math.Max(
                GetInt(local, "UnlockedLevel", 1),
                GetInt(cloud, "UnlockedLevel", 1)
            )
        );

        for (int level = StatsManager.FirstLevelNumber;
             level <= StatsManager.LastLevelNumber;
             level++)
        {
            string completedKey = "CompletedLevel_" + level;
            AddInt(
                merged,
                completedKey,
                Math.Max(
                    GetInt(local, completedKey, 0),
                    GetInt(cloud, completedKey, 0)
                )
            );

            string bestTimeKey = "BestTime_Level_" + level;
            AddFloat(
                merged,
                bestTimeKey,
                PickBestPositiveTime(
                    GetFloat(local, bestTimeKey, -1f),
                    GetFloat(cloud, bestTimeKey, -1f)
                )
            );
        }

        AddFloat(
            merged,
            "BestTime_DevRoom",
            PickBestPositiveTime(
                GetFloat(local, "BestTime_DevRoom", -1f),
                GetFloat(cloud, "BestTime_DevRoom", -1f)
            )
        );

        for (int i = 0; i < MonotonicIntKeys.Length; i++)
        {
            string key = MonotonicIntKeys[i];

            if (key == "UnlockedLevel")
                continue;

            AddInt(
                merged,
                key,
                Math.Max(
                    GetInt(local, key, 0),
                    GetInt(cloud, key, 0)
                )
            );
        }

        bool cloudIsNewer =
            cloud.revisionUtcTicks > local.revisionUtcTicks;

        CloudSnapshot latest = cloudIsNewer ? cloud : local;

        for (int i = 0; i < LatestIntKeys.Length; i++)
        {
            string key = LatestIntKeys[i];
            AddInt(
                merged,
                key,
                GetInt(latest, key, 0)
            );
        }

        for (int i = 0; i < MaxFloatKeys.Length; i++)
        {
            string key = MaxFloatKeys[i];
            AddFloat(
                merged,
                key,
                Math.Max(
                    GetFloat(local, key, 0f),
                    GetFloat(cloud, key, 0f)
                )
            );
        }

        for (int i = 0; i < LatestStringKeys.Length; i++)
        {
            string key = LatestStringKeys[i];
            string value = GetString(latest, key, string.Empty);

            // If the newer side has no selected skin at all (typical clean
            // reinstall before restore), keep the older non-empty selection.
            if (string.IsNullOrWhiteSpace(value))
            {
                CloudSnapshot older = cloudIsNewer ? local : cloud;
                value = GetString(older, key, string.Empty);
            }

            AddString(merged, key, value);
        }

        return merged;
    }

    private static void ApplySnapshotToLocal(CloudSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        PlayerPrefs.SetInt(
            "FatefulRush_SaveGeneration",
            ReleaseSaveMigration.CurrentSaveGeneration
        );

        for (int i = 0; i < snapshot.ints.Count; i++)
        {
            IntEntry entry = snapshot.ints[i];

            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                continue;

            PlayerPrefs.SetInt(entry.key, entry.value);
        }

        for (int i = 0; i < snapshot.floats.Count; i++)
        {
            FloatEntry entry = snapshot.floats[i];

            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                continue;

            if (entry.key.StartsWith("BestTime_", StringComparison.Ordinal) &&
                entry.value <= 0f)
            {
                PlayerPrefs.DeleteKey(entry.key);
                continue;
            }

            PlayerPrefs.SetFloat(entry.key, entry.value);
        }

        for (int i = 0; i < snapshot.strings.Count; i++)
        {
            StringEntry entry = snapshot.strings[i];

            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                continue;

            if (string.IsNullOrWhiteSpace(entry.value))
                PlayerPrefs.DeleteKey(entry.key);
            else
                PlayerPrefs.SetString(entry.key, entry.value);
        }

        SetLocalRevision(snapshot.revisionUtcTicks);

        // Avoid forcing disk IO if an asynchronous SDK callback happens after
        // the player has already entered a run. The in-memory values are valid
        // immediately and the next normal checkpoint will persist them.
        if (!GameStateManager.IsGameplayStarted)
            PlayerPrefs.Save();
    }

    private static bool IsCloudSnapshotCompatible(CloudSnapshot snapshot)
    {
        return snapshot != null &&
               snapshot.schemaVersion > 0 &&
               snapshot.saveGeneration ==
               ReleaseSaveMigration.CurrentSaveGeneration;
    }

    private static float PickBestPositiveTime(float a, float b)
    {
        bool aValid = a > 0f && !float.IsNaN(a) && !float.IsInfinity(a);
        bool bValid = b > 0f && !float.IsNaN(b) && !float.IsInfinity(b);

        if (aValid && bValid)
            return Math.Min(a, b);

        if (aValid)
            return a;

        if (bValid)
            return b;

        return -1f;
    }

    private static void TouchLocalRevision()
    {
        SetLocalRevision(DateTime.UtcNow.Ticks);
    }

    private static long GetLocalRevision()
    {
        string raw = PlayerPrefs.GetString(LocalRevisionKey, "0");

        long revision;
        return long.TryParse(raw, out revision)
            ? Math.Max(0L, revision)
            : 0L;
    }

    private static void SetLocalRevision(long revision)
    {
        PlayerPrefs.SetString(
            LocalRevisionKey,
            Math.Max(0L, revision).ToString()
        );
    }

    private static byte[] Serialize(CloudSnapshot snapshot)
    {
        string json = JsonUtility.ToJson(snapshot);
        return Encoding.UTF8.GetBytes(json);
    }

    private static CloudSnapshot Deserialize(byte[] bytes)
    {
        try
        {
            string json = Encoding.UTF8.GetString(bytes);

            if (string.IsNullOrWhiteSpace(json))
                return null;

            CloudSnapshot snapshot =
                JsonUtility.FromJson<CloudSnapshot>(json);

            snapshot?.EnsureLists();
            return snapshot;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[CloudSave] Cloud JSON okunamadi; local save korunuyor: " +
                exception.Message
            );

            return null;
        }
    }

    private static CloudSnapshot CloneSnapshot(CloudSnapshot source)
    {
        if (source == null)
            return new CloudSnapshot();

        return Deserialize(Serialize(source)) ?? new CloudSnapshot();
    }

    private static void AddInt(CloudSnapshot snapshot, string key, int value)
    {
        snapshot.EnsureLists();
        snapshot.ints.Add(new IntEntry { key = key, value = value });
    }

    private static void AddFloat(CloudSnapshot snapshot, string key, float value)
    {
        snapshot.EnsureLists();
        snapshot.floats.Add(new FloatEntry { key = key, value = value });
    }

    private static void AddString(CloudSnapshot snapshot, string key, string value)
    {
        snapshot.EnsureLists();
        snapshot.strings.Add(
            new StringEntry
            {
                key = key,
                value = value ?? string.Empty
            }
        );
    }

    private static int GetInt(CloudSnapshot snapshot, string key, int fallback)
    {
        if (snapshot == null || snapshot.ints == null)
            return fallback;

        for (int i = 0; i < snapshot.ints.Count; i++)
        {
            IntEntry entry = snapshot.ints[i];

            if (entry != null && entry.key == key)
                return entry.value;
        }

        return fallback;
    }

    private static float GetFloat(
        CloudSnapshot snapshot,
        string key,
        float fallback)
    {
        if (snapshot == null || snapshot.floats == null)
            return fallback;

        for (int i = 0; i < snapshot.floats.Count; i++)
        {
            FloatEntry entry = snapshot.floats[i];

            if (entry != null && entry.key == key)
                return entry.value;
        }

        return fallback;
    }

    private static string GetString(
        CloudSnapshot snapshot,
        string key,
        string fallback)
    {
        if (snapshot == null || snapshot.strings == null)
            return fallback;

        for (int i = 0; i < snapshot.strings.Count; i++)
        {
            StringEntry entry = snapshot.strings[i];

            if (entry != null && entry.key == key)
                return entry.value ?? fallback;
        }

        return fallback;
    }

    private static void InvokeSafely(Action callback)
    {
        if (callback == null)
            return;

        try
        {
            callback();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static void InvokeSafely(
        Action<SavedGameRequestStatus> callback,
        SavedGameRequestStatus status)
    {
        if (callback == null)
            return;

        try
        {
            callback(status);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }
#endif

    [Serializable]
    private sealed class CloudSnapshot
    {
        public int schemaVersion = 1;
        public int saveGeneration;
        public long revisionUtcTicks;
        public List<IntEntry> ints = new List<IntEntry>();
        public List<FloatEntry> floats = new List<FloatEntry>();
        public List<StringEntry> strings = new List<StringEntry>();

        public void EnsureLists()
        {
            if (ints == null)
                ints = new List<IntEntry>();

            if (floats == null)
                floats = new List<FloatEntry>();

            if (strings == null)
                strings = new List<StringEntry>();
        }
    }

    [Serializable]
    private sealed class IntEntry
    {
        public string key;
        public int value;
    }

    [Serializable]
    private sealed class FloatEntry
    {
        public string key;
        public float value;
    }

    [Serializable]
    private sealed class StringEntry
    {
        public string key;
        public string value;
    }
}
