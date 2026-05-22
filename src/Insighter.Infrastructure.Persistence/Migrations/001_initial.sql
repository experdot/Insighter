CREATE TABLE IF NOT EXISTS SchemaVersions (
    Version INTEGER NOT NULL PRIMARY KEY,
    AppliedAtUtc INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS LogEvents (
    Id TEXT NOT NULL PRIMARY KEY,
    OccurredAtUtc INTEGER NOT NULL,
    EventType TEXT NOT NULL,
    CollectorId TEXT NOT NULL,
    Severity INTEGER NOT NULL,
    UserSessionId TEXT NOT NULL,
    MachineId TEXT NOT NULL,
    Content TEXT NOT NULL CHECK (json_valid(Content))
);

CREATE TABLE IF NOT EXISTS CollectorConfigs (
    CollectorId TEXT NOT NULL PRIMARY KEY,
    Enabled INTEGER NOT NULL,
    Version INTEGER NOT NULL,
    UpdatedAtUtc INTEGER NOT NULL,
    SettingsJson TEXT NOT NULL CHECK (json_valid(SettingsJson))
);

CREATE TABLE IF NOT EXISTS CollectorStates (
    CollectorId TEXT NOT NULL PRIMARY KEY,
    Status INTEGER NOT NULL,
    LastHeartbeatUtc INTEGER NULL,
    LastError TEXT NULL,
    UpdatedAtUtc INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS AppSettings (
    Key TEXT NOT NULL PRIMARY KEY,
    Value TEXT NOT NULL,
    UpdatedAtUtc INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_LogEvents_OccurredAtUtc ON LogEvents (OccurredAtUtc DESC);
CREATE INDEX IF NOT EXISTS IX_LogEvents_EventType_OccurredAtUtc ON LogEvents (EventType, OccurredAtUtc DESC);
CREATE INDEX IF NOT EXISTS IX_LogEvents_CollectorId_OccurredAtUtc ON LogEvents (CollectorId, OccurredAtUtc DESC);
CREATE INDEX IF NOT EXISTS IX_LogEvents_UserSessionId_OccurredAtUtc ON LogEvents (UserSessionId, OccurredAtUtc DESC);
CREATE INDEX IF NOT EXISTS IX_LogEvents_MachineId_OccurredAtUtc ON LogEvents (MachineId, OccurredAtUtc DESC);
