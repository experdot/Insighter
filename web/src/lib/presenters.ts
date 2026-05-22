import type { TFunction } from 'i18next'
import {
  collectorTranslationKeys,
  eventTypeTranslationKeys,
} from '@/lib/catalog'
import type {
  ClipboardCaptureMode,
  CollectorCapability,
  CollectorResponse,
  CollectorStatus,
  EventSeverity,
} from '@/types/api'

export type BadgeTone =
  | 'default'
  | 'muted'
  | 'secondary'
  | 'warning'
  | 'destructive'
  | 'success'
  | 'outline'

export function translateCollectorId(
  t: TFunction,
  collectorId: string,
  fallback?: string,
) {
  const key = collectorTranslationKeys[collectorId]
  return key ? t(`common:${key}`) : (fallback ?? collectorId)
}

export function translateEventType(t: TFunction, eventType: string) {
  const key = eventTypeTranslationKeys[eventType]
  return key ? t(`common:${key}`) : eventType
}

export function translateSeverity(t: TFunction, severity: EventSeverity) {
  return t(`common:severity.${severity}`)
}

export function translateCollectorStatus(
  t: TFunction,
  status: CollectorStatus,
) {
  return t(`common:collectorStatus.${status}`)
}

export function translateCapability(
  t: TFunction,
  capability: CollectorCapability,
) {
  return t(`common:collectorCapability.${capability}`)
}

export function translateClipboardMode(
  t: TFunction,
  mode: ClipboardCaptureMode,
) {
  return t(`common:clipboardModes.${mode}`)
}

export function getCollectorStatusTone(status: CollectorStatus): BadgeTone {
  switch (status) {
    case 'Running':
      return 'success'
    case 'Starting':
    case 'Stopping':
      return 'secondary'
    case 'Faulted':
      return 'destructive'
    case 'Unsupported':
      return 'warning'
    case 'Disabled':
    default:
      return 'muted'
  }
}

export function getCapabilityTone(
  capability: CollectorCapability,
): BadgeTone {
  switch (capability) {
    case 'Supported':
      return 'success'
    case 'PartiallySupported':
      return 'warning'
    case 'Unsupported':
    default:
      return 'muted'
  }
}

export function getSeverityTone(severity: EventSeverity): BadgeTone {
  switch (severity) {
    case 'Error':
      return 'destructive'
    case 'Warning':
      return 'warning'
    case 'Information':
    default:
      return 'secondary'
  }
}

export function getCollectorSettings(
  collector?: Pick<CollectorResponse, 'settings'>,
) {
  return collector?.settings ?? {}
}

export function numberSetting(
  settings: Record<string, unknown>,
  key: string,
  fallback = 0,
) {
  const value = settings[key]
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback
}

export function booleanSetting(
  settings: Record<string, unknown>,
  key: string,
  fallback = false,
) {
  const value = settings[key]
  return typeof value === 'boolean' ? value : fallback
}

export function stringSetting(
  settings: Record<string, unknown>,
  key: string,
  fallback = '',
) {
  const value = settings[key]
  return typeof value === 'string' ? value : fallback
}

export function stringArraySetting(
  settings: Record<string, unknown>,
  key: string,
) {
  const value = settings[key]
  if (!Array.isArray(value)) {
    return []
  }

  return value.filter((entry): entry is string => typeof entry === 'string')
}

