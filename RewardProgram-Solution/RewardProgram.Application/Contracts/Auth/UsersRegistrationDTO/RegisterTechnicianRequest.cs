using RewardProgram.Application.Contracts.Auth;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.DTOs.Auth.UsersDTO;

public record RegisterTechnicianRequest
(
   string Name,
   string MobileNumber,
   NationalAddressResponse NationalAddress
);
