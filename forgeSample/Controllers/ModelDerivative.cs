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
using Autodesk.Forge;
using Autodesk.Forge.Model;
using Newtonsoft.Json.Linq;
using RestSharp;
using Hangfire.Server;
using Hangfire.Console;
using Microsoft.AspNetCore.SignalR;
using System.IO.Compression;
using System.IO;

namespace forgeSample.Controllers
{
    [ApiController]
    public class ModelDerivativeController : ControllerBase
    {
        [HttpGet]
        [Route("api/forge/modelderivative/{urn}/thumbnail")]
        public async Task<IActionResult> GetThumbnail(string urn)
        {
            Credentials credentials = await Credentials.FromSessionAsync(base.Request.Cookies, Response.Cookies);
            if (credentials == null) { return Unauthorized(); }

            DerivativesApi derivatives = new DerivativesApi();
            derivatives.Configuration.AccessToken = credentials.TokenInternal;

            dynamic thumb = await derivatives.GetThumbnailAsync(urn, 100, 100);
            if (thumb == null) return Ok();

            return File(thumb, "image/jpeg");
        }

        public static async Task ProcessFileAsync(string userId, string hubId, string projectId, string folderUrn, string itemUrn, string versionUrn, string fileName, PerformContext console)
        {
            Credentials credentials = await Credentials.FromDatabaseAsync(userId);

            // start the translation
            DerivativesApi derivative = new DerivativesApi();
            derivative.Configuration.AccessToken = credentials.TokenInternal;

            dynamic document = new JObject();

            document.hubId = hubId.Replace("-", string.Empty); // this is breaking the search...
            document.projectId = projectId;
            document.folderUrn = folderUrn;
            document.itemUrn = itemUrn;
            document.versionUrn = versionUrn;
            document.fileName = fileName;
            document.metadata = new JArray();

            string versionUrn64 = Base64Encode(versionUrn);
            dynamic manifest = await derivative.GetManifestAsync(versionUrn64);
            if (manifest.status == "inprogress") throw new Exception("Translating..."); // force run it again

            string propertyValues = string.Empty;
            {
                IRestClient forgeClient = new RestClient("https://developer.api.autodesk.com/");
                RestRequest forgeRequest = new RestRequest("/derivativeservice/v2/derivatives/urn:adsk.viewing:fs.file:{urn}/output/objects_vals.json.gz", Method.GET);
                forgeRequest.AddParameter("urn", versionUrn64, ParameterType.UrlSegment);
                forgeRequest.AddHeader("Authorization", "Bearer " + credentials.TokenInternal);
                forgeRequest.AddHeader("Accept-Encoding", "gzip, deflate");
                IRestResponse response = await forgeClient.ExecuteTaskAsync(forgeRequest);
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    console.WriteLine("Model not ready, will retry");
                    throw new Exception("Model not ready...");
                }
                using (GZipStream gzip = new GZipStream(new MemoryStream(response.RawBytes), CompressionMode.Decompress))
                using (var fileStream = new StreamReader(gzip))
                {
                    dynamic viewProperties = new JObject();
                    viewProperties.viewId = "viewer";
                    viewProperties.collection = System.Text.RegularExpressions.Regex.Replace(fileStream.ReadToEnd(), @"\s+", string.Empty);
                    document.metadata.Add(viewProperties);
                }
            }


            /*{
                dynamic metadata = await derivative.GetMetadataAsync(versionUrn64);
                foreach (KeyValuePair<string, dynamic> metadataItem in new DynamicDictionaryItems(metadata.data.metadata))
                {
                    dynamic properties = await derivative.GetModelviewPropertiesAsync(versionUrn64, metadataItem.Value.guid);
                    if (properties == null)
                    {
                        console.WriteLine("Model not ready, will retry");
                        throw new Exception("Model not ready...");
                    }
                    console.WriteLine(string.Format("View: {0}", (string)metadataItem.Value.guid));
                    JArray collection = JObject.Parse(properties.ToString()).data.collection;

                    if (collection.Count > 0)
                    {
                        dynamic viewProperties = new JObject();
                        viewProperties.viewId = (string)metadataItem.Value.guid;
                        viewProperties.collection = collection.ToString(Newtonsoft.Json.Formatting.None);
                        document.metadata.Add(viewProperties);
                    }
                }
            }*/

            string json = (string)document.ToString(Newtonsoft.Json.Formatting.None);
            string absolutePath = string.Format("/manifest/_doc/{0}", Base64Encode(itemUrn));

            RestClient elasticSearchclient = new RestClient(Config.ElasticSearchServer);
            RestRequest elasticSearchRequest = new RestRequest(absolutePath, RestSharp.Method.POST);
            elasticSearchRequest.AddHeader("Content-Type", "application/json");
            elasticSearchRequest.AddParameter("text/json", json, ParameterType.RequestBody);

            if (!string.IsNullOrEmpty(Config.AWSKey) && !string.IsNullOrEmpty(Config.AWSSecret))
            {
                SortedDictionary<string, string> headers = AWS.Signature.SignatureHeader(
                    Amazon.RegionEndpoint.GetBySystemName(Config.AWSRegion),
                    new Uri(Config.ElasticSearchServer).Host,
                    "POST", json, absolutePath);
                foreach (var entry in headers) elasticSearchRequest.AddHeader(entry.Key, entry.Value);
            }

            IRestResponse res = await elasticSearchclient.ExecuteTaskAsync(elasticSearchRequest);

            console.WriteLine(string.Format("Submit to elasticsearch status: {0}", res.StatusCode.ToString()));
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes).Replace("/", "_").Replace("=", string.Empty);
        }
    }

    /// <summary>
    /// Class uses for SignalR
    /// </summary>
    public class ModelDerivativeHub : Microsoft.AspNetCore.SignalR.Hub
    {
        public string GetConnectionId() { return Context.ConnectionId; }

        /// <summary>
        /// Notify the client that the workitem is complete
        /// </summary>
        public async static Task NotifyFileFound(IHubContext<ModelDerivativeHub> context, string hubId)
        {
            await context.Clients.All.SendAsync("fileFound", hubId);
        }

        public async static Task NotifyFileComplete(IHubContext<ModelDerivativeHub> context, string hubId)
        {
            await context.Clients.All.SendAsync("fileComplete", hubId);
        }

        public async static Task NotifyHubComplete(IHubContext<ModelDerivativeHub> context, string hubId)
        {
            await context.Clients.All.SendAsync("hubComplete", hubId);
        }
    }
}