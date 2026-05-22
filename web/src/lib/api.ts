import { normalizeError } from '@/lib/errors'
import { useAppStore } from '@/store/app-store'

export class ApiError extends Error {
  status: number
  code: string
  traceId?: string
  details?: Record<string, string[]>

  constructor(
    status: number,
    code: string,
    message: string,
    traceId?: string,
    details?: Record<string, string[]>,
  ) {
    super(message)
    this.status = status
    this.code = code
    this.traceId = traceId
    this.details = details
  }
}

async function request<T>(input: string, init?: RequestInit): Promise<T> {
  try {
    const response = await fetch(input, {
      headers: {
        'Content-Type': 'application/json',
        ...(init?.headers ?? {}),
      },
      ...init,
    })

    useAppStore.getState().setApiHealth('online')

    if (!response.ok) {
      let errorPayload: unknown = null
      try {
        errorPayload = await response.json()
      } catch {
        // ignore
      }

      const normalized = normalizeError(errorPayload)
      throw new ApiError(
        response.status,
        normalized.code ?? 'http_error',
        normalized.message ?? `HTTP ${response.status}`,
        normalized.traceId,
        normalized.details,
      )
    }

    if (response.status === 204) {
      return undefined as T
    }

    return (await response.json()) as T
  } catch (error) {
    useAppStore.getState().setApiHealth('offline')
    throw error
  }
}

export const api = {
  get: <T>(input: string) => request<T>(input),
  post: <T>(input: string, body?: unknown) =>
    request<T>(input, {
      method: 'POST',
      body: body ? JSON.stringify(body) : undefined,
    }),
  put: <T>(input: string, body?: unknown) =>
    request<T>(input, {
      method: 'PUT',
      body: body ? JSON.stringify(body) : undefined,
    }),
}
