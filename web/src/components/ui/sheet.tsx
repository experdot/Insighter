import * as React from 'react'
import * as DialogPrimitive from '@radix-ui/react-dialog'
import { cva, type VariantProps } from 'class-variance-authority'
import { X } from 'lucide-react'
import { cn } from '@/lib/utils'

const sheetVariants = cva(
  'fixed z-50 bg-card text-card-foreground shadow-[0_30px_70px_rgba(44,31,13,0.24)] transition-transform',
  {
    variants: {
      side: {
        top: 'inset-x-0 top-0 rounded-b-[28px] border-b border-border data-[state=closed]:-translate-y-full data-[state=open]:translate-y-0',
        bottom:
          'inset-x-0 bottom-0 rounded-t-[28px] border-t border-border data-[state=closed]:translate-y-full data-[state=open]:translate-y-0',
        left: 'inset-y-0 left-0 h-full w-[min(90vw,440px)] rounded-r-[28px] border-r border-border data-[state=closed]:-translate-x-full data-[state=open]:translate-x-0',
        right:
          'inset-y-0 right-0 h-full w-[min(90vw,520px)] rounded-l-[28px] border-l border-border data-[state=closed]:translate-x-full data-[state=open]:translate-x-0',
      },
    },
    defaultVariants: {
      side: 'right',
    },
  },
)

export const Sheet = DialogPrimitive.Root
export const SheetTrigger = DialogPrimitive.Trigger
export const SheetClose = DialogPrimitive.Close
export const SheetPortal = DialogPrimitive.Portal

export const SheetOverlay = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Overlay>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Overlay>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Overlay
    ref={ref}
    className={cn(
      'fixed inset-0 z-50 bg-black/30 backdrop-blur-sm transition-opacity data-[state=closed]:opacity-0 data-[state=open]:opacity-100',
      className,
    )}
    {...props}
  />
))

SheetOverlay.displayName = DialogPrimitive.Overlay.displayName

export interface SheetContentProps
  extends React.ComponentPropsWithoutRef<typeof DialogPrimitive.Content>,
    VariantProps<typeof sheetVariants> {}

export const SheetContent = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Content>,
  SheetContentProps
>(({ side = 'right', className, children, ...props }, ref) => (
  <SheetPortal>
    <SheetOverlay />
    <DialogPrimitive.Content
      ref={ref}
      className={cn(sheetVariants({ side }), className)}
      {...props}
    >
      {children}
      <DialogPrimitive.Close className="absolute right-4 top-4 rounded-full p-2 text-muted-foreground transition-colors hover:bg-accent/70 hover:text-foreground">
        <X className="size-4" />
        <span className="sr-only">Close</span>
      </DialogPrimitive.Close>
    </DialogPrimitive.Content>
  </SheetPortal>
))

SheetContent.displayName = DialogPrimitive.Content.displayName

export function SheetHeader({
  className,
  ...props
}: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div className={cn('flex flex-col gap-2 px-5 pt-5 md:px-6 md:pt-6', className)} {...props} />
  )
}

export function SheetFooter({
  className,
  ...props
}: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn('mt-auto flex flex-col-reverse gap-2 px-5 pb-5 pt-4 sm:flex-row sm:justify-end md:px-6 md:pb-6', className)}
      {...props}
    />
  )
}

export const SheetTitle = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Title>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Title>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Title
    ref={ref}
    className={cn('panel-heading text-xl', className)}
    {...props}
  />
))

SheetTitle.displayName = DialogPrimitive.Title.displayName

export const SheetDescription = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Description>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Description>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Description
    ref={ref}
    className={cn('text-sm leading-6 text-muted-foreground', className)}
    {...props}
  />
))

SheetDescription.displayName = DialogPrimitive.Description.displayName

