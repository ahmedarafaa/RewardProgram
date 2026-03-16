namespace RewardProgram.Application.Interfaces;

public interface IQrCodeGenerator
{
    byte[] Generate(string content, int width = 300, int height = 300);
}
