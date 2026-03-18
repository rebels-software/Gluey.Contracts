// Copyright 2026 Rebels Software sp. z o.o.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Gluey.Contract.AspNetCore;

/// <summary>
/// Endpoint filter that validates the request body against an <see cref="IContractSchema"/>
/// before the handler executes. Short-circuits with a 400 response on validation failure.
/// </summary>
internal sealed class ContractValidationFilter : IEndpointFilter
{
    private readonly IContractSchema _schema;

    internal ContractValidationFilter(IContractSchema schema)
    {
        _schema = schema;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        // Read the request body
        httpContext.Request.EnableBuffering();
        var body = await ReadBodyAsync(httpContext.Request);

        if (body is null || body.Length == 0)
        {
            return Results.Json(new ContractProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Title = "Validation failed",
                Status = StatusCodes.Status400BadRequest,
                Errors = [new ContractValidationError
                {
                    Path = "",
                    Code = "EmptyBody",
                    Message = "Request body is empty."
                }]
            }, statusCode: StatusCodes.Status400BadRequest);
        }

        // Parse and validate
        using var result = _schema.Parse(body);

        if (result is null)
        {
            return Results.Json(new ContractProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Title = "Validation failed",
                Status = StatusCodes.Status400BadRequest,
                Errors = [new ContractValidationError
                {
                    Path = "",
                    Code = "InvalidData",
                    Message = "Request body is structurally invalid."
                }]
            }, statusCode: StatusCodes.Status400BadRequest);
        }

        if (!result.Value.IsValid)
        {
            var options = httpContext.RequestServices.GetService<ContractOptions>();

            if (options?.OnValidationFailed is { } handler)
            {
                await handler(result.Value.Errors, httpContext);
                return Results.Empty;
            }

            var problemDetails = ProblemDetailsMapper.Build(result.Value.Errors, httpContext, options);
            return Results.Json(problemDetails, statusCode: StatusCodes.Status400BadRequest);
        }

        // Store the validated body in Items so handlers can access it
        httpContext.Items["Contract:Body"] = body;
        httpContext.Items["Contract:Schema"] = _schema;

        // Reset the body stream for downstream consumption
        httpContext.Request.Body.Position = 0;

        return await next(context);
    }

    private static async Task<byte[]?> ReadBodyAsync(HttpRequest request)
    {
        using var ms = new MemoryStream();
        await request.Body.CopyToAsync(ms);
        var bytes = ms.ToArray();
        request.Body.Position = 0;
        return bytes.Length > 0 ? bytes : null;
    }
}
