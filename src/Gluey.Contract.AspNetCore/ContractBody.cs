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
/// Bindable wrapper over <see cref="ParseResult"/> for ASP.NET Core minimal APIs.
/// Resolved via <see cref="HttpContextExtensions.GetContractBody"/>, auto-disposed
/// at the end of the request.
///
/// <para>Usage:</para>
/// <code>
/// app.MapPost("/orders", (HttpContext ctx) =>
/// {
///     var body = ctx.GetContractBody();
///     var name = body["name"].GetString();
///     return Results.Ok(new { name });
/// }).WithContractValidation(schema);
/// </code>
/// </summary>
public sealed class ContractBody : IDisposable
{
    private ParseResult _result;

    internal ContractBody(ParseResult result)
    {
        _result = result;
    }

    /// <summary>
    /// Gets the <see cref="ParsedProperty"/> with the given name.
    /// </summary>
    public ParsedProperty this[string name] => _result[name];

    /// <summary>
    /// Gets the <see cref="ParsedProperty"/> at the given ordinal.
    /// </summary>
    public ParsedProperty this[int ordinal] => _result[ordinal];

    /// <summary>Whether the parsed data passed all schema validations.</summary>
    public bool IsValid => _result.IsValid;

    /// <summary>The collected validation errors (empty when valid).</summary>
    public ErrorCollector Errors => _result.Errors;

    /// <summary>Returns the underlying <see cref="ParseResult"/>.</summary>
    public ParseResult Result => _result;

    /// <summary>
    /// Returns a struct enumerator over all parsed properties that have values.
    /// </summary>
    public ParseResult.Enumerator GetEnumerator() => _result.GetEnumerator();

    /// <summary>Disposes the underlying <see cref="ParseResult"/> and returns pooled buffers.</summary>
    public void Dispose()
    {
        _result.Dispose();
    }
}
