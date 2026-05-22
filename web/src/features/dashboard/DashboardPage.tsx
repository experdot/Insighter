import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import {
  Activity,
  ClipboardList,
  Coffee,
  MousePointerClick,
  TriangleAlert,
} from 'lucide-react'
import {
  Area,
  AreaChart,
  CartesianGrid,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { EmptyState } from '@/components/shared/EmptyState'
import { PageHeader } from '@/components/shared/PageHeader'
import { StatCard } from '@/components/shared/StatCard'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { formatDurationSeconds, formatNumber, formatUtcDateTime } from '@/lib/format'
import {
  getCapabilityTone,
  getCollectorStatusTone,
  translateCapability,
  translateCollectorId,
  translateCollectorStatus,
  translateEventType,
} from '@/lib/presenters'
import { useDashboardStore } from '@/store/dashboard-store'
import { useRefreshStore } from '@/store/refresh'

const chartPalette = ['#c77d2b', '#0f766e', '#cc5f3f', '#9b6a2f', '#3d6f85']

export function DashboardPage() {
  const { t } = useTranslation(['common', 'dashboard'])
  const {
    range,
    summary,
    timeline,
    collectors,
    loading,
    error,
    setRange,
    fetch,
  } = useDashboardStore()
  const refreshToken = useRefreshStore((state) => state.tokens.dashboard)

  useEffect(() => {
    void fetch()
  }, [fetch, range, refreshToken])

  useEffect(() => {
    const timer = window.setInterval(() => {
      void fetch()
    }, 15000)

    return () => window.clearInterval(timer)
  }, [fetch])

  const distribution = Object.entries(summary?.eventCountsByType ?? {}).map(
    ([name, value], index) => ({
      name,
      label: translateEventType(t, name),
      value,
      fill: chartPalette[index % chartPalette.length],
    }),
  )

  const timelineData = timeline.map((item) => ({
    time: formatUtcDateTime(item.bucketStartUtc),
    totalEvents: item.totalEvents,
  }))

  const hasData = (summary?.totalEvents ?? 0) > 0

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow={t('dashboard:eyebrow')}
        title={t('dashboard:title')}
        description={t('dashboard:description')}
        actions={
          <div className="flex items-center gap-2">
            <Button
              variant={range === '24h' ? 'default' : 'outline'}
              onClick={() => setRange('24h')}
            >
              {t('dashboard:range24h')}
            </Button>
            <Button
              variant={range === '7d' ? 'default' : 'outline'}
              onClick={() => setRange('7d')}
            >
              {t('dashboard:range7d')}
            </Button>
          </div>
        }
      />

      {error ? (
        <EmptyState
          title={error}
          description={t('dashboard:empty.description')}
          icon={<TriangleAlert className="size-6" />}
        />
      ) : null}

      <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
        {loading && !summary
          ? Array.from({ length: 5 }).map((_, index) => (
              <Card key={index}>
                <CardContent className="space-y-3 px-5 py-5 md:px-6 md:py-6">
                  <Skeleton className="h-4 w-20" />
                  <Skeleton className="h-10 w-24" />
                  <Skeleton className="h-3 w-28" />
                </CardContent>
              </Card>
            ))
          : (
            <>
              <StatCard
                label={t('dashboard:totalEvents')}
                value={formatNumber(summary?.totalEvents ?? 0)}
                icon={<Activity className="size-5" />}
              />
              <StatCard
                label={t('dashboard:windowActivations')}
                value={formatNumber(summary?.windowActivationCount ?? 0)}
                icon={<MousePointerClick className="size-5" />}
                accent="secondary"
              />
              <StatCard
                label={t('dashboard:clipboardChanges')}
                value={formatNumber(summary?.clipboardChangeCount ?? 0)}
                icon={<ClipboardList className="size-5" />}
                accent="muted"
              />
              <StatCard
                label={t('dashboard:idlePeriods')}
                value={formatNumber(summary?.idlePeriodCount ?? 0)}
                icon={<Coffee className="size-5" />}
              />
              <StatCard
                label={t('dashboard:idleDuration')}
                value={formatDurationSeconds(summary?.totalIdleDurationSeconds ?? 0)}
                icon={<Activity className="size-5" />}
                accent="secondary"
              />
            </>
          )}
      </section>

      {!loading && !hasData ? (
        <EmptyState
          title={t('dashboard:empty.title')}
          description={t('dashboard:empty.description')}
          icon={<Activity className="size-6" />}
        />
      ) : null}

      <section className="grid gap-6 xl:grid-cols-[minmax(0,1.5fr)_minmax(320px,0.9fr)]">
        <Card>
          <CardHeader>
            <CardTitle>{t('dashboard:timeline')}</CardTitle>
            <CardDescription>{t('dashboard:timelineDescription')}</CardDescription>
          </CardHeader>
          <CardContent className="h-[320px]">
            {loading && timelineData.length === 0 ? (
              <Skeleton className="h-full w-full" />
            ) : (
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart data={timelineData}>
                  <defs>
                    <linearGradient id="timelineFill" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="5%" stopColor="#c77d2b" stopOpacity={0.35} />
                      <stop offset="95%" stopColor="#c77d2b" stopOpacity={0.03} />
                    </linearGradient>
                  </defs>
                  <CartesianGrid stroke="rgba(86,67,39,0.12)" vertical={false} />
                  <XAxis
                    dataKey="time"
                    tick={{ fontSize: 12, fill: '#5b6472' }}
                    tickLine={false}
                    axisLine={false}
                    minTickGap={28}
                  />
                  <YAxis
                    tick={{ fontSize: 12, fill: '#5b6472' }}
                    tickLine={false}
                    axisLine={false}
                    allowDecimals={false}
                  />
                  <Tooltip
                    contentStyle={{
                      borderRadius: 18,
                      border: '1px solid rgba(86,67,39,0.16)',
                      background: 'rgba(255,251,245,0.98)',
                    }}
                  />
                  <Area
                    type="monotone"
                    dataKey="totalEvents"
                    stroke="#c77d2b"
                    strokeWidth={2}
                    fill="url(#timelineFill)"
                  />
                </AreaChart>
              </ResponsiveContainer>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>{t('dashboard:eventDistribution')}</CardTitle>
          </CardHeader>
          <CardContent className="h-[320px]">
            {loading && distribution.length === 0 ? (
              <Skeleton className="h-full w-full" />
            ) : distribution.length === 0 ? (
              <div className="flex h-full items-center justify-center text-sm text-muted-foreground">
                {t('dashboard:empty.description')}
              </div>
            ) : (
              <div className="grid h-full gap-4 md:grid-cols-[minmax(0,1fr)_220px] md:items-center">
                <ResponsiveContainer width="100%" height="100%">
                  <PieChart>
                    <Pie
                      data={distribution}
                      dataKey="value"
                      nameKey="label"
                      innerRadius={58}
                      outerRadius={92}
                      paddingAngle={3}
                    />
                    <Tooltip
                      contentStyle={{
                        borderRadius: 18,
                        border: '1px solid rgba(86,67,39,0.16)',
                        background: 'rgba(255,251,245,0.98)',
                      }}
                    />
                  </PieChart>
                </ResponsiveContainer>
                <div className="space-y-3">
                  {distribution.map((item) => (
                    <div key={item.name} className="flex items-center justify-between gap-3">
                      <div className="flex items-center gap-3">
                        <span
                          className="size-3 rounded-full"
                          style={{ backgroundColor: item.fill }}
                        />
                        <span className="text-sm text-foreground">{item.label}</span>
                      </div>
                      <span className="font-mono text-sm text-muted-foreground">
                        {formatNumber(item.value)}
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </CardContent>
        </Card>
      </section>

      <Card>
        <CardHeader>
          <CardTitle>{t('dashboard:collectorHealth')}</CardTitle>
          <CardDescription>{t('dashboard:collectorHealthDescription')}</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4 lg:grid-cols-2">
          {collectors.map((collector) => (
            <div
              key={collector.collectorId}
              className="rounded-[24px] border border-border/70 bg-card/55 p-5"
            >
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <h3 className="font-heading text-xl font-semibold tracking-[-0.03em]">
                    {translateCollectorId(t, collector.collectorId, collector.displayName)}
                  </h3>
                  <p className="mt-1 text-sm text-muted-foreground">
                    {collector.displayName}
                  </p>
                </div>
                <div className="flex flex-wrap gap-2">
                  <StatusBadge
                    label={translateCollectorStatus(t, collector.status)}
                    tone={getCollectorStatusTone(collector.status)}
                  />
                  <StatusBadge
                    label={translateCapability(t, collector.capability)}
                    tone={getCapabilityTone(collector.capability)}
                  />
                </div>
              </div>
              <div className="mt-4 grid gap-2 text-sm text-muted-foreground">
                <p>
                  {t('dashboard:health.lastHeartbeat')}: <span className="text-foreground">{formatUtcDateTime(collector.lastHeartbeatUtc)}</span>
                </p>
                <p>
                  {t('dashboard:health.enabled')}: <span className="text-foreground">{collector.enabled ? t('common:enabled') : t('common:disabled')}</span>
                </p>
                <p>
                  {t('dashboard:health.reason')}: <span className="text-foreground">{collector.capabilityReason ?? '—'}</span>
                </p>
              </div>
            </div>
          ))}
        </CardContent>
      </Card>
    </div>
  )
}
