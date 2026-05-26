import {
  Activity,
  LayoutDashboard,
  Menu,
  RadioTower,
  Settings2,
  ScrollText,
} from 'lucide-react'
import { Outlet, useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { LanguageSwitcher } from '@/components/shared/LanguageSwitcher'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { NavLink } from '@/components/layout/NavLink'
import { Button } from '@/components/ui/button'
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/sheet'
import { Separator } from '@/components/ui/separator'
import { formatRelative } from '@/lib/format'
import { useAppStore } from '@/store/app-store'
import { useRefreshStore, type RefreshScope } from '@/store/refresh'

const routeScopes: Record<string, RefreshScope> = {
  '/': 'dashboard',
  '/logs': 'logs',
  '/collectors': 'collectors',
  '/settings': 'settings',
}

export function AppShell() {
  const { t } = useTranslation(['common', 'nav'])
  const location = useLocation()
  const apiHealth = useAppStore((state) => state.apiHealth)
  const lastRefreshedAt = useAppStore((state) => state.lastRefreshedAt)
  const triggerRefresh = useRefreshStore((state) => state.trigger)

  const items = [
    { to: '/', label: t('nav:dashboard'), icon: LayoutDashboard },
    { to: '/logs', label: t('nav:logs'), icon: ScrollText },
    { to: '/collectors', label: t('nav:collectors'), icon: RadioTower },
    { to: '/settings', label: t('nav:settings'), icon: Settings2 },
  ]

  const activeScope = routeScopes[location.pathname] ?? 'dashboard'

  return (
    <div className="min-h-screen">
      <div className="mx-auto grid min-h-screen max-w-[1680px] gap-6 px-4 py-4 md:px-6 lg:grid-cols-[280px_minmax(0,1fr)] lg:px-8">
        <aside className="hidden lg:block">
          <div className="glass-card sticky top-4 flex min-h-[calc(100vh-2rem)] flex-col overflow-hidden">
            <div className="workbench-grid border-b border-border/70 px-6 py-6">
              <div className="flex items-center gap-3">
                <div className="flex size-12 items-center justify-center rounded-2xl bg-primary text-primary-foreground shadow-[0_18px_36px_rgba(199,125,43,0.24)]">
                  <Activity className="size-5" />
                </div>
                <div>
                  <p className="font-heading text-xl font-bold tracking-[-0.04em]">
                    {t('common:appName')}
                  </p>
                  <p className="text-sm text-muted-foreground">
                    {t('common:workspace')}
                  </p>
                </div>
              </div>
            </div>

            <nav className="flex-1 space-y-2 px-4 py-5">
              {items.map((item) => (
                <NavLink key={item.to} {...item} />
              ))}
            </nav>

            <Separator />

            <div className="space-y-4 px-6 py-5">
              <div className="flex items-center justify-between gap-3">
                <span className="text-xs font-semibold uppercase tracking-[0.16em] text-muted-foreground">
                  {t('common:localApi')}
                </span>
                <StatusBadge
                  label={t(`common:apiHealth.${apiHealth}`)}
                  tone={
                    apiHealth === 'online'
                      ? 'success'
                      : apiHealth === 'offline'
                        ? 'destructive'
                        : 'muted'
                  }
                />
              </div>
              <p className="text-sm text-muted-foreground">
                {t('common:lastUpdated')}: {lastRefreshedAt ? formatRelative(lastRefreshedAt) : t('common:notYet')}
              </p>
              <LanguageSwitcher />
            </div>
          </div>
        </aside>

        <div className="min-w-0">
          <header className="glass-card mb-6 flex items-center justify-between gap-3 px-4 py-4 md:px-5 lg:hidden">
            <div className="flex items-center gap-3">
              <Sheet>
                <SheetTrigger asChild>
                  <Button variant="outline" size="icon">
                    <Menu className="size-4" />
                  </Button>
                </SheetTrigger>
                <SheetContent side="left" className="flex flex-col">
                  <SheetHeader>
                    <SheetTitle>{t('common:appName')}</SheetTitle>
                  </SheetHeader>
                  <nav className="space-y-2 px-4 pb-6 md:px-5">
                    {items.map((item) => (
                      <NavLink key={item.to} {...item} />
                    ))}
                  </nav>
                </SheetContent>
              </Sheet>
              <div>
                <p className="font-heading text-lg font-bold">{t('common:appName')}</p>
                <p className="text-xs uppercase tracking-[0.16em] text-muted-foreground">
                  {t('common:workspace')}
                </p>
              </div>
            </div>
            <LanguageSwitcher />
          </header>

          <div className="mb-6 flex flex-wrap items-center justify-between gap-3">
            <div className="flex items-center gap-3">
              <StatusBadge
                label={`${t('common:localApi')} · ${t(`common:apiHealth.${apiHealth}`)}`}
                tone={
                  apiHealth === 'online'
                    ? 'success'
                    : apiHealth === 'offline'
                      ? 'destructive'
                      : 'muted'
                }
              />
              <p className="text-sm text-muted-foreground">
                {t('common:lastUpdated')}: {lastRefreshedAt ? formatRelative(lastRefreshedAt) : t('common:notYet')}
              </p>
            </div>
            <Button
              variant="outline"
              onClick={() => triggerRefresh(activeScope)}
            >
              {t('common:refresh')}
            </Button>
          </div>

          <main className="pb-8">
            <Outlet />
          </main>
        </div>
      </div>
    </div>
  )
}
