import dayjs from '@/lib/dayjs'
import type { LogEventResponse } from '@/types/api'

export function formatUtcDateTime(value?: string | null) {
  if (!value) {
    return '—'
  }

  return dayjs.utc(value).local().format('YYYY-MM-DD HH:mm:ss')
}

export function formatRelative(value?: string | null) {
  if (!value) {
    return '—'
  }

  return dayjs.utc(value).local().fromNow()
}

export function formatDurationSeconds(seconds: number) {
  if (!Number.isFinite(seconds)) {
    return '0s'
  }

  const duration = dayjs.duration(seconds, 'seconds')
  const hours = Math.floor(duration.asHours())
  const minutes = duration.minutes()

  if (hours > 0) {
    return `${hours}h ${minutes}m`
  }

  if (minutes > 0) {
    return `${minutes}m`
  }

  return `${Math.round(seconds)}s`
}

export function truncate(value: string, max = 100) {
  return value.length > max ? `${value.slice(0, max)}…` : value
}

export function formatNumber(value: number) {
  return new Intl.NumberFormat().format(value)
}

export function formatInputDateTimeLocal(value?: string | null) {
  if (!value) {
    return ''
  }

  return dayjs.utc(value).local().format('YYYY-MM-DDTHH:mm')
}

export function toUtcIso(value?: string | null) {
  if (!value) {
    return undefined
  }

  return dayjs(value).utc().toISOString()
}

export function createRecentFromInput(hours: number) {
  return dayjs().subtract(hours, 'hour').format('YYYY-MM-DDTHH:mm')
}

export function toStringList(value: string) {
  return value
    .split(/\r?\n|,/)
    .map((item) => item.trim())
    .filter(Boolean)
}

export function toTextareaList(value: string[]) {
  return value.join('\n')
}

export function prettyJson(value: unknown) {
  return JSON.stringify(value, null, 2)
}

export function summarizeLogEvent(log: LogEventResponse) {
  const data = log.content?.data
  if (!data || typeof data !== 'object') {
    return '—'
  }

  const pairs = Object.entries(data)
    .slice(0, 3)
    .map(([key, entry]) => `${key}: ${formatSummaryValue(entry)}`)

  return pairs.length > 0 ? pairs.join(' • ') : '—'
}

function formatSummaryValue(value: unknown) {
  if (value === null || value === undefined) {
    return '—'
  }

  if (typeof value === 'string') {
    return truncate(value, 48)
  }

  if (typeof value === 'number' || typeof value === 'boolean') {
    return String(value)
  }

  return truncate(prettyJson(value), 48)
}
