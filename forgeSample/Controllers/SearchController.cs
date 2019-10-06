/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Forge Partner Development
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace forgeSample.Controllers
{
    public class SearchController : ControllerBase
    {
        /// <summary>
        /// GET TreeNode passing the ID
        /// </summary>
        [HttpGet]
        [Route("api/forge/datamanagement/hubs/{hubid}/search")]
        public async Task<JArray> GetSearchAsync(string hubId, [FromQuery]string q)
        {
            Credentials credentials = await Credentials.FromSessionAsync(base.Request.Cookies, Response.Cookies);
            if (credentials == null) { return null; }

            string json = "{\"_source\":[\"projectId\",\"folderUrn\",\"itemUrn\",\"versionUrn\", \"fileName\"],\"query\":{\"bool\":{\"must\":{\"match\":{\"metadata.collection\":\"" + q + "\"}},\"filter\":[{\"term\":{\"hubId\":\"" + hubId.Replace("-", string.Empty) + "\"}}]}}}";
            string absolutePath = "/manifest/_search";

            RestClient client = new RestClient(Config.ElasticSearchServer);
            RestRequest request = new RestRequest(absolutePath, RestSharp.Method.POST);
            request.AddHeader("Content-Type", "application/json");
            request.AddParameter("text/json", json, ParameterType.RequestBody);

            SortedDictionary<string, string> headers = AWS.Signature.SignatureHeader(
                Amazon.RegionEndpoint.GetBySystemName(Config.ElasticSearchServerRegion),
                new Uri(Config.ElasticSearchServer).Host,
                "POST", json, absolutePath);
            foreach (var entry in headers) request.AddHeader(entry.Key, entry.Value);

            IRestResponse res = await client.ExecuteTaskAsync(request);

            dynamic results = JObject.Parse(res.Content);
            return results.hits.hits;
        }



    }
}