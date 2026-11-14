namespace RobotMissionMS.Api.Authorization;

public static class AuthPolicies
{
    public const string AdminOnly = "AdminOnly";
    public const string OperatorOrAdmin = "OperatorOrAdmin";
    public const string OperationsRead = "OperationsRead";
}
