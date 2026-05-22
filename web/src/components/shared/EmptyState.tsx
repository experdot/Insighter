import type { ReactNode } from 'react'
import { Card, CardContent } from '@/components/ui/card'

type EmptyStateProps = {
  title: string
  description: string
  icon?: ReactNode
}

export function EmptyState({ title, description, icon }: EmptyStateProps) {
  return (
    <Card>
      <CardContent className="flex flex-col items-center justify-center gap-4 px-6 py-12 text-center">
        {icon ? (
          <div className="rounded-full border border-border bg-muted/70 p-4 text-muted-foreground">
            {icon}
          </div>
        ) : null}
        <div>
          <h3 className="font-heading text-xl font-semibold text-foreground">
            {title}
          </h3>
          <p className="mt-2 max-w-xl text-sm leading-6 text-muted-foreground">
            {description}
          </p>
        </div>
      </CardContent>
    </Card>
  )
}

