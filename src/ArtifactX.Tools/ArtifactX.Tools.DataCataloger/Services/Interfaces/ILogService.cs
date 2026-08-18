namespace ArtifactX.Tools.DataCataloger.Services.Interfaces;

public interface ILogService
{
    void Info(string message);
    void Warn(string message);
    void Error(string message);
    void StartPak(string pakName);
    void EndPak(string pakName);
    void Flush();
}
