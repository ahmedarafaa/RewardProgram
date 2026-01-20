using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Domain.Enums.UserEnums;

public enum RegistrationStatus
{
    PendingSalesman,        // Just registered, waiting for SalesMan
    PendingDistrictManager, // SalesMan approved, waiting for DM
    PendingAdmin,           // DM approved, waiting for Admin
    Approved,               // Admin approved
    Rejected                
}
