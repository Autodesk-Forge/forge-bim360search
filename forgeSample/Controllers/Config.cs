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
using System.Net;
using System.Linq;

namespace forgeSample
{
    public class Config
    {
        /// <summary>
        /// Reads appsettings from web.config
        /// </summary>
        public static string GetAppSetting(string settingKey)
        {
            return Environment.GetEnvironmentVariable(settingKey);
        }

        public static string ConnectionString
        {
            get
            {
                return Config.GetAppSetting("OAUTH_DATABASE");//.Split("/").First();
            }
        }

        public static string AWSKey
        {
            get
            {
                return Config.GetAppSetting("AWS_ACCESS_KEY");
            }
        }

        public static string AWSSecret
        {
            get
            {
                return Config.GetAppSetting("AWS_SECRET_KEY");
            }
        }


        public static string DatabaseName
        {
            get
            {
                return Config.GetAppSetting("OAUTH_DATABASE").Split('/').Last().Split('?').First();
            }
        }

        public static string ElasticSearchServer
        {
            get
            {
                return Config.GetAppSetting("ELASTIC_SEARCH_SERVER");
            }
        }

        public static string ElasticSearchServerRegion
        {
            get
            {
                return Config.GetAppSetting("ELASTIC_SEARCH_SERVER_REGION");
            }
        }

        public static int ParallelJobs
        {
            get
            {
                string v = Config.GetAppSetting("FORGE_PARALLEL_JOBS");
                return (string.IsNullOrEmpty(v) ? 10 : Int32.Parse(v));
            }
        }

        public static string WebhookUrl
        {
            get
            {
                return Config.GetAppSetting("FORGE_WEBHOOK_URL") + "/api/forge/callback/webhook";
            }
        }
    }
}