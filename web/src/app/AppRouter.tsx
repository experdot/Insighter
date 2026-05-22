import { Suspense, lazy } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { AppShell } from '@/components/layout/AppShell'
import { Skeleton } from '@/components/ui/skeleton'

const DashboardPage = lazy(() =>
  import('@/features/dashboard/DashboardPage').then((module) => ({
    default: module.DashboardPage,
  })),
)
const LogsPage = lazy(() =>
  import('@/features/logs/LogsPage').then((module) => ({
    default: module.LogsPage,
  })),
)
const CollectorsPage = lazy(() =>
  import('@/features/collectors/CollectorsPage').then((module) => ({
    default: module.CollectorsPage,
  })),
)
const SettingsPage = lazy(() =>
  import('@/features/settings/SettingsPage').then((module) => ({
    default: module.SettingsPage,
  })),
)

function RouteFallback() {
  return (
    <div className="space-y-5">
      <Skeleton className="h-40 w-full rounded-[28px]" />
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {Array.from({ length: 4 }).map((_, index) => (
          <Skeleton key={index} className="h-36 w-full" />
        ))}
      </div>
      <Skeleton className="h-[360px] w-full rounded-[28px]" />
    </div>
  )
}

export function AppRouter() {
  return (
    <Suspense fallback={<RouteFallback />}>
      <Routes>
        <Route element={<AppShell />}>
          <Route index element={<DashboardPage />} />
          <Route path="/logs" element={<LogsPage />} />
          <Route path="/collectors" element={<CollectorsPage />} />
          <Route path="/settings" element={<SettingsPage />} />
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </Suspense>
  )
}
