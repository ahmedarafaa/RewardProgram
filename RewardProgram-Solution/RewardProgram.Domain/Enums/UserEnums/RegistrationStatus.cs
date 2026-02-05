using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Domain.Enums.UserEnums;

public enum RegistrationStatus
{
    PendingSalesman = 1,        // Just registered, waiting for SalesMan
    PendingZoneManager = 2, // SalesMan approved, waiting for DM
    Approved = 3,             
    Rejected = 4               
}
