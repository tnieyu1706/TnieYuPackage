# RecordLogging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a RecordLogging system that hooks into Unity's `Debug.Log` and records logs to file during play.

**Architecture:** ScriptableObject config (RecordLoggingConfig) + static service (RecordLoggingService) + auto-init on play (RecordLoggingAutoInit) + EditorWindow for control (RecordLoggingWindow). Uses ConcurrentQueue for thread-safe log buffering and batch file writes.

**Tech Stack:** Unity 2021.3+, C#, .NET Standard 2.0

---

### Task 1: RecordLoggingConfig — ScriptableObject Configuration

**Files:**
- Create: `Runtime/Utils/RecordLogging/RecordLoggingConfig.cs`

- [ ] **Write RecordLoggingConfig.cs**

```csharp
using UnityEngine;

namespace TnieYuPackage.Utils
{
    [CreateAssetMenu(fileName = "RecordLoggingConfig", menuName = "TnieYu/Record Logging Config")]
    public class RecordLoggingConfig : ScriptableObject
    {
        public bool isEnabled = true;
        public string fileName = "log.txt";
        public int queueSize = 1000;
        public bool appendMode = true;
        public bool snapshotMode = false;
        public LogTypeFlags logFilter = LogTypeFlags.Log | LogTypeFlags.Warning | LogTypeFlags.Error | LogTypeFlags.Exception | LogTypeFlags.Assert;

        private static RecordLoggingConfig _instance;
        public static RecordLoggingConfig Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<RecordLoggingConfig>("RecordLoggingConfig");
                return _instance;
            }
        }
    }

    [System.Flags]
    public enum LogTypeFlags
    {
        Log = 1 << 0,
        Warning = 1 << 1,
        Error = 1 << 2,
        Exception = 1 << 3,
        Assert = 1 << 4,
    }
}
```

- [ ] **Commit**

```bash
git add Runtime/Utils/RecordLogging/RecordLoggingConfig.cs
git commit -m "feat: add RecordLoggingConfig ScriptableObject"
```

---

### Task 2: RecordLoggingService — Core Logging Engine

**Files:**
- Create: `Runtime/Utils/RecordLogging/RecordLoggingService.cs`

- [ ] **Write RecordLoggingService.cs**

```csharp
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace TnieYuPackage.Utils
{
    public static class RecordLoggingService
    {
        private static RecordLoggingConfig _config;
        private static StreamWriter _writer;
        private static ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
        private static StringBuilder _sb = new StringBuilder();
        private static readonly object _lock = new object();
        private static string _logDirectory;
        private static string _currentFilePath;
        private static bool _isInitialized;

        public static bool IsInitialized => _isInitialized;
        public static int QueueCount => _queue.Count;
        public static string CurrentFilePath => _currentFilePath;

        public static void Initialize(RecordLoggingConfig config)
        {
            if (_isInitialized) return;
            if (config == null) return;

            _config = config;
            _logDirectory = Path.Combine(Application.persistentDataPath, "RecordLogging");

            if (_config.snapshotMode)
            {
                string snapshotsDir = Path.Combine(_logDirectory, "Snapshots");
                Directory.CreateDirectory(snapshotsDir);
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                _currentFilePath = Path.Combine(snapshotsDir, $"log-{timestamp}.txt");
            }
            else
            {
                Directory.CreateDirectory(_logDirectory);
                _currentFilePath = Path.Combine(_logDirectory, _config.fileName);
            }

            FileMode fileMode = _config.appendMode ? FileMode.Append : FileMode.Create;
            FileStream fs = new FileStream(_currentFilePath, fileMode, FileAccess.Write, FileShare.Read);
            _writer = new StreamWriter(fs, Encoding.UTF8);

            _queue = new ConcurrentQueue<string>();
            _sb = new StringBuilder();

            Application.logMessageReceived += OnLogMessageReceived;
            Application.quitting += OnApplicationQuitting;

            _isInitialized = true;
            Debug.Log($"[RecordLogging] Initialized -> {_currentFilePath}");
        }

        public static void Shutdown()
        {
            if (!_isInitialized) return;

            Application.logMessageReceived -= OnLogMessageReceived;
            Application.quitting -= OnApplicationQuitting;

            FlushInternal();

            lock (_lock)
            {
                if (_writer != null)
                {
                    _writer.Flush();
                    _writer.Dispose();
                    _writer = null;
                }
            }

            _isInitialized = false;
        }

        private static void OnLogMessageReceived(string logString, string stackTrace, LogType type)
        {
            if (!_isInitialized) return;
            if (!_config.isEnabled) return;

            if (!LogTypeFilterMatches(type)) return;

            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string entry = $"[{timestamp}] [{type}] {logString}\n{stackTrace}";
            _queue.Enqueue(entry);

            if (_queue.Count >= _config.queueSize)
            {
                FlushInternal();
            }
        }

        private static bool LogTypeFilterMatches(LogType type)
        {
            switch (type)
            {
                case LogType.Log: return (_config.logFilter & LogTypeFlags.Log) != 0;
                case LogType.Warning: return (_config.logFilter & LogTypeFlags.Warning) != 0;
                case LogType.Error: return (_config.logFilter & LogTypeFlags.Error) != 0;
                case LogType.Exception: return (_config.logFilter & LogTypeFlags.Exception) != 0;
                case LogType.Assert: return (_config.logFilter & LogTypeFlags.Assert) != 0;
                default: return false;
            }
        }

        public static void Flush()
        {
            FlushInternal();
        }

        private static void FlushInternal()
        {
            if (!_isInitialized) return;

            lock (_lock)
            {
                if (_writer == null) return;

                _sb.Clear();
                while (_queue.TryDequeue(out string entry))
                {
                    _sb.AppendLine(entry);
                }

                if (_sb.Length > 0)
                {
                    _writer.Write(_sb.ToString());
                    _writer.Flush();
                }
            }
        }

        private static void OnApplicationQuitting()
        {
            Shutdown();
        }

        public static void ClearLogs()
        {
            Shutdown();

            if (Directory.Exists(_logDirectory))
            {
                Directory.Delete(_logDirectory, true);
            }

            _currentFilePath = null;
        }

        public static string GetLogDirectory()
        {
            return _logDirectory;
        }
    }
}
```

