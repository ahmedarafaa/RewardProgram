namespace RewardProgram.Application.Interfaces;

public interface IBarcodePdfGenerator
{
    /// <param name="productNameEn">English name, printed under the Arabic one. Omitted from the label when null or blank.</param>
    byte[] GeneratePdf(string productName, string? productNameEn, string productCode, List<string> barcodeCodes);
}
