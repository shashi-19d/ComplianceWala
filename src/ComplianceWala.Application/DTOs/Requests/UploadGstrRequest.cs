namespace ComplianceWala.Application.DTOs.Requests;

/// <summary>
/// Request to upload and process a GSTR JSON file.
/// JsonContent is the raw string from the GST portal export.
/// </summary>
public record UploadGstrRequest(
    string JsonContent
);