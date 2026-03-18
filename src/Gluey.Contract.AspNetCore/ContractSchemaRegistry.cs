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

using System.Collections.Concurrent;
using Gluey.Contract.Json;

namespace Gluey.Contract.AspNetCore;

/// <summary>
/// Thread-safe registry of named <see cref="JsonContractSchema"/> instances.
/// Schemas are loaded once and cached for the application lifetime.
/// </summary>
public sealed class ContractSchemaRegistry
{
    private readonly ConcurrentDictionary<string, JsonContractSchema> _schemas = new();

    /// <summary>
    /// Registers a schema with the given name.
    /// </summary>
    /// <param name="name">The schema identifier.</param>
    /// <param name="schema">The compiled schema.</param>
    public void Add(string name, JsonContractSchema schema)
    {
        _schemas[name] = schema;
    }

    /// <summary>
    /// Registers a schema from a JSON string.
    /// </summary>
    /// <param name="name">The schema identifier.</param>
    /// <param name="schemaJson">The JSON Schema document.</param>
    /// <returns>The compiled <see cref="JsonContractSchema"/>, or <c>null</c> if invalid.</returns>
    public JsonContractSchema? Add(string name, string schemaJson)
    {
        var schema = JsonContractSchema.Load(schemaJson);
        if (schema is not null)
            _schemas[name] = schema;
        return schema;
    }

    /// <summary>
    /// Tries to retrieve a schema by name.
    /// </summary>
    /// <param name="name">The schema identifier.</param>
    /// <param name="schema">The resolved schema, or <c>null</c>.</param>
    /// <returns><c>true</c> if found.</returns>
    public bool TryGet(string name, out JsonContractSchema? schema)
    {
        return _schemas.TryGetValue(name, out schema);
    }

    /// <summary>
    /// Retrieves a schema by name. Throws <see cref="KeyNotFoundException"/> if not found.
    /// </summary>
    public JsonContractSchema Get(string name)
    {
        if (_schemas.TryGetValue(name, out var schema))
            return schema;
        throw new KeyNotFoundException($"Schema '{name}' not found in the contract registry.");
    }
}
