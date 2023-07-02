using AlliumInterview.Models;
using AlliumInterview.Services.Abstractions;
using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace AlliumInterview.Services.Implementations
{
    public class SharePointFileService : ISharePointFileService
    {
        private readonly HttpClient _httpClient;
        private IConfiguration _configuration;
        private string token = default!;

        public SharePointFileService(IConfiguration configuration)
        {
            _httpClient = new HttpClient();
            _configuration = configuration;
        }

        public async Task<SignDocumentResponse> DownloadFile(string fileName)
        {
            string url = _configuration["SharePointInformation:DownloadUrl"].Replace("#filename#", fileName);

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    request.Headers.Add("Accept", "application/json; odata=nometadata");

                    var response = await _httpClient.SendAsync(request);

                    response.EnsureSuccessStatusCode();

                    dynamic content = await response.Content.ReadAsByteArrayAsync();

                    var filePath = Environment.CurrentDirectory + "\\" + fileName;

                    FileStream outputStream = new FileStream(filePath, FileMode.OpenOrCreate | FileMode.Append, FileAccess.Write, FileShare.None);
                    outputStream.Write(content, 0, content.Length);
                    outputStream.Flush(true);
                    outputStream.Close();

                    var signResponse = SignDocument(filePath);
                    if (signResponse.Success is false)
                        return signResponse;

                    var uploadResponse = await UploadFile(filePath);

                    return uploadResponse;
                }
            }
            catch (Exception)
            {
                return new SignDocumentResponse { Success = false, Message = "Error downloading the file" };
            }
            
        }

        public async Task<List<SharePointFile>> GetFilesList()
        {
            var result = new List<SharePointFile>();
            while(true)
            {
                try
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Post, _configuration["SharePointInformation:FolderUrl"]))
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                        request.Headers.Add("Accept", "application/json; odata=nometadata");


                        var response = await _httpClient.SendAsync(request);

                        response.EnsureSuccessStatusCode();

                        dynamic content = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());

                        if (content is not null)
                            foreach (var item in content.value)
                            {
                                var time = item.TimeCreated.ToString();

                                result.Add(new SharePointFile
                                {
                                    Name = item.Name.ToString(),
                                    TimeCreated = item.TimeCreated,
                                    TimeLastModified = item.TimeLastModified
                                });
                            }
                        break;
                    }
                }
                catch (HttpRequestException ex)
                {
                    if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        token = await GetAccessToken();
                    }

                }
            }
           
            return result;
        }

        private async Task<string> GetAccessToken()
        {
            string token = "";
            using (var request = new HttpRequestMessage(HttpMethod.Post, _configuration["SharePointInformation:AccessTokenUrl"]))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("Accept", "application/json; odata=nometadata");

                var content = new MultipartFormDataContent();

            
                content.Add(new StringContent("refresh_token"), "grant_type");
                content.Add(new StringContent(_configuration["SharePointInformation:ClientId"]), "client_id");
                content.Add(new StringContent(_configuration["SharePointInformation:ClientSecret"]), "client_secret");
                content.Add(new StringContent(_configuration["SharePointInformation:Resource"]), "resource");
                content.Add(new StringContent(_configuration["SharePointInformation:RefreshToken"]), "refresh_token");

                request.Content = content;

                var response = await _httpClient.SendAsync(request);

                response.EnsureSuccessStatusCode();

                dynamic result = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());

                token = result?.access_token ?? "";
            }

            return token;
        }

        private async Task<SignDocumentResponse> UploadFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            string url = _configuration["SharePointInformation:UploadUrl"].Replace("#filename#", fileName);

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    request.Headers.Add("Accept", "application/json; odata=nometadata");
                    request.Headers.Add("X-RequestDigest", _configuration["SharePointInformation:FormDigestValue"]);

                    var content = new MultipartFormDataContent();

                    MemoryStream memoryStream = new MemoryStream(File.ReadAllBytes(filePath));
                    byte[] data = memoryStream.ToArray();

                    ByteArrayContent bytecontent = new ByteArrayContent(data);
                    bytecontent.Headers.ContentDisposition = new ContentDispositionHeaderValue("binary");
                    bytecontent.Headers.ContentDisposition.Name = fileName;
                    bytecontent.Headers.ContentDisposition.FileName = fileName;
                    bytecontent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                    content.Add(bytecontent);
                    request.Content = content;

                    var response = await _httpClient.SendAsync(request);

                    response.EnsureSuccessStatusCode();

                    dynamic result = await response.Content.ReadAsStreamAsync();

                    return new SignDocumentResponse { Success = true, Message = "Success" };
                }
            }
            catch (Exception)
            {
                return new SignDocumentResponse { Success = false, Message = "Error uploading the file" };
            }
            
        }

        private SignDocumentResponse SignDocument(string filePath)
        {
            try
            {
                var document = PdfDocument.FromFile(filePath);

                var rect = new IronSoftware.Drawing.CropRectangle(100, 200, 200, 100);

                var signature = new IronPdf.Signing.PdfSignature("cert.pfx", "1234")
                {
                    SigningContact = "marianrazvan508@gmail.com",
                    SigningLocation = "Bucharest",
                    SigningReason = "Allium interview",
                };


                document.Sign(signature);
                document.SaveAs(filePath);

                return new SignDocumentResponse { Success = true, Message = "OK" };
            }
            catch(Exception)
            {
                return new SignDocumentResponse { Success = false, Message = "Error signing the document" };
            }
            
        }

    }
}
