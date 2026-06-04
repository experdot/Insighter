namespace Insighter.Domain;

public enum CollectorStatus
{
    Disabled = 0,
    Starting = 1,
    Running = 2,
    Stopping = 3,
    Faulted = 4,
    Unsupported = 5,
}

public enum CapabilitySupportLevel
{
    Supported = 0,
    PartiallySupported = 1,
    Unsupported = 2,
}

public enum EventSeverity
{
    Information = 0,
    Warning = 1,
    Error = 2,
}

public enum ClipboardCaptureMode
{
    Off = 0,
    MetadataOnly = 1,
    Preview = 2,
    FullText = 3,
}

public enum SessionLockState
{
    Unknown = 0,
    Unlocked = 1,
    Locked = 2,
}

public enum SystemLifecycleSignalKind
{
    Sleep = 0,
    Resume = 1,
}

public enum PowerSourceKind
{
    Unknown = 0,
    Ac = 1,
    Battery = 2,
}

public enum PowerChargingState
{
    Unknown = 0,
    NotPresent = 1,
    Charging = 2,
    Discharging = 3,
    NotCharging = 4,
    Full = 5,
}
