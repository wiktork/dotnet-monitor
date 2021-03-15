﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Diagnostics.Monitoring.OpenApiGen
{
    internal sealed class TooManyRequestsResponseDocumentFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            OpenApiHeader authenticateHeader = new OpenApiHeader();
            authenticateHeader.Schema = new OpenApiSchema() { Type = "string" };

            OpenApiResponse tooManyRequests = new OpenApiResponse();
            tooManyRequests.Description = "TooManyRequests";

            swaggerDoc.Components.Responses.Add(
                ResponseNames.TooManyRequestsResponse,
                tooManyRequests);
        }
    }
}
