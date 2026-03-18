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

namespace Gluey.Contract.AspNetCore;

/// <summary>
/// Extension methods for accessing the validated contract data from <see cref="HttpContext"/>.
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Gets the validated request body as a <see cref="ParseResult"/>.
    /// Only available after <see cref="ContractValidationFilter"/> has run successfully.
    /// The caller is responsible for disposing the result.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A <see cref="ParseResult"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called outside a validated endpoint (no <c>WithContractValidation</c>).
    /// </exception>
    public static ParseResult GetContractResult(this HttpContext context)
    {
        if (context.Items.TryGetValue("Contract:Body", out var bodyObj) && bodyObj is byte[] body
            && context.Items.TryGetValue("Contract:Schema", out var schemaObj) && schemaObj is IContractSchema schema)
        {
            return schema.Parse(body)
                ?? throw new InvalidOperationException("Contract validation passed but re-parse returned null. This should not happen.");
        }

        throw new InvalidOperationException(
            "No validated contract data found. Ensure this endpoint uses .WithContractValidation().");
    }
}
