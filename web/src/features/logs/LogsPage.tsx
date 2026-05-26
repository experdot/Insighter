import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { FileSearch, TriangleAlert } from 'lucide-react'
import { EmptyState } from '@/components/shared/EmptyState'
import { JsonPreview } from '@/components/shared/JsonPreview'
import { PageHeader } from '@/components/shared/PageHeader'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { collectorIds, eventSeverities, eventTypes } from '@/lib/catalog'
import {
  formatNumber,
  formatUtcDateTime,
  prettyJson,
  summarizeLogEvent,
} from '@/lib/format'
import {
  getSeverityTone,
  translateCollectorId,
  translateEventType,
  translateSeverity,
} from '@/lib/presenters'
import { useLogsStore } from '@/store/logs-store'
import { useRefreshStore } from '@/store/refresh'

export function LogsPage() {
  const { t } = useTranslation(['common', 'logs'])
  const {
    filters,
    result,
    loading,
    selectedLog,
    error,
    setFilter,
    resetFilters,
    fetch,
    openDetails,
  } = useLogsStore()
  const refreshToken = useRefreshStore((state) => state.tokens.logs)

  useEffect(() => {
    void fetch()
  }, [fetch, refreshToken])

  return (
    <div className="space-y-5">
      <PageHeader
        title={t('logs:title')}
      />

      <Card className="border-foreground">
        <CardHeader className="gap-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <CardTitle>{t('common:filters')}</CardTitle>
            <div className="flex items-center gap-2">
              <Button variant="outline" onClick={() => resetFilters()}>
                {t('logs:filters.reset')}
              </Button>
              <Button onClick={() => void fetch(1)}>{t('common:refresh')}</Button>
            </div>
          </div>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          <div className="space-y-2">
            <Label htmlFor="logs-from">{t('logs:filters.from')}</Label>
            <Input
              id="logs-from"
              type="datetime-local"
              value={filters.fromLocal}
              onChange={(event) => setFilter('fromLocal', event.target.value)}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="logs-to">{t('logs:filters.to')}</Label>
            <Input
              id="logs-to"
              type="datetime-local"
              value={filters.toLocal}
              onChange={(event) => setFilter('toLocal', event.target.value)}
            />
          </div>
          <div className="space-y-2">
            <Label>{t('logs:filters.eventType')}</Label>
            <Select
              value={filters.eventType || '__all'}
              onValueChange={(value) =>
                setFilter('eventType', value === '__all' ? '' : value)
              }
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__all">{t('common:all')}</SelectItem>
                {eventTypes.map((item) => (
                  <SelectItem key={item} value={item}>
                    {translateEventType(t, item)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>{t('logs:filters.collector')}</Label>
            <Select
              value={filters.collectorId || '__all'}
              onValueChange={(value) =>
                setFilter('collectorId', value === '__all' ? '' : value)
              }
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__all">{t('common:all')}</SelectItem>
                {collectorIds.map((item) => (
                  <SelectItem key={item} value={item}>
                    {translateCollectorId(t, item)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>{t('logs:filters.severity')}</Label>
            <Select
              value={filters.severity || '__all'}
              onValueChange={(value) =>
                setFilter('severity', value === '__all' ? '' : value)
              }
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__all">{t('common:all')}</SelectItem>
                {eventSeverities.map((item) => (
                  <SelectItem key={item} value={item}>
                    {translateSeverity(t, item)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label htmlFor="logs-session">{t('logs:filters.session')}</Label>
            <Input
              id="logs-session"
              value={filters.userSessionId}
              onChange={(event) => setFilter('userSessionId', event.target.value)}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="logs-machine">{t('logs:filters.machine')}</Label>
            <Input
              id="logs-machine"
              value={filters.machineId}
              onChange={(event) => setFilter('machineId', event.target.value)}
            />
          </div>
          <div className="space-y-2">
            <Label>{t('logs:filters.pageSize')}</Label>
            <Select
              value={String(filters.pageSize)}
              onValueChange={(value) => setFilter('pageSize', Number(value))}
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {[25, 50, 100, 200].map((item) => (
                  <SelectItem key={item} value={String(item)}>
                    {String(item)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {error ? (
        <EmptyState
          title={error}
          icon={<TriangleAlert className="size-6" />}
        />
      ) : null}

      <Card>
        <CardHeader className="flex-row items-center justify-between">
          <CardTitle>{t('logs:title')}</CardTitle>
          <p className="text-sm text-muted-foreground">
            {formatNumber(result?.totalCount ?? 0)} {t('logs:totalItems')}
          </p>
        </CardHeader>
        <CardContent>
          {loading && !result ? (
            <div className="space-y-3">
              {Array.from({ length: 6 }).map((_, index) => (
                <Skeleton key={index} className="h-14 w-full" />
              ))}
            </div>
          ) : result && result.items.length > 0 ? (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t('logs:columns.time')}</TableHead>
                    <TableHead>{t('logs:columns.eventType')}</TableHead>
                    <TableHead>{t('logs:columns.collector')}</TableHead>
                    <TableHead>{t('logs:columns.severity')}</TableHead>
                    <TableHead>{t('logs:columns.session')}</TableHead>
                    <TableHead>{t('logs:columns.machine')}</TableHead>
                    <TableHead>{t('logs:columns.summary')}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {result.items.map((log) => (
                    <TableRow
                      key={log.id}
                      className="cursor-pointer"
                      onClick={() => openDetails(log)}
                    >
                      <TableCell className="font-mono text-xs">
                        {formatUtcDateTime(log.occurredAtUtc)}
                      </TableCell>
                      <TableCell>{translateEventType(t, log.eventType)}</TableCell>
                      <TableCell>{translateCollectorId(t, log.collectorId)}</TableCell>
                      <TableCell>
                        <StatusBadge
                          label={translateSeverity(t, log.severity)}
                          tone={getSeverityTone(log.severity)}
                        />
                      </TableCell>
                      <TableCell className="font-mono text-xs">
                        {log.userSessionId}
                      </TableCell>
                      <TableCell className="font-mono text-xs">
                        {log.machineId}
                      </TableCell>
                      <TableCell className="max-w-[380px]">
                        <span className="line-clamp-2 text-sm text-muted-foreground">
                          {summarizeLogEvent(log)}
                        </span>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
              <div className="mt-4 flex flex-wrap items-center justify-between gap-3">
                <p className="text-sm text-muted-foreground">
                  {t('logs:pagination.page', {
                    page: result.page,
                    total: Math.max(1, result.totalPages),
                  })}
                </p>
                <div className="flex items-center gap-2">
                  <Button
                    variant="outline"
                    disabled={result.page <= 1 || loading}
                    onClick={() => void fetch(result.page - 1)}
                  >
                    {t('logs:pagination.prev')}
                  </Button>
                  <Button
                    variant="outline"
                    disabled={result.page >= result.totalPages || loading}
                    onClick={() => void fetch(result.page + 1)}
                  >
                    {t('logs:pagination.next')}
                  </Button>
                </div>
              </div>
            </>
          ) : (
            <EmptyState
              title={t('logs:empty.title')}
              description={t('logs:empty.description')}
              icon={<FileSearch className="size-6" />}
            />
          )}
        </CardContent>
      </Card>

      <Dialog open={!!selectedLog} onOpenChange={(open) => !open && openDetails(null)}>
        <DialogContent className="max-h-[88vh] overflow-hidden">
          <DialogHeader>
            <DialogTitle>{t('logs:details.title')}</DialogTitle>
            <DialogDescription>
              {selectedLog ? `${translateEventType(t, selectedLog.eventType)} · ${formatUtcDateTime(selectedLog.occurredAtUtc)}` : ''}
            </DialogDescription>
          </DialogHeader>
          {selectedLog ? (
            <div className="grid gap-5 px-5 pb-5 md:grid-cols-2 md:px-6 md:pb-6">
              <div className="space-y-3 rounded-lg border border-border bg-background p-4">
                <div className="space-y-2 text-sm">
                  <p><span className="text-muted-foreground">{t('logs:details.collector')}:</span> {translateCollectorId(t, selectedLog.collectorId)}</p>
                  <p><span className="text-muted-foreground">{t('logs:details.severity')}:</span> {translateSeverity(t, selectedLog.severity)}</p>
                  <p><span className="text-muted-foreground">{t('logs:details.session')}:</span> <span className="font-mono text-xs">{selectedLog.userSessionId}</span></p>
                  <p><span className="text-muted-foreground">{t('logs:details.machine')}:</span> <span className="font-mono text-xs">{selectedLog.machineId}</span></p>
                </div>
              </div>
              <JsonPreview value={prettyJson(selectedLog.content)} className="h-[420px]" />
            </div>
          ) : null}
        </DialogContent>
      </Dialog>
    </div>
  )
}
