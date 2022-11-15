# PTI.Microservices.Library.AzureBingSearch

This is part of PTI.Microservices.Library set of packages

The purpose of this package is to facilitate consuming Azure Bing Search APIs

**Examples:**

## Get Insights
    CustomHttpClient customHttpClient = new CustomHttpClient(new CustomHttpClientHandler(null));
    AzureBingSearchService azureBingSearchService =
        new AzureBingSearchService(null, this.AzureBingSearchConfiguration, customHttpClient);
    string filePath = @"C:\Temp\TestImage.png";
    var fileName = Path.GetFileName(filePath);
    var imageStream = File.Open(filePath, FileMode.Open);
    imageStream.Position = 0;
    var result = await azureBingSearchService.GetInsightsAsync(imageStream, fileName, AzureBingSearchService.SafeSearchMode.Moderate);

## Search Images
    string searchTerm = "La Paz Waterfall";
    CustomHttpClient customHttpClient = new CustomHttpClient(new CustomHttpClientHandler(null));
    AzureBingSearchService azureBingSearchService =
        new AzureBingSearchService(null, this.AzureBingSearchConfiguration, customHttpClient);
    var searchImagesResult =
    await azureBingSearchService.SearchImagesAsync(searchTerm,
        safeSearchMode: AzureBingSearchService.SafeSearchMode.Strict,
        itemsToRetrieve: 50);

## Search Videos
    string searchTerm = "Costa Rica";
    CustomHttpClient customHttpClient = new CustomHttpClient(new CustomHttpClientHandler(null));
    AzureBingSearchService azureBingSearchService =
        new AzureBingSearchService(null, this.AzureBingSearchConfiguration, customHttpClient);
    var result =
    await azureBingSearchService.SearchVideosAsync(searchTerm,
        safeSearchMode: AzureBingSearchService.SafeSearchMode.Strict,
        itemsToRetrieve: 50);