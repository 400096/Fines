let pendingImportFile: File | null = null

export function setPendingImportFile(file: File) {
  pendingImportFile = file
}

export function consumePendingImportFile() {
  const file = pendingImportFile
  pendingImportFile = null
  return file
}
