import { Languages } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Button } from '@/components/ui/button'
import { supportedLocales } from '@/i18n/config'
import { useAppStore } from '@/store/app-store'
import type { Locale } from '@/types/api'

export function LanguageSwitcher() {
  const { t } = useTranslation(['common'])
  const locale = useAppStore((state) => state.locale)
  const setLocale = useAppStore((state) => state.setLocale)

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="outline" size="sm">
          <Languages className="size-4" />
          {t(`common:languages.${locale}`)}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuRadioGroup
          value={locale}
          onValueChange={(value) => setLocale(value as Locale)}
        >
          {supportedLocales.map((item) => (
            <DropdownMenuRadioItem key={item} value={item}>
              {t(`common:languages.${item}`)}
            </DropdownMenuRadioItem>
          ))}
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

