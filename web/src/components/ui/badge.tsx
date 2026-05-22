import { cva, type VariantProps } from 'class-variance-authority'
import type * as React from 'react'
import { cn } from '@/lib/utils'

const badgeVariants = cva(
  'inline-flex items-center rounded-full border px-2.5 py-1 text-xs font-medium',
  {
    variants: {
      variant: {
        default: 'border-transparent bg-primary/12 text-primary',
        muted: 'border-border bg-muted/80 text-muted-foreground',
        secondary: 'border-transparent bg-secondary/12 text-secondary',
        warning: 'border-transparent bg-amber-500/15 text-amber-700',
        destructive:
          'border-transparent bg-destructive/12 text-destructive',
        success: 'border-transparent bg-emerald-500/15 text-emerald-700',
        outline: 'border-border bg-transparent text-foreground',
      },
    },
    defaultVariants: {
      variant: 'default',
    },
  },
)

export interface BadgeProps
  extends React.HTMLAttributes<HTMLDivElement>,
    VariantProps<typeof badgeVariants> {}

export function Badge({ className, variant, ...props }: BadgeProps) {
  return (
    <div className={cn(badgeVariants({ variant }), className)} {...props} />
  )
}

export { badgeVariants }