- [ ] **Commit**

```bash
git add Runtime/Utils/RecordLogging/RecordLoggingService.cs
git commit -m "feat: add RecordLoggingService with queue and file writing"
```

---

### Task 3: RecordLoggingAutoInit — Auto-Initialize on Play

**Files:**
- Create: `Runtime/Utils/RecordLogging/RecordLoggingAutoInit.cs`

- [ ] **Write RecordLoggingAutoInit.cs**

```csharp
using UnityEngine;

namespace TnieYuPackage.Utils
{
    public static class RecordLoggingAutoInit
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInit()
        {
            var config = RecordLoggingConfig.Instance;
            if (config != null && config.isEnabled)
            {
                RecordLoggingService.Initialize(config);
            }
        }
    }
}
```

- [ ] **Commit**

```bash
git add Runtime/Utils/RecordLogging/RecordLoggingAutoInit.cs
git commit -m "feat: add RecordLoggingAutoInit for play start"
```

---

### Task 4: RecordLoggingWindow — Editor Window

**Files:**
- Create: `Editor/Utils/RecordLogging/RecordLoggingWindow.cs`

- [ ] **Write RecordLoggingWindow.cs**

```csharp
using UnityEditor;
using UnityEngine;

namespace TnieYuPackage.Utils
{
    public class RecordLoggingWindow : EditorWindow
    {
        private RecordLoggingConfig _config;
        private Vector2 _scrollPos;
        private int _lastLogCount;

        [MenuItem("Tools/TnieYu/Record Logging")]
        public static void Open()
        {
            var window = GetWindow<RecordLoggingWindow>("Record Logging");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnEnable()
        {
            _config = RecordLoggingConfig.Instance;
            if (_config == null)
            {
                string[] guids = AssetDatabase.FindAssets("RecordLoggingConfig t:ScriptableObject");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _config = AssetDatabase.LoadAssetAtPath<RecordLoggingConfig>(path);
                }
            }

            if (_config == null)
            {
                _config = ScriptableObject.CreateInstance<RecordLoggingConfig>();
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                AssetDatabase.CreateAsset(_config, "Assets/Resources/RecordLoggingConfig.asset");
                AssetDatabase.SaveAssets();
            }

            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (RecordLoggingService.IsInitialized)
            {
                int currentCount = RecordLoggingService.QueueCount;
                if (currentCount != _lastLogCount)
                {
                    _lastLogCount = currentCount;
                    Repaint();
                }
            }
        }

        private void OnGUI()
        {
            if (_config == null)
            {
                EditorGUILayout.HelpBox("Config not found. Please create a RecordLoggingConfig.", MessageType.Warning);
                return;
            }

            bool isPlaying = EditorApplication.isPlaying;
            bool serviceActive = RecordLoggingService.IsInitialized;

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            GUILayout.Label("Record Logging Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledGroupScope(!isPlaying))
            {
                _config.isEnabled = EditorGUILayout.Toggle("Enable Logging", _config.isEnabled);
            }

            if (!isPlaying)
            {
                EditorGUILayout.HelpBox("Logging only works during Play Mode.", MessageType.Info);
            }

            EditorGUILayout.Space();
            GUILayout.Label("File Settings", EditorStyles.boldLabel);

            _config.fileName = EditorGUILayout.TextField("File Name", _config.fileName);
            _config.queueSize = EditorGUILayout.IntField("Queue Size", _config.queueSize);
            if (_config.queueSize < 1) _config.queueSize = 1;

            using (new EditorGUI.DisabledGroupScope(_config.snapshotMode))
            {
                _config.appendMode = EditorGUILayout.Toggle("Append Mode", _config.appendMode);
            }

            _config.snapshotMode = EditorGUILayout.Toggle("Snapshot Mode", _config.snapshotMode);

            EditorGUILayout.Space();
            GUILayout.Label("Log Filter", EditorStyles.boldLabel);
            _config.logFilter = (LogTypeFlags)EditorGUILayout.EnumFlagsField("Log Types", _config.logFilter);

            EditorGUILayout.Space();
            GUILayout.Label("Actions", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledGroupScope(!isPlaying))
            {
                if (GUILayout.Button("Flush Now", GUILayout.Height(30)))
                {
                    RecordLoggingService.Flush();
                }
            }

            if (GUILayout.Button("Clear All Logs", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Clear Logs",
                    "Are you sure you want to delete all log files in the RecordLogging directory?",
                    "Yes", "Cancel"))
                {
                    RecordLoggingService.ClearLogs();
                    _lastLogCount = 0;
                }
            }

            if (GUILayout.Button("Open Log Folder", GUILayout.Height(30)))
            {
                string dir = RecordLoggingService.GetLogDirectory();
                if (dir != null && System.IO.Directory.Exists(dir))
                    EditorUtility.RevealInFinder(dir);
                else
                    EditorUtility.RevealInFinder(Application.persistentDataPath);
            }

            EditorGUILayout.Space();
            GUILayout.Label("Status", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("Service Active", serviceActive ? "Yes" : "No");
            EditorGUILayout.LabelField("Queue", $"{_lastLogCount} / {_config.queueSize}");

            if (serviceActive)
            {
                EditorGUILayout.LabelField("Log File", RecordLoggingService.CurrentFilePath);
            }

            if (GUI.changed)
            {
                EditorUtility.SetDirty(_config);
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
```

