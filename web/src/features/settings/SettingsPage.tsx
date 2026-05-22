import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { LockKeyhole, TriangleAlert } from 'lucide-react'
import { EmptyState } from '@/components/shared/EmptyState'
import { KeyValueList } from '@/components/shared/KeyValueList'
import { PageHeader } from '@/components/shared/PageHeader'
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
import { Textarea } from '@/components/ui/textarea'
import { clipboardCaptureModes } from '@/lib/catalog'
import { translateClipboardMode } from '@/lib/presenters'
import { useRefreshStore } from '@/store/refresh'
import { useSettingsStore } from '@/store/settings-store'
import type { ClipboardCaptureMode } from '@/types/api'

export function SettingsPage() {
  const { t } = useTranslation(['common', 'settings'])
  const {
    snapshot,
    form,
    loading,
    saving,
    error,
    setField,
    fetch,
    save,
  } = useSettingsStore()
  const refreshToken = useRefreshStore((state) => state.tokens.settings)

  useEffect(() => {
    void fetch()
  }, [fetch, refreshToken])

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow={t('settings:eyebrow')}
        title={t('settings:title')}
        description={t('settings:description')}
      />

      {error ? (
        <EmptyState
          title={error}
          description={t('settings:privacyDescription')}
          icon={<TriangleAlert className="size-6" />}
        />
      ) : null}

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1.2fr)_minmax(320px,0.8fr)]">
        <Card>
          <CardHeader>
            <CardTitle>{t('settings:editable')}</CardTitle>
            <CardDescription>{t('settings:privacyDescription')}</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-5">
            <div className="space-y-2">
              <Label htmlFor="retention-days">{t('settings:fields.retentionDays')}</Label>
              <Input
                id="retention-days"
                type="number"
                value={form.retentionDays}
                onChange={(event) => setField('retentionDays', event.target.value)}
                disabled={loading}
              />
            </div>
            <div className="space-y-2">
              <Label>{t('settings:fields.defaultClipboardMode')}</Label>
              <Select
                value={form.defaultClipboardMode}
                onValueChange={(value) =>
                  setField('defaultClipboardMode', value as ClipboardCaptureMode)
                }
                disabled={loading}
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
              <Label htmlFor="clipboard-max-length">
                {t('settings:fields.defaultClipboardMaxLength')}
              </Label>
              <Input
                id="clipboard-max-length"
                type="number"
                value={form.defaultClipboardMaxLength}
                onChange={(event) =>
                  setField('defaultClipboardMaxLength', event.target.value)
                }
                disabled={loading}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="window-title-max-length">
                {t('settings:fields.defaultWindowTitleMaxLength')}
              </Label>
              <Input
                id="window-title-max-length"
                type="number"
                value={form.defaultWindowTitleMaxLength}
                onChange={(event) =>
                  setField('defaultWindowTitleMaxLength', event.target.value)
                }
                disabled={loading}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="window-blacklist">
                {t('settings:fields.windowProcessBlacklist')}
              </Label>
              <Textarea
                id="window-blacklist"
                value={form.windowProcessBlacklist}
                onChange={(event) =>
                  setField('windowProcessBlacklist', event.target.value)
                }
                disabled={loading}
              />
            </div>
            <div className="flex justify-end">
              <Button onClick={() => void save()} disabled={loading || saving}>
                {t('common:save')}
              </Button>
            </div>
          </CardContent>
        </Card>

        <div className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle>{t('settings:readonly')}</CardTitle>
            </CardHeader>
            <CardContent>
              {snapshot ? (
                <KeyValueList
                  items={[
                    {
                      label: t('settings:fields.databasePath'),
                      value: snapshot.databasePath,
                    },
                    {
                      label: t('settings:fields.apiUrls'),
                      value: snapshot.apiUrls,
                    },
                    {
                      label: t('settings:fields.queueCapacity'),
                      value: String(snapshot.queueCapacity),
                    },
                    {
                      label: t('settings:fields.queryMaxPageSize'),
                      value: String(snapshot.queryMaxPageSize),
                    },
                    {
                      label: t('settings:fields.autoStartCollectors'),
                      value: snapshot.autoStartCollectors ? t('common:yes') : t('common:no'),
                    },
                  ]}
                />
              ) : (
                <div className="text-sm text-muted-foreground">{t('common:loading')}…</div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>{t('settings:privacy')}</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="flex items-start gap-3 rounded-[24px] border border-border/70 bg-card/55 p-4">
                <div className="rounded-2xl bg-secondary/12 p-3 text-secondary">
                  <LockKeyhole className="size-5" />
                </div>
                <p className="text-sm leading-7 text-muted-foreground">
                  {t('settings:privacyDescription')}
                </p>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  )
}
