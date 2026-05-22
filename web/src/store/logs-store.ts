import { create } from 'zustand'
import { api } from '@/lib/api'
import { createRecentFromInput, toUtcIso } from '@/lib/format'
import { getErrorMessage } from '@/lib/errors'
import { useAppStore } from '@/store/app-store'
import type {
  LogEventResponse,
  LogsQuery,
  PagedResponse,
} from '@/types/api'

type LogsFilters = {
  fromLocal: string
  toLocal: string
  eventType: string
  collectorId: string
  severity: string
  userSessionId: string
  machineId: string
  pageSize: number
}

type LogsState = {
  filters: LogsFilters
  result: PagedResponse<LogEventResponse> | null
  loading: boolean
  selectedLog: LogEventResponse | null
  error: string | null
  setFilter: <K extends keyof LogsFilters>(key: K, value: LogsFilters[K]) => void
  resetFilters: () => void
  fetch: (page?: number) => Promise<void>
  openDetails: (log: LogEventResponse | null) => void
}

function createDefaultFilters(): LogsFilters {
  return {
    fromLocal: createRecentFromInput(24),
    toLocal: '',
    eventType: '',
    collectorId: '',
    severity: '',
    userSessionId: '',
    machineId: '',
    pageSize: 50,
  }
}

export const useLogsStore = create<LogsState>()((set, get) => ({
  filters: createDefaultFilters(),
  result: null,
  loading: false,
  selectedLog: null,
  error: null,
  setFilter: (key, value) =>
    set((state) => ({
      filters: {
        ...state.filters,
        [key]: value,
      },
    })),
  resetFilters: () => set({ filters: createDefaultFilters() }),
  fetch: async (page = 1) => {
    set({ loading: true, error: null })
    const { filters } = get()

    const query: LogsQuery = {
      fromUtc: toUtcIso(filters.fromLocal),
      toUtc: toUtcIso(filters.toLocal),
      eventType: filters.eventType || undefined,
      collectorId: filters.collectorId || undefined,
      severity: (filters.severity || undefined) as LogsQuery['severity'],
      userSessionId: filters.userSessionId || undefined,
      machineId: filters.machineId || undefined,
      page,
      pageSize: filters.pageSize,
    }

    const params = new URLSearchParams()
    Object.entries(query).forEach(([key, value]) => {
      if (value !== undefined && value !== '') {
        params.set(key, String(value))
      }
    })

    try {
      const result = await api.get<PagedResponse<LogEventResponse>>(
        `/api/logs?${params.toString()}`,
      )
      set({ result, loading: false })
      useAppStore.getState().markRefreshed()
    } catch (error) {
      set({
        loading: false,
        error: getErrorMessage(error, 'Failed to load logs'),
      })
    }
  },
  openDetails: (log) => set({ selectedLog: log }),
}))

