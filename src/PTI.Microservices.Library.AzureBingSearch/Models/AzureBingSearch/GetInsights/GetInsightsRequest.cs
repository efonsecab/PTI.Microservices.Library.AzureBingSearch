using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PTI.Microservices.Library.Models.AzureBingSearch.GetInsights
{

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    /// <summary>
    /// Ceck https://docs.microsoft.com/en-us/bing/search-apis/bing-visual-search/how-to/get-insights
    /// </summary>
    public class GetInsightsRequest
    {
        public Imageinfo imageInfo { get; set; }
        public Knowledgerequest knowledgeRequest { get; set; }
    }

    public class Imageinfo
    {
        public string url { get; set; }
        public Croparea cropArea { get; set; }
    }

    public class Croparea
    {
        public float top { get; set; }
        public float left { get; set; }
        public float right { get; set; }
        public float bottom { get; set; }
    }

    public class Knowledgerequest
    {
        public Filters filters { get; set; }
    }

    public class Filters
    {
        public string site { get; set; }
    }

    public class Invokedskillsrequestdata
    {
        public string enableEntityData { get; set; }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
