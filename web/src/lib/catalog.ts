import type {
  ClipboardCaptureMode,
  CollectorCapability,
  CollectorStatus,
  EventSeverity,
} from '@/types/api'

export const collectorIds = [
  'SystemLifecycleCollector',
  'SessionStateCollector',
  'ForegroundWindowCollector',
  'IdleCollector',
  'ClipboardCollector',
  'ProcessLifecycleCollector',
  'PowerCollector',
  'NetworkConnectivityCollector',
] as const

export const eventTypes = [
  'System.Startup',
  'System.Shutdown',
  'System.Sleep',
  'System.Resumed',
  'Session.Locked',
  'Session.Unlocked',
  'Window.Activated',
  'User.IdleStarted',
  'User.IdleEnded',
  'Clipboard.Changed',
  'Process.Started',
  'Process.Exited',
  'Power.SourceChanged',
  'Power.ChargingStateChanged',
  'Power.BatteryLevelChanged',
  'Network.Online',
  'Network.Offline',
  'Network.ActiveNetworkChanged',
] as const

export const eventSeverities: EventSeverity[] = ['Information', 'Warning', 'Error']
export const collectorCapabilities: CollectorCapability[] = [
  'Supported',
  'PartiallySupported',
  'Unsupported',
]
export const collectorStatuses: CollectorStatus[] = [
  'Disabled',
  'Starting',
  'Running',
  'Stopping',
  'Faulted',
  'Unsupported',
]
export const clipboardCaptureModes: ClipboardCaptureMode[] = [
  'Off',
  'MetadataOnly',
  'Preview',
  'FullText',
]

export const collectorTranslationKeys: Record<string, string> = {
  SystemLifecycleCollector: 'collectorNames.systemLifecycle',
  SessionStateCollector: 'collectorNames.sessionState',
  ForegroundWindowCollector: 'collectorNames.foregroundWindow',
  IdleCollector: 'collectorNames.idle',
  ClipboardCollector: 'collectorNames.clipboard',
  ProcessLifecycleCollector: 'collectorNames.processLifecycle',
  PowerCollector: 'collectorNames.power',
  NetworkConnectivityCollector: 'collectorNames.networkConnectivity',
}

export const eventTypeTranslationKeys: Record<string, string> = {
  'System.Startup': 'eventTypes.systemStartup',
  'System.Shutdown': 'eventTypes.systemShutdown',
  'System.Sleep': 'eventTypes.systemSleep',
  'System.Resumed': 'eventTypes.systemResumed',
  'Session.Locked': 'eventTypes.sessionLocked',
  'Session.Unlocked': 'eventTypes.sessionUnlocked',
  'Window.Activated': 'eventTypes.windowActivated',
  'User.IdleStarted': 'eventTypes.userIdleStarted',
  'User.IdleEnded': 'eventTypes.userIdleEnded',
  'Clipboard.Changed': 'eventTypes.clipboardChanged',
  'Process.Started': 'eventTypes.processStarted',
  'Process.Exited': 'eventTypes.processExited',
  'Power.SourceChanged': 'eventTypes.powerSourceChanged',
  'Power.ChargingStateChanged': 'eventTypes.powerChargingStateChanged',
  'Power.BatteryLevelChanged': 'eventTypes.powerBatteryLevelChanged',
  'Network.Online': 'eventTypes.networkOnline',
  'Network.Offline': 'eventTypes.networkOffline',
  'Network.ActiveNetworkChanged': 'eventTypes.networkActiveNetworkChanged',
}
