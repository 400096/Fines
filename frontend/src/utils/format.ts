export function formatDateTime(value?: string | null) {
  if (!value) return '—'
  return new Intl.DateTimeFormat('zh-CN', {
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', hour12: false,
  }).format(new Date(value)).replaceAll('/', '-')
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
