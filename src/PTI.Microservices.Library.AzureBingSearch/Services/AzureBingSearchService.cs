using Microsoft.Azure.CognitiveServices.Search.VideoSearch;
using Microsoft.Extensions.Logging;
using PTI.Microservices.Library.Configuration;
using PTI.Microservices.Library.Interceptors;
using PTI.Microservices.Library.Models.AzureBingSearch.GetInsights;
using PTI.Microservices.Library.Models.AzureBingSearch.SearchImages;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PTI.Microservices.Library.Services
{
    /// <summary>
    /// 
    /// </summary>
    public sealed class AzureBingSearchService
    {
        private ILogger<AzureBingSearchService> Logger { get; }
        private AzureBingSearchConfiguration AzureBingSearchConfiguration { get; }
        private CustomHttpClient CustomHttpClient { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="azureBingSearchConfiguration"></param>
        /// <param name="customHttpClient"></param>
        public AzureBingSearchService(ILogger<AzureBingSearchService> logger, AzureBingSearchConfiguration azureBingSearchConfiguration,
            CustomHttpClient customHttpClient)
        {
            this.Logger = logger;
            this.AzureBingSearchConfiguration = azureBingSearchConfiguration;
            this.CustomHttpClient = customHttpClient;
        }

        /// <summary>
        /// 
        /// </summary>
        public enum SafeSearchMode
        {
            /// <summary>
            /// 
            /// </summary>
            Strict,
            /// <summary>
            /// 
            /// </summary>
            Moderate,
            /// <summary>
            /// 
            /// </summary>
            Off
        }

        /// <summary>
        /// Searches for images using the specified term
        /// </summary>
        /// <param name="searchTerm"></param>
        /// <param name="safeSearchMode"></param>
        /// <param name="itemsToRetrieve"></param>
        /// <param name="offset"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<SearchImagesResponse> SearchImagesAsync(string searchTerm, SafeSearchMode safeSearchMode,
            int itemsToRetrieve = 10,
            int offset = 0,
            CancellationToken cancellationToken = default)
        {
            try
            {
                string requestUrl = $"{this.AzureBingSearchConfiguration.Endpoint}/bing/v7.0/images/search" +
                    $"?q={searchTerm}" +
                    $"&count={itemsToRetrieve}" +
                    $"&offset={offset}" +
                    $"&mkt={"en-US"}" +
                    $"&safeSearch={safeSearchMode.ToString()}";
                this.CustomHttpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", this.AzureBingSearchConfiguration.Key);
                var result = await this.CustomHttpClient.GetFromJsonAsync<SearchImagesResponse>(requestUrl);
                return result;
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Gets insights about the specified image. Uses Bing Visual Search APIs
        /// </summary>
        /// <param name="imageStream"></param>
        /// <param name="filename"></param>
        /// <param name="safeSearchMode"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<string> GetInsightsAsync(Stream imageStream, 
            string filename,
            SafeSearchMode safeSearchMode, CancellationToken cancellationToken = default)
        {
            try
            {
                //Check https://docs.microsoft.com/en-us/rest/api/cognitiveservices/bingvisualsearch/images/visualsearch#examples
                string requestUrl = $"{this.AzureBingSearchConfiguration.Endpoint}/bing/v7.0/images/visualsearch" +
                    $"?mkt={"en-US"}" +
                    $"&safeSearch={safeSearchMode.ToString()}";
                this.CustomHttpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", this.AzureBingSearchConfiguration.Key);
                this.CustomHttpClient.DefaultRequestHeaders.Add("X-BingApis-SDK", "true");
                GetInsightsRequest model = new GetInsightsRequest()
                {
                    imageInfo = new Imageinfo()
                    {
                        url = null,
                        cropArea = new Croparea()
                        {
                            right=1,
                            bottom=1
                        }
                    },
                    knowledgeRequest = new Knowledgerequest()
                    {
                        filters = new Filters()
                        {
                            site= null
                        }
                    }
                };
                MultipartFormDataContent multipartContent =
                    new MultipartFormDataContent();
                string knowledgeRequestContentString = JsonSerializer.Serialize(model);
                StringContent knowledgeRequestContent = new StringContent(knowledgeRequestContentString);
                multipartContent.Add(knowledgeRequestContent, "knowledgeRequest");
                multipartContent.Add(
                    new StreamContent(imageStream),
                    "image", filename
                    //, e.File.Name
                    );

                var bodyString = await multipartContent.ReadAsStringAsync(cancellationToken);
                HttpResponseMessage response = await this.CustomHttpClient.PostAsync(requestUrl, multipartContent, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    return result;
                }
                else
                {
                    string reason = response.ReasonPhrase;
                    string detailedError = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Reason: {reason}. Details: {detailedError}");
                }
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public class SearchTermTag
        {
            /// <summary>
            /// 
            /// </summary>
            public string SearchTerm { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public string Tag { get; set; }
        }

        /// <summary>
        /// Exports the search results ready for ML .NET
        /// </summary>
        /// <param name="searchTerms"></param>
        /// <param name="baseFolder"></param>
        /// <param name="safeSearchMode"></param>
        /// <param name="overwriteExistingFiles"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task ExportSearchResultsImagesForMLNetToDiskAsync(List<SearchTermTag> searchTerms,
            string baseFolder,
            SafeSearchMode safeSearchMode,
            bool overwriteExistingFiles = false,
            CancellationToken cancellationToken = default)
        {
            foreach (var singleSearchTerm in searchTerms)
            {
                int totalItemsToRetrieve = 1000;
                int itemsPerPage = 150;
                int totalPages = (int)Math.Ceiling((decimal)totalItemsToRetrieve / itemsPerPage);
                if (totalPages > 1)
                    totalPages = 1;
                for (int iPage = 0; iPage < totalPages; iPage++)
                {
                    int offSet = iPage * itemsPerPage;
                    Logger?.LogInformation($"Searching term: {singleSearchTerm.SearchTerm}. Page: {iPage + 1} of {totalPages}");
                    var singleSearchTermResults = await this.SearchImagesAsync(singleSearchTerm.SearchTerm, safeSearchMode,
                        itemsToRetrieve: itemsPerPage, offset: offSet, cancellationToken);

                    foreach (var singleSearchResult in singleSearchTermResults.value?.Take(10))
                    {
                        try
                        {
                            var fileName = System.IO.Path.GetFileName(singleSearchResult.contentUrl);
                            if (String.IsNullOrWhiteSpace(fileName))
                                fileName = $"{Guid.NewGuid().ToString()}.png";
                            string destFilePath = $"{Path.Combine(baseFolder, @$"Images\{singleSearchTerm.Tag ?? singleSearchTerm.SearchTerm}\{fileName}")}";
                            if (!overwriteExistingFiles && File.Exists(destFilePath))
                                continue;
                            Logger?.LogInformation($"Downloading image from: {singleSearchResult.contentUrl}");
                            var response = await this.CustomHttpClient.GetAsync(singleSearchResult.contentUrl, cancellationToken: cancellationToken);
                            if (response.IsSuccessStatusCode)
                            {
                                //var contentType = response.Content.Headers.ContentType;
                                //string fileExtension = contentType.MediaType.ToLower().Replace("image/","");
                                var directory = Path.GetDirectoryName(destFilePath);
                                if (!Directory.Exists(directory))
                                    Directory.CreateDirectory(directory);
                                var fileBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                                var queryStringStartIndex = destFilePath.IndexOf("?");
                                if (queryStringStartIndex >= 0)
                                    destFilePath = destFilePath.Substring(0, queryStringStartIndex);
                                destFilePath = destFilePath.TrimEnd(new char[] {'\\','/'});
                                await File.WriteAllBytesAsync(destFilePath, fileBytes);
                            }
                            else
                            {
                                var error = await response.Content.ReadAsStringAsync();
                                this.Logger?.LogError($"Error downloading image. Message: {error}. Url: {singleSearchResult.contentUrl}");
                            }
                        }
                        catch (HttpRequestException httpRequestException)
                        {
                            //probable unable to access the image resource: e.g. access denied
                            this.Logger?.LogError(httpRequestException, httpRequestException.Message);
                        }
                        catch (Exception ex)
                        {
                            this.Logger?.LogError(ex, ex.Message);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Exports the search results ready for ML .NET
        /// </summary>
        /// <param name="searchTerms"></param>
        /// <param name="safeSearchMode"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<MemoryStream> ExportSearchResultsImagesForMLNetAsZipAsync(List<SearchTermTag> searchTerms, SafeSearchMode safeSearchMode,
            CancellationToken cancellationToken = default)
        {
            MemoryStream outputStream = new MemoryStream();
            // create a zip
            using (ZipArchive zipArchive = new ZipArchive(outputStream, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                foreach (var singleSearchTerm in searchTerms)
                {
                    int totalItemsToRetrieve = 1000;
                    int itemsPerPage = 150;
                    int totalPages = (int)Math.Ceiling((decimal)totalItemsToRetrieve / itemsPerPage);
                    for (int iPage = 0; iPage < totalPages; iPage++)
                    {
                        int offSet = iPage * itemsPerPage;
                        var singleSearchTermResults = await this.SearchImagesAsync(singleSearchTerm.SearchTerm, safeSearchMode,
                            itemsToRetrieve: itemsPerPage, offset: offSet, cancellationToken);

                        foreach (var singleSearchResult in singleSearchTermResults.value)
                        {
                            try
                            {
                                var fileStream = await this.CustomHttpClient.GetStreamAsync(singleSearchResult.contentUrl);
                                // add the item name to the zip
                                var fileName = System.IO.Path.GetFileName(singleSearchResult.contentUrl);
                                ZipArchiveEntry zipArchiveEntry = zipArchive.CreateEntry($"Images/{singleSearchTerm.Tag ?? singleSearchTerm.SearchTerm}/{fileName}");
                                // add the item bytes to the zip entry by opening the original file and copying the bytes
                                using (System.IO.Stream entryStream = zipArchiveEntry.Open())
                                {
                                    fileStream.CopyTo(entryStream);
                                }
                            }
                            catch (Exception ex)
                            {
                                this.Logger?.LogError(ex, ex.Message);
                            }
                        }
                    }
                }
            }
            if (outputStream.CanSeek)
                outputStream.Position = 0;
            return outputStream;
        }

        /// <summary>
        /// Searches for videos using the specified term
        /// </summary>
        /// <param name="searchTerm"></param>
        /// <param name="safeSearchMode"></param>
        /// <param name="itemsToRetrieve"></param>
        /// <param name="offset"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<Microsoft.Azure.CognitiveServices.Search.VideoSearch.Models.Videos> SearchVideosAsync(string searchTerm, SafeSearchMode safeSearchMode,
            int itemsToRetrieve = 10,
            int offset = 0,
            CancellationToken cancellationToken = default)
        {
            try
            {
                Microsoft.Azure.CognitiveServices.Search.VideoSearch.VideoSearchClient videoSearchClient =
                    new Microsoft.Azure.CognitiveServices.Search.VideoSearch.VideoSearchClient(
                        credentials: new Microsoft.Azure.CognitiveServices.Search.VideoSearch.ApiKeyServiceClientCredentials(this.AzureBingSearchConfiguration.Key),
                        this.CustomHttpClient, disposeHttpClient: false
                        )
                    {
                        Endpoint = this.AzureBingSearchConfiguration.Endpoint
                    };
                var result = await videoSearchClient.Videos.SearchAsync(searchTerm,
                    safeSearch: Microsoft.Azure.CognitiveServices.Search.VideoSearch.Models.SafeSearch.Off
                    );
                return result;
                //string requestUrl = $"{this.AzureBingSearchConfiguration.Endpoint}/bing/v7.0/videos/search" +
                //    $"?q={searchTerm}" +
                //    $"&count={itemsToRetrieve}" +
                //    $"&offset={offset}" +
                //    $"&mkt={"en-US"}" +
                //    $"&safeSearch={safeSearchMode.ToString()}";
                //this.CustomHttpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", this.AzureBingSearchConfiguration.Key);
                //var rQuery = await this.CustomHttpClient.GetStringAsync(requestUrl);
                //var result = await this.CustomHttpClient.GetFromJsonAsync<SearchImagesResponse>(requestUrl);
                //return result;
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex, ex.Message);
                throw;
            }
        }

    }
}
