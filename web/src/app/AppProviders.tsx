import type { PropsWithChildren } from 'react'
import { Toaster } from 'sonner'
import { useI18nSync } from '@/app/hooks/useI18nSync'

export function AppProviders({ children }: PropsWithChildren) {
  useI18nSync()

  return (
    <>
      {children}
      <Toaster
        richColors
        closeButton
        position="top-right"
        toastOptions={{
          className: 'border border-border bg-card text-card-foreground',
        }}
      />
    </>
  )
}
