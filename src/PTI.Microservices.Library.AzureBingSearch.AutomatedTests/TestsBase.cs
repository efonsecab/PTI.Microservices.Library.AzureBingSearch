using Microsoft.Extensions.Configuration;
using PTI.Microservices.Library.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PTI.Microservices.Library.AzureBingSearch.AutomatedTests
{
    public abstract class TestsBase
    {
        protected readonly IConfigurationRoot _configuration;
        protected readonly AzureBingSearchConfiguration? _azureBingSearchConfiguration;

        public TestsBase()
        {
            var configurationBuilder = new ConfigurationBuilder()
                .AddUserSecrets<TestsBase>().AddEnvironmentVariables();
            this._configuration = configurationBuilder.Build();
            this._azureBingSearchConfiguration = this._configuration
                .GetSection(nameof(AzureBingSearchConfiguration))
                .Get<AzureBingSearchConfiguration>();
            GlobalPackageConfiguration.EnableHttpRequestInformationLog = false;
            GlobalPackageConfiguration.RapidApiKey = this._configuration["RapidApiKey"];
            ;
        }
    }
}
