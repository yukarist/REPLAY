public interface IAiService
{
    Task<string> SummarizeAsync(string text, int mode);
    Task<string> CorrectAsync(string text);
}