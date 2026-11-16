using Microsoft.AspNetCore.Mvc;

namespace RobotMissionMS.Api.Contracts;

public sealed class ImportUploadForm
{
    [FromForm(Name = "file")]
    public IFormFile? File { get; init; }
}
