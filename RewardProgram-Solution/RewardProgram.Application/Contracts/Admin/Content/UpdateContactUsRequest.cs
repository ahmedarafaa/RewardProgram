namespace RewardProgram.Application.Contracts.Admin.Content;

public record UpdateContactUsRequest(
    string Phone,
    string Email,
    string WhatsApp,
    string Address,
    string WorkingHours
);
