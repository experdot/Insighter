import type { Locale } from '@/types/api'

export const supportedLocales: Locale[] = ['en', 'zh-CN']

export const dayjsLocaleMap: Record<Locale, string> = {
  en: 'en',
  'zh-CN': 'zh-cn',
}

export function normalizeLocale(value?: string | null): Locale {
  if (!value) {
    return 'en'
  }

  const lower = value.toLowerCase()
  if (lower.startsWith('zh')) {
    return 'zh-CN'
  }

  return 'en'
}

export function detectInitialLocale(): Locale {
  if (typeof window === 'undefined') {
    return 'en'
  }

  return normalizeLocale(window.navigator.language)
}
