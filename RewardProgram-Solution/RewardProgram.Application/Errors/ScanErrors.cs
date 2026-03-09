using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Errors;

public static class ScanErrors
{
    public static readonly Error UnauthorizedRole =
        new("Scan.UnauthorizedRole", "لا يحق لك مسح الباركود — مخصص للبائعين والفنيين فقط", 403);

    public static readonly Error UserNotApproved =
        new("Scan.UserNotApproved", "الحساب غير مفعل أو غير معتمد", 403);
}
