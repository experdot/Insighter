import i18n from 'i18next'
import LanguageDetector from 'i18next-browser-languagedetector'
import { initReactI18next } from 'react-i18next'
import commonEn from '@/i18n/locales/en/common.json'
import navEn from '@/i18n/locales/en/nav.json'
import dashboardEn from '@/i18n/locales/en/dashboard.json'
import logsEn from '@/i18n/locales/en/logs.json'
import collectorsEn from '@/i18n/locales/en/collectors.json'
import settingsEn from '@/i18n/locales/en/settings.json'
import commonZh from '@/i18n/locales/zh-CN/common.json'
import navZh from '@/i18n/locales/zh-CN/nav.json'
import dashboardZh from '@/i18n/locales/zh-CN/dashboard.json'
import logsZh from '@/i18n/locales/zh-CN/logs.json'
import collectorsZh from '@/i18n/locales/zh-CN/collectors.json'
import settingsZh from '@/i18n/locales/zh-CN/settings.json'
import { detectInitialLocale, normalizeLocale } from '@/i18n/config'

void i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    lng: detectInitialLocale(),
    fallbackLng: 'en',
    supportedLngs: ['en', 'zh-CN'],
    defaultNS: 'common',
    ns: ['common', 'nav', 'dashboard', 'logs', 'collectors', 'settings'],
    interpolation: {
      escapeValue: false,
    },
    resources: {
      en: {
        common: commonEn,
        nav: navEn,
        dashboard: dashboardEn,
        logs: logsEn,
        collectors: collectorsEn,
        settings: settingsEn,
      },
      'zh-CN': {
        common: commonZh,
        nav: navZh,
        dashboard: dashboardZh,
        logs: logsZh,
        collectors: collectorsZh,
        settings: settingsZh,
      },
    },
    detection: {
      order: ['localStorage', 'navigator'],
      caches: ['localStorage'],
      lookupLocalStorage: 'insighter-locale',
      convertDetectedLanguage: (language) => normalizeLocale(language),
    },
  })

export default i18n
