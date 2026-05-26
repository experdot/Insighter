import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { TriangleAlert } from 'lucide-react'
import { EmptyState } from '@/components/shared/EmptyState'
import { KeyValueList } from '@/components/shared/KeyValueList'
import { PageHeader } from '@/components/shared/PageHeader'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useRefreshStore } from '@/store/refresh'
import { useSettingsStore } from '@/store/settings-store'

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
      <PageHeader title={t('settings:title')} />

      {error ? (
        <EmptyState
          title={error}
          icon={<TriangleAlert className="size-6" />}
        />
      ) : null}

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1.2fr)_minmax(320px,0.8fr)]">
        <Card>
          <CardHeader>
            <CardTitle>{t('settings:editable')}</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-5">
            <div className="space-y-2">
              <Label htmlFor="retention-days">{t('settings:fields.retentionDays')}</Label>
              <Input
                id="retention-days"
                type="number"
                value={form.retentionDays}
                onChange={(event) => setField('retentionDays', event.target.value)}
                disabled={loading || saving}
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
        </div>
      </div>
    </div>
  )
}
