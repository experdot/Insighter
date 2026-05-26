import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { RadioTower, Save, TriangleAlert } from 'lucide-react'
import { EmptyState } from '@/components/shared/EmptyState'
import { PageHeader } from '@/components/shared/PageHeader'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  Sheet,
  SheetContent,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet'
import { Switch } from '@/components/ui/switch'
import { Textarea } from '@/components/ui/textarea'
import { clipboardCaptureModes } from '@/lib/catalog'
import { formatRelative, formatUtcDateTime, toStringList, toTextareaList } from '@/lib/format'
import {
  booleanSetting,
  getCapabilityTone,
  getCollectorSettings,
  getCollectorStatusTone,
  numberSetting,
  stringArraySetting,
  stringSetting,
  translateCapability,
  translateClipboardMode,
  translateCollectorId,
  translateCollectorStatus,
} from '@/lib/presenters'
import { useCollectorsStore } from '@/store/collectors-store'
import { useRefreshStore } from '@/store/refresh'
import type { ClipboardCaptureMode, CollectorResponse } from '@/types/api'

type CollectorConfigForm = {
  enabled: boolean
  titleMaxLength: string
  processBlacklist: string
  idleThresholdSeconds: string
  ignoreWhileLocked: boolean
  captureMode: ClipboardCaptureMode
  maxContentLength: string
  textOnly: boolean
  captureExecutablePath: boolean
  ignoreCurrentProcess: boolean
  ignoreSystemProcesses: boolean
  batteryLevelStepPercent: string
  reportPowerSourceChanges: boolean
  reportChargingStateChanges: boolean
  reportBatteryLevelChanges: boolean
  powerPollIntervalSeconds: string
  networkPollIntervalSeconds: string
  reportActiveNetworkChanges: boolean
}

function createCollectorForm(collector?: CollectorResponse | null): CollectorConfigForm {
  const settings = getCollectorSettings(collector ?? undefined)
  return {
    enabled: collector?.enabled ?? true,
    titleMaxLength: String(numberSetting(settings, 'titleMaxLength', 200) || 200),
    processBlacklist: toTextareaList(stringArraySetting(settings, 'processBlacklist')),
    idleThresholdSeconds: String(numberSetting(settings, 'idleThresholdSeconds', 300) || 300),
    ignoreWhileLocked: booleanSetting(settings, 'ignoreWhileLocked', true),
    captureMode: (stringSetting(settings, 'captureMode', 'Preview') || 'Preview') as ClipboardCaptureMode,
    maxContentLength: String(numberSetting(settings, 'maxContentLength', 200) || 200),
    textOnly: booleanSetting(settings, 'textOnly', true),
    captureExecutablePath: booleanSetting(settings, 'captureExecutablePath', true),
    ignoreCurrentProcess: booleanSetting(settings, 'ignoreCurrentProcess', true),
    ignoreSystemProcesses: booleanSetting(settings, 'ignoreSystemProcesses', true),
    batteryLevelStepPercent: String(numberSetting(settings, 'batteryLevelStepPercent', 5) || 5),
    reportPowerSourceChanges: booleanSetting(settings, 'reportPowerSourceChanges', true),
    reportChargingStateChanges: booleanSetting(settings, 'reportChargingStateChanges', true),
    reportBatteryLevelChanges: booleanSetting(settings, 'reportBatteryLevelChanges', true),
    powerPollIntervalSeconds: String(numberSetting(settings, 'pollIntervalSeconds', 15) || 15),
    networkPollIntervalSeconds: String(numberSetting(settings, 'pollIntervalSeconds', 5) || 5),
    reportActiveNetworkChanges: booleanSetting(settings, 'reportActiveNetworkChanges', true),
  }
}

