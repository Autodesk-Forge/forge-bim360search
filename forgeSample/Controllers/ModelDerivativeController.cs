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
using System.Linq;

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

        private class ManifestItem
        {
            public string Guid { get; set; }
            public string MIME { get; set; }
            public PathInfo Path { get; set; }
        }

        private static string[] ROLES = {
            "Autodesk.CloudPlatform.DesignDescription",
            "Autodesk.CloudPlatform.PropertyDatabase",
            "Autodesk.CloudPlatform.IndexableContent",
            "leaflet-zip",
            "thumbnail",
            "graphics",
            "preview",
            "raas",
            "pdf",
            "lod",
        };

        private class PathInfo
        {
            public string RootFileName { get; set; }
            public string LocalPath { get; set; }
            public string BasePath { get; set; }
            public string URN { get; set; }
            public List<string> Files { get; set; }
        }

        private static PathInfo DecomposeURN(string encodedUrn)
        {
            string urn = Uri.UnescapeDataString(encodedUrn);

            string rootFileName = urn.Substring(urn.LastIndexOf('/') + 1);
            string basePath = urn.Substring(0, urn.LastIndexOf('/') + 1);
            string localPath = basePath.Substring(basePath.IndexOf('/') + 1);
            localPath = System.Text.RegularExpressions.Regex.Replace(localPath, "[/]?output/", string.Empty);

            return new PathInfo()
            {
                RootFileName = rootFileName,
                BasePath = basePath,
                LocalPath = localPath,
                URN = urn
            };
        }

        private static List<ManifestItem> ParseManifest(dynamic manifest)
        {
            List<ManifestItem> urns = new List<ManifestItem>();
            foreach (KeyValuePair<string, object> item in manifest.Dictionary)
            {
                DynamicDictionary itemKeys = (DynamicDictionary)item.Value;
                if (itemKeys.Dictionary.ContainsKey("role") && ROLES.Contains(itemKeys.Dictionary["role"]))
                {
                    urns.Add(new ManifestItem
                    {
                        Guid = (string)itemKeys.Dictionary["guid"],
                        MIME = (string)itemKeys.Dictionary["mime"],
                        Path = DecomposeURN((string)itemKeys.Dictionary["urn"])
                    });
                }

                if (itemKeys.Dictionary.ContainsKey("children"))
                {
                    urns.AddRange(ParseManifest(itemKeys.Dictionary["children"]));
                }
            }
            return urns;
        }

        public struct Resource
        {
            /// <summary>
            /// File name (no path)
            /// </summary>
            public string FileName { get; set; }
            /// <summary>
            /// Remove path to download (must add developer.api.autodesk.com prefix)
            /// </summary>
            public string RemotePath { get; set; }
            /// <summary>
            /// Path to save file locally
            /// </summary>
            public string LocalPath { get; set; }
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

            List<ManifestItem> manifestItems = ParseManifest(manifest.derivatives);

            List<Resource> resouces = new List<Resource>();
            foreach (ManifestItem item in manifestItems)
            {
                if (item.MIME != "application/autodesk-db") continue; 

                string file = "objects_vals.json.gz"; // the only file we need here
                Uri myUri = new Uri(new Uri(item.Path.BasePath), file);
                resouces.Add(new Resource()
                {
                    FileName = file,
                    RemotePath = "derivativeservice/v2/derivatives/" + Uri.UnescapeDataString(myUri.AbsoluteUri),
                    LocalPath = Path.Combine(item.Path.LocalPath, file)
                });
            }

            // this approach uses the Viewer propertyDatabase, which is a non-supported way of accessing the model metadata
            // it will return the non-duplicated list of attributes values (not the attribute type)
            // as we don't want to manipulate it, just search, it doesn't matter if this list changes its format
            IRestClient forgeClient = new RestClient("https://developer.api.autodesk.com/");
            if (resouces.Count != 1) throw new Exception(resouces.Count + " objects_vals.json.gz found, will try again");

            RestRequest forgeRequest = new RestRequest(resouces[0].RemotePath, Method.GET);
            forgeRequest.AddHeader("Authorization", "Bearer " + credentials.TokenInternal);
            forgeRequest.AddHeader("Accept-Encoding", "gzip, deflate");
            IRestResponse response = await forgeClient.ExecuteTaskAsync(forgeRequest);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                console.WriteLine(string.Format("Cannot download attributes ({0}), will retry", response.StatusCode));
                throw new Exception(string.Format("Cannot download attributes: {0}", response.StatusCode));
            }
            using (GZipStream gzip = new GZipStream(new MemoryStream(response.RawBytes), CompressionMode.Decompress))
            using (var fileStream = new StreamReader(gzip))
            {
                dynamic viewProperties = new JObject();
                viewProperties.viewId = "viewer";
                viewProperties.collection = System.Text.RegularExpressions.Regex.Replace(fileStream.ReadToEnd(), @"\n", string.Empty);
                document.metadata.Add(viewProperties);
            }


            // as an alternative solution, using supported APIs, one could get the complete metadata JSON
            // but that results in more data that we don't need for search, like attribute names
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