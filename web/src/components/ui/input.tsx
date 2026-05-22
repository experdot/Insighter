import * as React from 'react'
import { cn } from '@/lib/utils'

export const Input = React.forwardRef<
  HTMLInputElement,
  React.InputHTMLAttributes<HTMLInputElement>
>(({ className, type, ...props }, ref) => {
  return (
    <input
      type={type}
      className={cn(
        'flex h-10 w-full rounded-2xl border border-border bg-input px-3 py-2 text-sm text-foreground shadow-sm outline-none transition-colors placeholder:text-muted-foreground focus-visible:ring-2 focus-visible:ring-ring/30',
        className,
      )}
      ref={ref}
      {...props}
    />
  )
})

Input.displayName = 'Input'

