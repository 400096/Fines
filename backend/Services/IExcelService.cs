using FineManagement.Api.DTOs.Assessments;

namespace FineManagement.Api.Services;

public interface IExcelService
{
    Task<ExcelImportResult> StageAssessmentRecordsAsync(Stream stream, long? userId, CancellationToken cancellationToken);
    Task<ExcelImportConfirmResult> ConfirmAssessmentRecordsAsync(long batchId, long? userId, CancellationToken cancellationToken);
    Task<byte[]> ExportAssessmentRecordsAsync(AssessmentRecordQuery query, CancellationToken cancellationToken);
}
