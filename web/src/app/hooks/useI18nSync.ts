import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import dayjs from '@/lib/dayjs'
import { dayjsLocaleMap, normalizeLocale } from '@/i18n/config'
import { useAppStore } from '@/store/app-store'

export function useI18nSync() {
  const { i18n } = useTranslation()
  const locale = useAppStore((state) => state.locale)

  useEffect(() => {
    const normalized = normalizeLocale(locale)
    if (i18n.language !== normalized) {
      void i18n.changeLanguage(normalized)
    }

    dayjs.locale(dayjsLocaleMap[normalized])
    document.documentElement.lang = normalized
  }, [i18n, locale])
}
