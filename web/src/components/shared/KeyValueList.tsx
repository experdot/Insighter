type KeyValueListProps = {
  items: Array<{ label: string; value: string }>
}

export function KeyValueList({ items }: KeyValueListProps) {
  return (
    <dl className="grid gap-4">
      {items.map((item) => (
        <div
          key={item.label}
          className="grid gap-1 rounded-2xl border border-border/70 bg-card/50 p-4"
        >
          <dt className="text-xs font-semibold uppercase tracking-[0.16em] text-muted-foreground">
            {item.label}
          </dt>
          <dd className="break-all font-mono text-sm text-foreground">
            {item.value}
          </dd>
        </div>
      ))}
    </dl>
  )
}

