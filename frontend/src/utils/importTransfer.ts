// 首页选择文件后通过内存临时传递给考核记录页，不把 File 对象写入浏览器存储。
let pendingImportFile: File | null = null

export function setPendingImportFile(file: File) {
  pendingImportFile = file
}

export function consumePendingImportFile() {
  const file = pendingImportFile
  pendingImportFile = null
  return file
}
