namespace RewardProgram.Application.Helpers;

public class InvitationOptions
{
    public const string SectionName = "Invitation";

    public string ShareBaseUrl { get; set; } = "https://app.raedrewardapp.com/invite/";
    public string IosAppStoreUrl { get; set; } = string.Empty;
    public string AndroidPlayStoreUrl { get; set; } = string.Empty;
    public string AndroidPackageName { get; set; } = string.Empty;
    public string DeepLinkScheme { get; set; } = "raedreward://invite/";
}
