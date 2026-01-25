using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.Interfaces;

public interface ISmsService
{
    Task<bool> SendAsync(string mobileNumber, string message);
}
