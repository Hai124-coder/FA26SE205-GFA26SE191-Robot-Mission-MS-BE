using Microsoft.AspNetCore.Mvc;

namespace RobotMissionMS.Api.Contracts;

public sealed class MapPackageUploadForm
{
    [FromForm(Name = "file")]
    public IFormFile? File { get; init; }
}
