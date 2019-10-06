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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Autodesk.Forge;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Hangfire;
using Hangfire.States;

namespace forgeSample.Controllers
{
    public class WebhookController : ControllerBase
    {
        /// <summary>
        /// Credentials on this request
        /// </summary>
        private Credentials Credentials { get; set; }

        [HttpPost]
        [Route("api/forge/callback/webhook")]
        public async Task<IActionResult> WebhookCallback([FromBody]JObject body)
        {
            // catch any errors, we don't want to return 500
            try
            {
                string eventType = body["hook"]["event"].ToString();
                string userId = body["hook"]["createdBy"].ToString();
                string projectId = "b." + body["payload"]["project"].ToString();//body["hook"]["hookAttribute"]["projectId"].ToString();
                string versionId = body["resourceUrn"].ToString();
                string hubId = "b." + body["payload"]["tenant"].ToString();
                string fileName = body["payload"]["name"].ToString();
                string folderUrn = body["hook"]["tenant"].ToString();
                string itemUrn = body["payload"]["lineageUrn"].ToString();

                // do you want to filter events??
                if (eventType != "dm.version.added") return Ok();

                // use Hangfire to schedule a job
                BackgroundJobClient metadataQueue = new BackgroundJobClient();
                IState state = new EnqueuedState("metadata");
                metadataQueue.Create(() => ModelDerivativeController.ProcessFileAsync(userId, hubId, projectId, folderUrn, itemUrn, versionId, fileName, null), state);
            }
            catch { }

            // ALWAYS return ok (200)
            return Ok();
        }

        /// <summary>
        /// Base64 encode a string (source: http://stackoverflow.com/a/11743162)
        /// </summary>
        /// <param name="plainText"></param>
        /// <returns></returns>
        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
    }
}