using AlliumInterview.Models;
using AlliumInterview.Services;

namespace AlliumInterview.Services.Abstractions
{
    public interface ISharePointFileService
    {
        Task<List<SharePointFile>> GetFilesList();

        Task<SignDocumentResponse> DownloadFile(string fileName);
    }
}
