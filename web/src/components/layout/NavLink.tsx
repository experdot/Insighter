import type { LucideIcon } from 'lucide-react'
import { NavLink as RouterNavLink } from 'react-router-dom'
import { cn } from '@/lib/utils'

type NavLinkProps = {
  to: string
  label: string
  icon: LucideIcon
}

export function NavLink({ to, label, icon: Icon }: NavLinkProps) {
  return (
    <RouterNavLink
      to={to}
      end={to === '/'}
      className={({ isActive }) =>
        cn(
          'group flex items-center gap-3 rounded-2xl px-3 py-3 text-sm font-medium text-muted-foreground transition-colors',
          isActive
            ? 'bg-primary text-primary-foreground shadow-[0_16px_30px_rgba(199,125,43,0.26)]'
            : 'hover:bg-accent/70 hover:text-foreground',
        )
      }
    >
      <Icon className="size-4 shrink-0" />
      <span>{label}</span>
    </RouterNavLink>
  )
}