- [ ] **Commit**

```bash
git add Editor/Utils/RecordLogging/RecordLoggingWindow.cs
git commit -m "feat: add RecordLoggingWindow editor tool"
```

---

### Self-Review Checklist

- **Spec coverage:** 
  - Config with filename, queue size, append/override, snapshot, log filter → Task 1 ✅
  - Hook Application.logMessageReceived → Task 2 ✅
  - Queue with configurable size, auto-flush on full → Task 2 ✅
  - Auto-flush on quit → Task 2 ✅
  - File saved to persistentDataPath → Task 2 ✅
  - Snapshot mode with timestamp per session → Task 2 ✅
  - Append/Override mode → Task 2 ✅
  - Auto-init on play → Task 3 ✅
  - EditorWindow with toggle, clear, open folder, status → Task 4 ✅
  - Disable controls when not playing → Task 4 ✅
  - Namespace TnieYuPackage.Utils → All ✅
  - Runtime files in Runtime/Utils/RecordLogging/ → All ✅
  - Editor files in Editor/Utils/RecordLogging/ → All ✅
- **Placeholder scan:** No TBD, TODO, or placeholder patterns found ✅
- **Type consistency:** LogTypeFlags, RecordLoggingConfig, RecordLoggingService names consistent across all tasks ✅
