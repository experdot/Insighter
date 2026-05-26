import { create } from 'zustand'
import { api } from '@/lib/api'
import { getErrorMessage } from '@/lib/errors'
import { useAppStore } from '@/store/app-store'
import type {
  DashboardSummaryResponse,
  TimelineBucketResponse,
} from '@/types/api'

type DashboardRange = '24h' | '7d'

type DashboardState = {
  range: DashboardRange
  summary: DashboardSummaryResponse | null
  timeline: TimelineBucketResponse[]
  loading: boolean
  error: string | null
  setRange: (range: DashboardRange) => void
  fetch: () => Promise<void>
}

export const useDashboardStore = create<DashboardState>()((set, get) => ({
  range: '24h',
  summary: null,
  timeline: [],
  loading: false,
  error: null,
  setRange: (range) => set({ range }),
  fetch: async () => {
    const { range } = get()
    set({ loading: true, error: null })

    const now = new Date()
    const from = new Date(now)
    from.setUTCDate(from.getUTCDate() - (range === '24h' ? 1 : 7))

    const params = new URLSearchParams({
      fromUtc: from.toISOString(),
      toUtc: now.toISOString(),
    })

    const bucket = range === '24h' ? 'hour' : 'day'

    try {
      const [summary, timeline] = await Promise.all([
        api.get<DashboardSummaryResponse>(`/api/dashboard/summary?${params.toString()}`),
        api.get<TimelineBucketResponse[]>(
          `/api/dashboard/timeline?${params.toString()}&bucket=${bucket}`,
        ),
      ])

      set({
        summary,
        timeline,
        loading: false,
      })
      useAppStore.getState().markRefreshed()
    } catch (error) {
      set({
        loading: false,
        error: getErrorMessage(error, 'Failed to load dashboard'),
      })
    }
  },
}))
