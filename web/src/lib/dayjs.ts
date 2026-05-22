import dayjs from 'dayjs'
import 'dayjs/locale/zh-cn'
import duration from 'dayjs/plugin/duration'
import localizedFormat from 'dayjs/plugin/localizedFormat'
import relativeTime from 'dayjs/plugin/relativeTime'
import utc from 'dayjs/plugin/utc'

dayjs.extend(utc)
dayjs.extend(localizedFormat)
dayjs.extend(relativeTime)
dayjs.extend(duration)

export default dayjs