export function CollectorsPage() {
  const { t } = useTranslation(['common', 'collectors'])
  const {
    items,
    loading,
    mutatingId,
    selectedCollectorId,
    error,
    fetch,
    startCollector,
    stopCollector,
    saveCollectorConfig,
    openSheet,
  } = useCollectorsStore()
  const refreshToken = useRefreshStore((state) => state.tokens.collectors)
  const selectedCollector =
    items.find((item) => item.collectorId === selectedCollectorId) ?? null
  const [form, setForm] = useState<CollectorConfigForm>(createCollectorForm())

  useEffect(() => {
    void fetch()
  }, [fetch, refreshToken])

  useEffect(() => {
    const timer = window.setInterval(() => {
      void fetch()
    }, 15000)

    return () => window.clearInterval(timer)
  }, [fetch])

  const handleSubmit = async () => {
    if (!selectedCollector) {
      return
    }

    const settings: Record<string, unknown> = {}
    if (selectedCollector.collectorId === 'ForegroundWindowCollector') {
      settings.titleMaxLength = Number(form.titleMaxLength)
      settings.processBlacklist = toStringList(form.processBlacklist)
    }

    if (selectedCollector.collectorId === 'IdleCollector') {
      settings.idleThresholdSeconds = Number(form.idleThresholdSeconds)
      settings.ignoreWhileLocked = form.ignoreWhileLocked
    }

    if (selectedCollector.collectorId === 'ClipboardCollector') {
      settings.captureMode = form.captureMode
      settings.maxContentLength = Number(form.maxContentLength)
      settings.textOnly = form.textOnly
    }

    if (selectedCollector.collectorId === 'ProcessLifecycleCollector') {
      settings.captureExecutablePath = form.captureExecutablePath
      settings.ignoreCurrentProcess = form.ignoreCurrentProcess
      settings.ignoreSystemProcesses = form.ignoreSystemProcesses
    }

    if (selectedCollector.collectorId === 'PowerCollector') {
      settings.batteryLevelStepPercent = Number(form.batteryLevelStepPercent)
      settings.reportPowerSourceChanges = form.reportPowerSourceChanges
      settings.reportChargingStateChanges = form.reportChargingStateChanges
      settings.reportBatteryLevelChanges = form.reportBatteryLevelChanges
      settings.pollIntervalSeconds = Number(form.powerPollIntervalSeconds)
    }

    if (selectedCollector.collectorId === 'NetworkConnectivityCollector') {
      settings.pollIntervalSeconds = Number(form.networkPollIntervalSeconds)
      settings.reportActiveNetworkChanges = form.reportActiveNetworkChanges
    }

    await saveCollectorConfig(selectedCollector.collectorId, {
      enabled: form.enabled,
      version: selectedCollector.configVersion,
      settings,
    })
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('collectors:title')}
      />

      {error ? (
        <EmptyState
          title={error}
          icon={<TriangleAlert className="size-6" />}
        />
      ) : null}

      {!loading && items.length === 0 ? (
        <EmptyState
          title={t('collectors:empty.title')}
          description={t('collectors:empty.description')}
          icon={<RadioTower className="size-6" />}
        />
      ) : null}

      <div className="grid gap-5 xl:grid-cols-2">
        {items.map((collector) => (
          <Card key={collector.collectorId}>
            <CardHeader>
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <CardTitle>
                    {translateCollectorId(t, collector.collectorId, collector.displayName)}
                  </CardTitle>
                  <CardDescription>{collector.displayName}</CardDescription>
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
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-3 text-sm">
                <div className="flex items-center justify-between gap-3">
                  <span className="text-muted-foreground">{t('collectors:card.lastHeartbeat')}</span>
                  <span className="font-mono text-xs text-foreground">
                    {collector.lastHeartbeatUtc
                      ? `${formatRelative(collector.lastHeartbeatUtc)} (${formatUtcDateTime(collector.lastHeartbeatUtc)})`
                      : '—'}
                  </span>
                </div>
                <div className="flex items-center justify-between gap-3">
                  <span className="text-muted-foreground">{t('collectors:card.version')}</span>
                  <span className="font-mono text-xs text-foreground">{collector.configVersion}</span>
                </div>
                <div className="flex items-center justify-between gap-3">
                  <span className="text-muted-foreground">{t('collectors:card.latestError')}</span>
                  <span className="max-w-[60%] text-right text-xs text-foreground">
                    {collector.lastError ?? '—'}
                  </span>
                </div>
              </div>

              <div className="flex flex-wrap gap-2">
                <Button
                  variant="secondary"
                  disabled={mutatingId === collector.collectorId || collector.status === 'Running'}
                  onClick={() => void startCollector(collector.collectorId)}
                >
                  {t('collectors:actions.start')}
                </Button>
                <Button
                  variant="outline"
                  disabled={mutatingId === collector.collectorId || collector.status === 'Disabled'}
                  onClick={() => void stopCollector(collector.collectorId)}
                >
                  {t('collectors:actions.stop')}
                </Button>
                <Button
                  variant="outline"
                  onClick={() => {
                    setForm(createCollectorForm(collector))
                    openSheet(collector.collectorId)
                  }}
                >
                  {t('collectors:actions.configure')}
                </Button>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      <Sheet
        open={!!selectedCollector}
        onOpenChange={(open) => !open && openSheet(null)}
      >
        <SheetContent side="right" className="flex flex-col overflow-y-auto">
          <SheetHeader>
            <SheetTitle>{t('collectors:sheet.title')}</SheetTitle>
          </SheetHeader>

          {selectedCollector ? (
            <div className="grid gap-5 px-5 py-5 md:px-6">
              <div className="rounded-[24px] border border-border/70 bg-card/55 p-4">
                <div className="flex items-center justify-between gap-4">
                  <div>
                    <h3 className="font-heading text-xl font-semibold">
                      {translateCollectorId(t, selectedCollector.collectorId, selectedCollector.displayName)}
                    </h3>
                    <p className="mt-1 text-sm text-muted-foreground">
                      {selectedCollector.displayName}
                    </p>
                  </div>
                  <Switch
                    checked={form.enabled}
                    onCheckedChange={(checked) =>
                      setForm((current) => ({ ...current, enabled: checked }))
                    }
                  />
                </div>
              </div>

              {selectedCollector.collectorId === 'ForegroundWindowCollector' ? (
                <>
                  <div className="space-y-2">
                    <Label htmlFor="fw-title-max">
                      {t('collectors:fields.titleMaxLength')}
                    </Label>
                    <Input
                      id="fw-title-max"
                      type="number"
                      value={form.titleMaxLength}
                      onChange={(event) =>
                        setForm((current) => ({
                          ...current,
                          titleMaxLength: event.target.value,
                        }))
                      }
                    />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="fw-blacklist">
                      {t('collectors:fields.processBlacklist')}
                    </Label>
                    <Textarea
                      id="fw-blacklist"
                      value={form.processBlacklist}
                      onChange={(event) =>
                        setForm((current) => ({
                          ...current,
                          processBlacklist: event.target.value,
                        }))
                      }
                    />
                  </div>
                </>
              ) : null}

              {selectedCollector.collectorId === 'IdleCollector' ? (
                <>
                  <div className="space-y-2">
                    <Label htmlFor="idle-threshold">
                      {t('collectors:fields.idleThresholdSeconds')}
                    </Label>
                    <Input
                      id="idle-threshold"
                      type="number"
                      value={form.idleThresholdSeconds}
                      onChange={(event) =>
                        setForm((current) => ({
                          ...current,
                          idleThresholdSeconds: event.target.value,
                        }))
                      }
                    />
                  </div>
                  <div className="flex items-center justify-between rounded-[24px] border border-border/70 bg-card/55 p-4">
                    <div>
                      <p className="font-medium text-foreground">
                        {t('collectors:fields.ignoreWhileLocked')}
                      </p>
                    </div>
                    <Switch
                      checked={form.ignoreWhileLocked}
                      onCheckedChange={(checked) =>
                        setForm((current) => ({
                          ...current,
                          ignoreWhileLocked: checked,
                        }))
                      }
                    />
                  </div>
                </>
              ) : null}

              {selectedCollector.collectorId === 'ClipboardCollector' ? (
                <>
                  <div className="space-y-2">
                    <Label>{t('collectors:fields.captureMode')}</Label>
                    <Select
                      value={form.captureMode}
                      onValueChange={(value) =>
                        setForm((current) => ({
                          ...current,
                          captureMode: value as ClipboardCaptureMode,
                        }))
                      }
                    >
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {clipboardCaptureModes.map((item) => (
                          <SelectItem key={item} value={item}>
                            {translateClipboardMode(t, item)}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="clipboard-max">
                      {t('collectors:fields.maxContentLength')}
                    </Label>
                    <Input
                      id="clipboard-max"
                      type="number"
                      value={form.maxContentLength}
                      onChange={(event) =>
                        setForm((current) => ({
                          ...current,
                          maxContentLength: event.target.value,
                        }))
                      }
                    />
                  </div>
                  <div className="flex items-center justify-between rounded-[24px] border border-border/70 bg-card/55 p-4">
                    <div>
                      <p className="font-medium text-foreground">
                        {t('collectors:fields.textOnly')}
                      </p>
                    </div>
                    <Switch
                      checked={form.textOnly}
                      onCheckedChange={(checked) =>
                        setForm((current) => ({
                          ...current,
                          textOnly: checked,
                        }))
                      }
                    />
                  </div>
                </>
              ) : null}

              {selectedCollector.collectorId === 'ProcessLifecycleCollector' ? (
                <>
                  <div className="flex items-center justify-between rounded-[24px] border border-border/70 bg-card/55 p-4">
                    <div>
                      <p className="font-medium text-foreground">
                        {t('collectors:fields.captureExecutablePath')}
                      </p>
                    </div>
                    <Switch
                      checked={form.captureExecutablePath}
                      onCheckedChange={(checked) =>
                        setForm((current) => ({
                          ...current,
                          captureExecutablePath: checked,
                        }))
                      }
                    />
                  </div>
                  <div className="flex items-center justify-between rounded-[24px] border border-border/70 bg-card/55 p-4">
                    <div>
                      <p className="font-medium text-foreground">
                        {t('collectors:fields.ignoreCurrentProcess')}
                      </p>
                    </div>
                    <Switch
                      checked={form.ignoreCurrentProcess}
                      onCheckedChange={(checked) =>
                        setForm((current) => ({
                          ...current,
                          ignoreCurrentProcess: checked,
                        }))
                      }
                    />
                  </div>
                  <div className="flex items-center justify-between rounded-[24px] border border-border/70 bg-card/55 p-4">
                    <div>
                      <p className="font-medium text-foreground">
                        {t('collectors:fields.ignoreSystemProcesses')}
                      </p>
                    </div>
                    <Switch
                      checked={form.ignoreSystemProcesses}
                      onCheckedChange={(checked) =>
                        setForm((current) => ({
                          ...current,
                          ignoreSystemProcesses: checked,
                        }))
                      }
                    />
                  </div>
                </>
              ) : null}

              {selectedCollector.collectorId === 'PowerCollector' ? (
                <>
                  <div className="space-y-2">
                    <Label htmlFor="power-battery-step">
                      {t('collectors:fields.batteryLevelStepPercent')}
                    </Label>
                    <Input
                      id="power-battery-step"
                      type="number"
                      value={form.batteryLevelStepPercent}
                      onChange={(event) =>
                        setForm((current) => ({
                          ...current,
                          batteryLevelStepPercent: event.target.value,
                        }))
                      }
                    />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="power-poll-interval">
                      {t('collectors:fields.pollIntervalSeconds')}
                    </Label>
                    <Input
                      id="power-poll-interval"
                      type="number"
                      value={form.powerPollIntervalSeconds}
                      onChange={(event) =>
                        setForm((current) => ({
                          ...current,
                          powerPollIntervalSeconds: event.target.value,
                        }))
                      }
                    />
                  </div>
                  <div className="flex items-center justify-between rounded-[24px] border border-border/70 bg-card/55 p-4">
                    <div>
                      <p className="font-medium text-foreground">
                        {t('collectors:fields.reportPowerSourceChanges')}
                      </p>
                    </div>
                    <Switch
                      checked={form.reportPowerSourceChanges}
                      onCheckedChange={(checked) =>
                        setForm((current) => ({
                          ...current,
                          reportPowerSourceChanges: checked,
                        }))
                      }
                    />
                  </div>
                  <div className="flex items-center justify-between rounded-[24px] border border-border/70 bg-card/55 p-4">
                    <div>
                      <p className="font-medium text-foreground">
                        {t('collectors:fields.reportChargingStateChanges')}
                      </p>
                    </div>
                    <Switch
                      checked={form.reportChargingStateChanges}
                      onCheckedChange={(checked) =>
                        setForm((current) => ({
                          ...current,
                          reportChargingStateChanges: checked,
                        }))
                      }
                    />
                  </div>
                  <div className="flex items-center justify-between rounded-[24px] border border-border/70 bg-card/55 p-4">
                    <div>
                      <p className="font-medium text-foreground">
                        {t('collectors:fields.reportBatteryLevelChanges')}
                      </p>
                    </div>
                    <Switch
                      checked={form.reportBatteryLevelChanges}
                      onCheckedChange={(checked) =>
                        setForm((current) => ({
                          ...current,
                          reportBatteryLevelChanges: checked,
                        }))
                      }
                    />
                  </div>
                </>
              ) : null}

              {selectedCollector.collectorId === 'NetworkConnectivityCollector' ? (
                <>
                  <div className="space-y-2">
                    <Label htmlFor="network-poll-interval">
                      {t('collectors:fields.pollIntervalSeconds')}
                    </Label>
                    <Input
                      id="network-poll-interval"
                      type="number"
                      value={form.networkPollIntervalSeconds}
                      onChange={(event) =>
                        setForm((current) => ({
                          ...current,
                          networkPollIntervalSeconds: event.target.value,
                        }))
                      }
                    />
                  </div>
                  <div className="flex items-center justify-between rounded-[24px] border border-border/70 bg-card/55 p-4">
                    <div>
                      <p className="font-medium text-foreground">
                        {t('collectors:fields.reportActiveNetworkChanges')}
                      </p>
                    </div>
                    <Switch
                      checked={form.reportActiveNetworkChanges}
                      onCheckedChange={(checked) =>
                        setForm((current) => ({
                          ...current,
                          reportActiveNetworkChanges: checked,
                        }))
                      }
                    />
                  </div>
                </>
              ) : null}
            </div>
          ) : null}

          <SheetFooter>
            <Button variant="outline" onClick={() => openSheet(null)}>
              {t('common:cancel')}
            </Button>
            <Button
              onClick={() => void handleSubmit()}
              disabled={!selectedCollector || mutatingId === selectedCollector.collectorId}
            >
              <Save className="size-4" />
              {t('common:save')}
            </Button>
          </SheetFooter>
        </SheetContent>
      </Sheet>
    </div>
  )
}
