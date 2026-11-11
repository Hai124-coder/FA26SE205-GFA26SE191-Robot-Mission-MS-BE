using System;
using System.Collections.Generic;

namespace RobotMissionMS.DataAccess.Entities;

public partial class RobotType
{
    public Guid Id { get; set; }

    public string TypeCode { get; set; } = null!;

    public string Name { get; set; } = null!;

    public int Slots { get; set; }

    public string ColorHex { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<MissionType> MissionTypes { get; set; } = new List<MissionType>();

    public virtual ICollection<Robot> Robots { get; set; } = new List<Robot>();
}
