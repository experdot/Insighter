import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

type PageHeaderProps = {
  eyebrow: string
  title: string
  description: string
  actions?: ReactNode
  className?: string
}

export function PageHeader({
  eyebrow,
  title,
  description,
  actions,
  className,
}: PageHeaderProps) {
  return (
    <div
      className={cn(
        'flex flex-col gap-6 rounded-[28px] border border-border bg-card/60 p-5 shadow-[0_16px_36px_rgba(79,56,23,0.08)] backdrop-blur-sm md:flex-row md:items-end md:justify-between md:p-6',
        className,
      )}
    >
      <div className="max-w-3xl">
        <p className="mb-2 text-xs font-semibold uppercase tracking-[0.24em] text-secondary">
          {eyebrow}
        </p>
        <h1 className="page-heading">{title}</h1>
        <p className="mt-3 max-w-2xl text-sm leading-7 text-muted-foreground md:text-base">
          {description}
        </p>
      </div>
      {actions ? <div className="flex items-center gap-3">{actions}</div> : null}
    </div>
  )
}

