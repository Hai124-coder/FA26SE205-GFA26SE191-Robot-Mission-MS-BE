namespace RobotMissionMS.Api.Authorization;

public static class AuthPolicies
{
    public const string AdminOnly = "AdminOnly";
    public const string OperatorOnly = "OperatorOnly";
    public const string OperatorOrAdmin = "OperatorOrAdmin";
    public const string OperationsRead = "OperationsRead";
    public const string SupervisorOnly = "SupervisorOnly";
    public const string SupervisorOrAdmin = "SupervisorOrAdmin";
}
