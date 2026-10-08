export function formatDateTime(value?: string | null) {
  if (!value) return '—'
  // MySQL DATETIME 读出的 UTC 时间可能没有时区后缀，补上 Z 后按北京时间显示。
  const utcValue = /(Z|[+-]\d{2}:?\d{2})$/i.test(value) ? value : `${value}Z`
  const date = new Date(utcValue)
  if (Number.isNaN(date.getTime())) return '—'
  return new Intl.DateTimeFormat('zh-CN', {
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', hour12: false,
    timeZone: 'Asia/Shanghai',
  }).format(date).replaceAll('/', '-')
}

// 页面共用的日期、金额显示格式，避免各页面产生不同格式。
export function formatMoney(value: number) {
  return new Intl.NumberFormat('zh-CN', { maximumFractionDigits: 2 }).format(value)
}

export function todayString() {
  const date = new Date()
  const offset = date.getTimezoneOffset() * 60_000
  return new Date(date.getTime() - offset).toISOString().slice(0, 10)
}
