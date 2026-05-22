import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { ApiHealth, Locale } from '@/types/api'
import { detectInitialLocale } from '@/i18n/config'

type AppState = {
  locale: Locale
  apiHealth: ApiHealth
  lastRefreshedAt: string | null
  setLocale: (locale: Locale) => void
  setApiHealth: (health: ApiHealth) => void
  markRefreshed: () => void
}

export const useAppStore = create<AppState>()(
  persist(
    (set) => ({
      locale: detectInitialLocale(),
      apiHealth: 'checking',
      lastRefreshedAt: null,
      setLocale: (locale) => set({ locale }),
      setApiHealth: (apiHealth) => set({ apiHealth }),
      markRefreshed: () => set({ lastRefreshedAt: new Date().toISOString() }),
    }),
    {
      name: 'insighter-app',
      partialize: (state) => ({ locale: state.locale }),
    },
  ),
)
