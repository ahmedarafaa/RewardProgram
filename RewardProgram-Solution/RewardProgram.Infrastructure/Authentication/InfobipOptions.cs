using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Infrastructure.Authentication;

public class InfobipOptions
{
    public const string SectionName = "Infobip";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string WhatsAppSender { get; set; } = string.Empty;
    public string ApplicationId { get; set; } = string.Empty;
    public string MessageTemplateId { get; set; } = string.Empty;
}
