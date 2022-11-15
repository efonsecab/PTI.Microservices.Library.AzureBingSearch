using PTI.Microservices.Library.Interceptors;
using PTI.Microservices.Library.Services;

namespace PTI.Microservices.Library.AzureBingSearch.AutomatedTests
{
    [TestClass]
    public class AzureBingSearchService_Tests: TestsBase
    {
        [TestMethod]
        public async Task Test_SearchVideosAsync()
        {
            var azureBingSearchService = CreateAzureBingSearchService();
            var result = await
            azureBingSearchService.SearchVideosAsync(".NET", AzureBingSearchService.SafeSearchMode.Strict,
                20, 0, CancellationToken.None);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.TotalEstimatedMatches > 0);
            Assert.IsNotNull(result.Value);
            Assert.IsTrue(result.Value.Count > 0);
        }

        [TestMethod]
        public async Task Test_SearchImagesAsync()
        {
            var azureBingSearchService = CreateAzureBingSearchService();
            var result = await
            azureBingSearchService.SearchImagesAsync(".NET", AzureBingSearchService.SafeSearchMode.Strict,
                20, 0, CancellationToken.None);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.totalEstimatedMatches > 0);
            Assert.IsNotNull(result.value);
            Assert.IsTrue(result.value.Length > 0);
        }

        private AzureBingSearchService CreateAzureBingSearchService()
        {
            CustomHttpClientHandler customHttpClientHandler = new(null);
            CustomHttpClient customHttpClient = new(customHttpClientHandler);
            AzureBingSearchService azureBingSearchService = new AzureBingSearchService(null,
                base._azureBingSearchConfiguration, customHttpClient);
            return azureBingSearchService;
        }
    }
}