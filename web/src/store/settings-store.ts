import { create } from 'zustand'
import { toast } from 'sonner'
import { api } from '@/lib/api'
import { getErrorMessage } from '@/lib/errors'
import i18n from '@/i18n'
import { useAppStore } from '@/store/app-store'
import type { SettingsResponse, SettingsUpdateRequest } from '@/types/api'

type SettingsForm = {
  retentionDays: string
}

type SettingsState = {
  snapshot: SettingsResponse | null
  form: SettingsForm
  loading: boolean
  saving: boolean
  error: string | null
  setField: <K extends keyof SettingsForm>(key: K, value: SettingsForm[K]) => void
  fetch: () => Promise<void>
  save: () => Promise<void>
}

function toForm(snapshot: SettingsResponse): SettingsForm {
  return {
    retentionDays: String(snapshot.retentionDays),
  }
}

export const useSettingsStore = create<SettingsState>()((set, get) => ({
  snapshot: null,
  form: {
    retentionDays: '30',
  },
  loading: false,
  saving: false,
  error: null,
  setField: (key, value) =>
    set((state) => ({
      form: {
        ...state.form,
        [key]: value,
      },
    })),
  fetch: async () => {
    set({ loading: true, error: null })
    try {
      const snapshot = await api.get<SettingsResponse>('/api/settings')
      set({ snapshot, form: toForm(snapshot), loading: false })
      useAppStore.getState().markRefreshed()
    } catch (error) {
      set({
        loading: false,
        error: getErrorMessage(error, 'Failed to load settings'),
      })
    }
  },
  save: async () => {
    const { form } = get()
    const payload: SettingsUpdateRequest = {
      retentionDays: Number(form.retentionDays),
    }

    set({ saving: true })
    try {
      const snapshot = await api.put<SettingsResponse>('/api/settings', payload)
      set({ snapshot, form: toForm(snapshot), saving: false })
      useAppStore.getState().markRefreshed()
      toast.success(i18n.t('common:toast.settingsSaved'))
    } catch (error) {
      set({ saving: false })
      toast.error(getErrorMessage(error, 'Failed to save settings'))
    }
  },
}))
