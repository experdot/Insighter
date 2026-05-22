import type { ReactNode } from 'react'
import { Card, CardContent } from '@/components/ui/card'
import { cn } from '@/lib/utils'

type StatCardProps = {
  label: string
  value: string
  hint?: string
  icon?: ReactNode
  accent?: 'primary' | 'secondary' | 'muted'
}

export function StatCard({
  label,
  value,
  hint,
  icon,
  accent = 'primary',
}: StatCardProps) {
  return (
    <Card
      className={cn(
        'relative overflow-hidden',
        accent === 'primary' && 'before:bg-primary/12',
        accent === 'secondary' && 'before:bg-secondary/12',
        accent === 'muted' && 'before:bg-muted/80',
        'before:absolute before:inset-x-0 before:top-0 before:h-1',
      )}
    >
      <CardContent className="flex items-start justify-between gap-4 px-5 py-5 md:px-6 md:py-6">
        <div>
          <p className="text-sm text-muted-foreground">{label}</p>
          <p className="mt-3 font-heading text-3xl font-bold tracking-[-0.04em] text-foreground">
            {value}
          </p>
          {hint ? (
            <p className="mt-2 text-xs uppercase tracking-[0.16em] text-muted-foreground">
              {hint}
            </p>
          ) : null}
        </div>
        {icon ? (
          <div className="rounded-2xl border border-border bg-card/80 p-3 text-primary">
            {icon}
          </div>
        ) : null}
      </CardContent>
    </Card>
  )
}

