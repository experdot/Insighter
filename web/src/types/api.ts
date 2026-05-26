export type Locale = 'en' | 'zh-CN'

export type ApiHealth = 'checking' | 'online' | 'offline'
export type CollectorCapability = 'Supported' | 'PartiallySupported' | 'Unsupported'
export type CollectorStatus =
  | 'Disabled'
  | 'Starting'
  | 'Running'
  | 'Stopping'
  | 'Faulted'
  | 'Unsupported'
export type EventSeverity = 'Information' | 'Warning' | 'Error'
export type ClipboardCaptureMode = 'Off' | 'MetadataOnly' | 'Preview' | 'FullText'

export type ErrorResponse = {
  code: string
  message: string
  details?: Record<string, string[]>
  traceId?: string
}

export type PagedResponse<T> = {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export type CollectorConfigRequest = {
  enabled: boolean
  version: number
  settings: Record<string, unknown>
}

export type CollectorResponse = {
  collectorId: string
  displayName: string
  enabled: boolean
  status: CollectorStatus
  capability: CollectorCapability
  capabilityReason?: string | null
  lastHeartbeatUtc?: string | null
  lastError?: string | null
  configVersion: number
  settings?: Record<string, unknown> | null
}

export type LogEventResponse = {
  id: string
  occurredAtUtc: string
  eventType: string
  collectorId: string
  severity: EventSeverity
  userSessionId: string
  machineId: string
  content: {
    schemaVersion?: number
    platform?: string
    data?: Record<string, unknown>
  } & Record<string, unknown>
}

export type DashboardSummaryResponse = {
  totalEvents: number
  windowActivationCount: number
  clipboardChangeCount: number
  idlePeriodCount: number
  totalIdleDurationSeconds: number
  eventCountsByType: Record<string, number>
}

export type TimelineBucketResponse = {
  bucketStartUtc: string
  bucketEndUtc: string
  totalEvents: number
  eventCountsByType: Record<string, number>
}

export type PlatformCapabilityResponse = {
  capabilityId: string
  supportLevel: CollectorCapability
  reason?: string | null
}

export type SettingsResponse = {
  databasePath: string
  apiUrls: string
  queueCapacity: number
  retentionDays: number
  queryMaxPageSize: number
  autoStartCollectors: boolean
}

export type SettingsUpdateRequest = {
  retentionDays?: number
}

export type LogsQuery = {
  fromUtc?: string
  toUtc?: string
  eventType?: string
  collectorId?: string
  severity?: EventSeverity
  userSessionId?: string
  machineId?: string
  page: number
  pageSize: number
}
