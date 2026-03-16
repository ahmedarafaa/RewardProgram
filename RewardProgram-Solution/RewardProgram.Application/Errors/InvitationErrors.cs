using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Errors;

public static class InvitationErrors
{
    public static readonly Error InvalidInvitationCode =
        new("Invitation.InvalidCode", "رمز الدعوة غير صالح", 400);

    public static readonly Error SelfInvitation =
        new("Invitation.SelfInvitation", "لا يمكنك استخدام رمز الدعوة الخاص بك", 400);

    public static readonly Error InviterNotApproved =
        new("Invitation.InviterNotApproved", "صاحب الدعوة غير معتمد", 400);
}
