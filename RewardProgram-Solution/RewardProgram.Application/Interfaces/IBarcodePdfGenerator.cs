namespace RewardProgram.Application.Interfaces;

public interface IBarcodePdfGenerator
{
    byte[] GeneratePdf(string productName, string productCode, List<string> barcodeCodes);
}
