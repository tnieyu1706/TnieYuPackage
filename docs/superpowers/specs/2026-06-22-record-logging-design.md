# RecordLogging — Design Specification

## Overview
Hệ thống RecordLogging cho phép hook vào `Debug.Log` của Unity để ghi lại log vào file trong lúc play (PlayMode Editor & runtime build). Hỗ trợ bật/tắt, snapshot theo session, và cấu hình queue để cân bằng memory.

## Principles
- Chỉ hoạt động **khi play** — PlayMode trong Editor và runtime sau build
- Snapshot ON → mỗi session play tạo file riêng kèm timestamp
- Snapshot OFF → dùng 1 file cố định (append/override)
- Queue-based: ghi batch khi đầy, tránh I/O quá thường xuyên

## Components

### 1. RecordLoggingConfig (Runtime)
- **Path**: `Runtime/Utils/RecordLogging/RecordLoggingConfig.cs`
- **Type**: ScriptableObject
- **Auto-create tại**: `Assets/Resources/RecordLoggingConfig.asset`
- **Fields**:
  - `bool isEnabled` — master toggle
  - `string fileName` — default `"log.txt"`
  - `int queueSize` — default 1000
  - `bool appendMode` — chỉ dùng khi snapshot OFF
  - `bool snapshotMode` — mỗi session → file riêng có timestamp
  - `LogType logFilter` — kiểu log được record (bitmask)
- **Singleton**: `Instance` property auto-load từ Resources

### 2. RecordLoggingService (Runtime)
- **Path**: `Runtime/Utils/RecordLogging/RecordLoggingService.cs`
- **Type**: Static singleton
- **Hook**: `Application.logMessageReceived` — chỉ active khi play
- **Queue**: `ConcurrentQueue<string>` với capacity = queueSize
- **Entry format**: `[HH:mm:ss.fff] [LogType] message\n<stackTrace>`
- **Flush trigger**:
  - Queue đầy (`Count >= queueSize`) → ghi batch vào file, clear queue
  - `Application.quitting` → flush lần cuối, đóng writer
- **File path**:
  - Snapshot ON: `{persistentDataPath}/RecordLogging/Snapshots/log-{yyyy-MM-dd_HH-mm-ss}.txt`
  - Snapshot OFF: `{persistentDataPath}/RecordLogging/{fileName}`
- **Append/Override**:
  - Append: mở file với `FileMode.Append`
  - Override: mở file với `FileMode.Create`
- **Methods**:
  - `Initialize(RecordLoggingConfig config)` — register callback, mở file
  - `Shutdown()` — flush + close writer + unregister callback
  - `Flush()` — ghi queue vào file
  - `ClearLogs()` — xóa toàn bộ file trong thư mục RecordLogging
  - `GetLogCount()` — số log hiện tại trong queue

### 3. RecordLoggingAutoInit (Runtime)
- **Path**: `Runtime/Utils/RecordLogging/RecordLoggingAutoInit.cs`
- **Type**: Static class với `[RuntimeInitializeOnLoadMethod]`
- **Method**: `AutoInit()` chạy ở `BeforeSceneLoad`
- **Logic**: Load config từ Resources, nếu `isEnabled` thì gọi `RecordLoggingService.Initialize(config)`
- **Shutdown**: Hook `Application.quitting` để flush lần cuối

### 4. RecordLoggingWindow (Editor)
- **Path**: `Editor/Utils/RecordLogging/RecordLoggingWindow.cs`
- **Type**: `EditorWindow`
- **Menu**: `Tools/TnieYu/Record Logging`
- **Controls**:
  - `isEnabled` toggle — bật/tắt record
  - `fileName` text field
  - `queueSize` int field
  - `appendMode` toggle — disable khi snapshot ON
  - `snapshotMode` toggle
  - `logFilter` enum flags dropdown
  - **Nút Clear Logs** — xóa tất cả file log, confirm dialog
  - **Nút Open Folder** — `Application.persistentDataPath/RecordLogging/`
  - **Status bar** — số log hiện tại / queue size
- **Update**: EditorApplication.update poll để refresh log count real-time
- **Chỉ active khi play**: disable controls khi Editor không ở PlayMode

## File Structure
```
Runtime/Utils/RecordLogging/
    RecordLoggingConfig.cs
    RecordLoggingService.cs
    RecordLoggingAutoInit.cs

Editor/Utils/RecordLogging/
    RecordLoggingWindow.cs
```

## Data Flow
```
Debug.Log("msg")
  → Application.logMessageReceived
  → RecordLoggingService.HandleLog(logString, stackTrace, type)
  → Format entry + Enqueue
  → queue.Count >= queueSize → Flush() ghi batch vào file
  → Application.quitting → Flush() + close writer

Snapshot ON → persistentDataPath/RecordLogging/Snapshots/log-{timestamp}.txt
Snapshot OFF → persistentDataPath/RecordLogging/{fileName} (append/override)
```

## Thread Safety
- `ConcurrentQueue<string>` cho enqueue từ bất kỳ thread nào
- Flush chạy trên main thread (gọi từ Update hoặc quitting callback)
- Lock file write bằng `lock` statement

## Assumptions
- Unity 2021.3+ (dựa trên package.json)
- .NET Standard 2.0 compatible
- Runtime assembly không cần thay đổi (thêm file vào là đủ)
- Editor assembly đã reference runtime assembly
