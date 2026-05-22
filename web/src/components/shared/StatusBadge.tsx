import { Badge } from '@/components/ui/badge'
import type { BadgeTone } from '@/lib/presenters'

type StatusBadgeProps = {
  label: string
  tone?: BadgeTone
  className?: string
}

export function StatusBadge({
  label,
  tone = 'default',
  className,
}: StatusBadgeProps) {
  return (
    <Badge variant={tone} className={className}>
      {label}
    </Badge>
  )
}

