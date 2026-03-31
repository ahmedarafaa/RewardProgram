namespace RewardProgram.Application.Contracts.Admin.Content;

public record ContactUsResponse(
    string Phone,
    string Email,
    string WhatsApp,
    string Address,
    string WorkingHours
);
