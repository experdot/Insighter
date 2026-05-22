import { create } from 'zustand'

export type RefreshScope = 'dashboard' | 'logs' | 'collectors' | 'settings'

type RefreshState = {
  tokens: Record<RefreshScope, number>
  trigger: (scope: RefreshScope | 'all') => void
}

export const useRefreshStore = create<RefreshState>()((set) => ({
  tokens: {
    dashboard: 0,
    logs: 0,
    collectors: 0,
    settings: 0,
  },
  trigger: (scope) =>
    set((state) => {
      if (scope === 'all') {
        return {
          tokens: {
            dashboard: state.tokens.dashboard + 1,
            logs: state.tokens.logs + 1,
            collectors: state.tokens.collectors + 1,
            settings: state.tokens.settings + 1,
          },
        }
      }

      return {
        tokens: {
          ...state.tokens,
          [scope]: state.tokens[scope] + 1,
        },
      }
    }),
}))

