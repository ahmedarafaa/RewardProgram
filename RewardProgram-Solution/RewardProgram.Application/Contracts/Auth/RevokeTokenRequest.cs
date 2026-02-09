using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.Contracts.Auth;

public record RevokeTokenRequest(
    string RefreshToken
);