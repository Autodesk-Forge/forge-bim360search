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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Autodesk.Forge;
using Autodesk.Forge.Model;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using RestSharp;
using Hangfire.Server;
using Hangfire.Console;
using Microsoft.AspNetCore.SignalR;

namespace forgeSample.Controllers
{
    [ApiController]
    public class ModelDerivativeController : ControllerBase
    {
        private IHubContext<ModelDerivativeHub> _hubContext;
        public ModelDerivativeController(IHubContext<ModelDerivativeHub> hubContext) { _hubContext = hubContext; }

        [HttpGet]
        [Route("api/forge/modelderivative/{urn}/thumbnail")]
        public async Task<IActionResult> GetThumbnail(string urn)
        {
            Credentials credentials = await Credentials.FromSessionAsync(base.Request.Cookies, Response.Cookies);
            if (credentials == null) { return Unauthorized(); }

            DerivativesApi derivatives = new DerivativesApi();
            derivatives.Configuration.AccessToken = credentials.TokenInternal;

            return File(await derivatives.GetThumbnailAsync(urn, 100, 100), "image/jpeg");
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
            //console.WriteLine(string.Format("Retrieving data for {0}/{1}/{2}/{3}/{4}", hubId, projectId, folderUrn, itemUrn, versionUrn));
            //console.WriteLine(string.Format("https://docs.b360.autodesk.com/projects/{0}/folders/{1}/detail/viewer/items/{2}",projectId.Replace("b.", string.Empty), folderUrn, itemUrn));
            try
            {
                dynamic manifest = await derivative.GetManifestAsync(versionUrn64);
                if (manifest.status == "inprogress")
                {
                    throw new Exception("Translating..."); // force run it again
                }
            }
            catch (Exception ex)
            {

            }

            dynamic metadata = await derivative.GetMetadataAsync(versionUrn64);
            foreach (KeyValuePair<string, dynamic> metadataItem in new DynamicDictionaryItems(metadata.data.metadata))
            {
                console.WriteLine(string.Format("View: {0}", (string)metadataItem.Value.guid));
                dynamic properties = await derivative.GetModelviewPropertiesAsync(versionUrn64, metadataItem.Value.guid);

                JArray collection = JObject.Parse(properties.ToString()).data.collection;
                if (collection.Count > 0)
                {
                    dynamic viewProperties = new JObject();
                    viewProperties.viewId = (string)metadataItem.Value.guid;
                    viewProperties.collection = collection.ToString(Newtonsoft.Json.Formatting.None);
                    document.metadata.Add(viewProperties);
                }
            }

            string json = (string)document.ToString(Newtonsoft.Json.Formatting.None);

            RestClient client = new RestClient(Config.ElasticSearchServer);
            RestRequest request = new RestRequest("/manifest/_doc/{itemUrn64}", RestSharp.Method.POST);
            request.AddParameter("itemUrn64", Base64Encode(itemUrn), ParameterType.UrlSegment);
            request.AddHeader("Content-Type", "application/json");
            request.AddParameter("text/json", json, ParameterType.RequestBody);
            IRestResponse res = await client.ExecuteTaskAsync(request);

            Console.WriteLine(string.Format("Status: {0}", res.StatusCode.ToString()));
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