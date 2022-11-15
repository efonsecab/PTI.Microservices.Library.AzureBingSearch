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
