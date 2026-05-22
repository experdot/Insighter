export type ErrorPayload = {
  code?: string
  message?: string
  details?: Record<string, string[]>
  traceId?: string
}

export function getErrorMessage(error: unknown, fallback = 'Unexpected error') {
  if (error instanceof Error && error.message) {
    return error.message
  }

  if (typeof error === 'string' && error) {
    return error
  }

  const normalized = normalizeError(error)
  return normalized.message ?? fallback
}

export function normalizeError(payload: unknown): ErrorPayload {
  if (!payload || typeof payload !== 'object') {
    return {}
  }

  const object = payload as Record<string, unknown>
  return {
    code: typeof object.code === 'string' ? object.code : undefined,
    message: typeof object.message === 'string' ? object.message : undefined,
    traceId: typeof object.traceId === 'string' ? object.traceId : undefined,
    details: isDetailsRecord(object.details) ? object.details : undefined,
  }
}

function isDetailsRecord(value: unknown): value is Record<string, string[]> {
  if (!value || typeof value !== 'object') {
    return false
  }

  return Object.values(value as Record<string, unknown>).every(
    (item) => Array.isArray(item) && item.every((entry) => typeof entry === 'string'),
  )
}
