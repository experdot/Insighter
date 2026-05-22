import { create } from 'zustand'
import { toast } from 'sonner'
import { api } from '@/lib/api'
import { getErrorMessage } from '@/lib/errors'
import i18n from '@/i18n'
import { useAppStore } from '@/store/app-store'
import type { CollectorConfigRequest, CollectorResponse } from '@/types/api'

type CollectorsState = {
  items: CollectorResponse[]
  loading: boolean
  mutatingId: string | null
  selectedCollectorId: string | null
  error: string | null
  fetch: () => Promise<void>
  startCollector: (collectorId: string) => Promise<void>
  stopCollector: (collectorId: string) => Promise<void>
  saveCollectorConfig: (collectorId: string, body: CollectorConfigRequest) => Promise<void>
  openSheet: (collectorId: string | null) => void
}

export const useCollectorsStore = create<CollectorsState>()((set) => ({
  items: [],
  loading: false,
  mutatingId: null,
  selectedCollectorId: null,
  error: null,
  fetch: async () => {
    set({ loading: true, error: null })
    try {
      const items = await api.get<CollectorResponse[]>('/api/collectors')
      set({ items, loading: false })
      useAppStore.getState().markRefreshed()
    } catch (error) {
      set({
        loading: false,
        error: getErrorMessage(error, 'Failed to load collectors'),
      })
    }
  },
  startCollector: async (collectorId) => {
    set({ mutatingId: collectorId })
    try {
      const updated = await api.post<CollectorResponse>(
        `/api/collectors/${collectorId}/start`,
      )
      set((state) => ({
        items: state.items.map((item) =>
          item.collectorId === collectorId ? updated : item,
        ),
        mutatingId: null,
      }))
      useAppStore.getState().markRefreshed()
      toast.success(i18n.t('common:toast.collectorStarted'))
    } catch (error) {
      set({ mutatingId: null })
      toast.error(getErrorMessage(error, 'Failed to start collector'))
    }
  },
  stopCollector: async (collectorId) => {
    set({ mutatingId: collectorId })
    try {
      const updated = await api.post<CollectorResponse>(
        `/api/collectors/${collectorId}/stop`,
      )
      set((state) => ({
        items: state.items.map((item) =>
          item.collectorId === collectorId ? updated : item,
        ),
        mutatingId: null,
      }))
      useAppStore.getState().markRefreshed()
      toast.success(i18n.t('common:toast.collectorStopped'))
    } catch (error) {
      set({ mutatingId: null })
      toast.error(getErrorMessage(error, 'Failed to stop collector'))
    }
  },
  saveCollectorConfig: async (collectorId, body) => {
    set({ mutatingId: collectorId })
    try {
      const updated = await api.put<CollectorResponse>(
        `/api/collectors/${collectorId}/config`,
        body,
      )
      set((state) => ({
        items: state.items.map((item) =>
          item.collectorId === collectorId ? updated : item,
        ),
        mutatingId: null,
        selectedCollectorId: null,
      }))
      useAppStore.getState().markRefreshed()
      toast.success(i18n.t('common:toast.collectorSaved'))
    } catch (error) {
      set({ mutatingId: null })
      toast.error(getErrorMessage(error, 'Failed to save collector config'))
    }
  },
  openSheet: (collectorId) => set({ selectedCollectorId: collectorId }),
}))
