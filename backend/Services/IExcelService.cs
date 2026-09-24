using FineManagement.Api.DTOs.Assessments;

namespace FineManagement.Api.Services;

/// <summary>考核记录 Excel 暂存导入、确认入库及导出服务。</summary>
public interface IExcelService
{
    Task<ExcelImportResult> StageAssessmentRecordsAsync(Stream stream, long? userId, CancellationToken cancellationToken);
    Task<ExcelImportConfirmResult> ConfirmAssessmentRecordsAsync(long batchId, long? userId, CancellationToken cancellationToken);
    Task<byte[]> ExportAssessmentRecordsAsync(AssessmentRecordQuery query, CancellationToken cancellationToken);
}
