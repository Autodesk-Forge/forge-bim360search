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

namespace forgeSample.Controllers
{
    [ApiController]
    public class ModelDerivative : ControllerBase
    {
        /// <summary>
        /// Start the translation job for a given urn
        /// </summary>
        /// <param name="urn"></param>
        /// <returns></returns>
        public static async Task ProcessFileAsync(string userId, string hubId, string projectId, string folderUrn, string itemUrn, string versionUrn, string fileName, PerformContext console)
        {
            Credentials credentials = await Credentials.FromDatabaseAsync(userId);

            // start the translation
            DerivativesApi derivative = new DerivativesApi();
            derivative.Configuration.AccessToken = credentials.TokenInternal;

            dynamic document = new JObject();
            document.hubId =  hubId.Replace("-", string.Empty); // this is breaking the search...
            document.projectId = projectId;
            document.folderUrn = folderUrn;
            document.itemUrn = itemUrn;
            document.fileName = fileName;
            document.metadata = new JArray();

            string versionUrn64 = Base64Encode(versionUrn);
            //console.WriteLine(string.Format("Retrieving data for {0}/{1}/{2}/{3}/{4}", hubId, projectId, folderUrn, itemUrn, versionUrn));
            //console.WriteLine(string.Format("https://docs.b360.autodesk.com/projects/{0}/folders/{1}/detail/viewer/items/{2}",projectId.Replace("b.", string.Empty), folderUrn, itemUrn));
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
                    viewProperties.collection = collection.ToString(Newtonsoft.Json.Formatting.None); // (string)Regex.Replace(properties.ToString(), @"(""[^""\\]*(?:\\.[^""\\]*)*"")|\s+", "$1");
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
            console.WriteLine(string.Format("Status: {0}", res.StatusCode.ToString()));



            // prepare the payload
            /* List<JobPayloadItem> outputs = new List<JobPayloadItem>()
            {
            new JobPayloadItem(
              JobPayloadItem.TypeEnum.Svf,
              new List<JobPayloadItem.ViewsEnum>()
              {
                JobPayloadItem.ViewsEnum._2d,
                JobPayloadItem.ViewsEnum._3d
              })
            };
            JobPayload job;
            job = new JobPayload(new JobPayloadInput(urn), new JobPayloadOutput(outputs));*/
            //dynamic jobPosted = await derivative.TranslateAsync(job);

            /*
            {
              "jsonapi": {
                "version": "1.0"
              },
              "data": {
                "id": {your uuid for this call}
                "type": "commands",
                "attributes": {
                  "extension": {
                    "type": "commands:autodesk.bim360:files.process",
                    "version": "1.0.0",
                    "data": {
                      'files': [{
                        'versionUrn': 'xxxxxxx',
                        'versionName': '',
                        'parentFolderUrn': 'xxxxxxx'
                      }]
                    }
                  }
                }
              }
            } 
            */

            /*
                        dynamic payload = JObject.Parse(@"{'jsonapi':{'version':'1.0'},'data':{'id':'','type':'commands','attributes':{'extension':{'type':'commands:autodesk.bim360:files.process','version':'1.0.0','data':{'files':[{'versionUrn':'','versionName':'','parentFolderUrn':''}]}}}}}");
                        payload.data.id = Guid.NewGuid().ToString(); // works as our uuid
                        payload.data.attributes.extension.data.files[0].versionUrn = versionUrn;
                        payload.data.attributes.extension.data.files[0].parentFolderUrn = parentFolderUrn;

                        console.WriteLine(string.Format("Starting translation for {0}", fileName));

                        RestClient client = new RestClient("https://developer.api.autodesk.com");
                        RestRequest request = new RestRequest("/dm/v1/projects/{project_id}/commands", RestSharp.Method.POST);
                        request.AddParameter("project_id", projectId.Replace("b.", string.Empty), ParameterType.UrlSegment);
                        request.AddHeader("Authorization", "Bearer " + credentials.TokenInternal);
                        request.AddHeader("Content-Type", "application/vnd.api+json");
                        request.AddParameter("text/json", Newtonsoft.Json.JsonConvert.SerializeObject(payload), ParameterType.RequestBody);
                        IRestResponse res = await client.ExecuteTaskAsync(request);

                        for (int i = 0; i < 5760; i++)
                        {
                            System.Threading.Thread.Sleep(15000); // wait before first check... 
                            await credentials.RefreshAsync();
                            try
                            {
                                dynamic manifest = await derivative.GetManifestAsync(Base64Encode(versionUrn));
                                int progress = (string.IsNullOrWhiteSpace(Regex.Match(manifest.progress, @"\d+").Value) ? 100 : Int32.Parse(Regex.Match(manifest.progress, @"\d+").Value));
                                console.WriteLine(string.Format("Progress: {0}%", progress));
                                if (progress == 100) break;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                            }
                        }
                        console.WriteLine("Done");*/
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes).Replace("/", "_").Replace("=", string.Empty);
        }
    }
}