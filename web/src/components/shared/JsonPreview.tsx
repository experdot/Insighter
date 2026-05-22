import { ScrollArea } from '@/components/ui/scroll-area'
import { cn } from '@/lib/utils'

type JsonPreviewProps = {
  value: string
  className?: string
}

export function JsonPreview({ value, className }: JsonPreviewProps) {
  return (
    <ScrollArea
      className={cn(
        'rounded-[24px] border border-border bg-[#fffaf3]/80 p-4',
        className,
      )}
    >
      <pre className="font-mono text-xs leading-6 text-foreground">{value}</pre>
    </ScrollArea>
  )
}

