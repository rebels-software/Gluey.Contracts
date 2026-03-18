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

using System.Buffers;
using System.Text.Json;
using Gluey.Contract;

namespace Gluey.Contract.Json;

/// <summary>
/// Extension methods for serializing a <see cref="ParseResult"/> to JSON.
/// Works with any source format — the schema provides property names and types,
/// and <see cref="ParsedProperty"/> materializers provide the values.
/// </summary>
public static class ParseResultJsonExtensions
{
    /// <summary>
    /// Serializes the parsed result to a UTF-8 JSON byte array.
    /// Walks the schema tree and writes each property that has a value.
    /// </summary>
    /// <param name="result">The parse result to serialize.</param>
    /// <param name="schema">The schema used to parse the data (provides property names, types, and structure).</param>
    /// <returns>A UTF-8 encoded JSON byte array.</returns>
    public static byte[] ToJson(this ParseResult result, JsonContractSchema schema)
    {
        var buffer = new ArrayBufferWriter<byte>();
        result.WriteJson(schema, buffer);
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Serializes the parsed result as UTF-8 JSON into the provided buffer writer.
    /// Zero-allocation serialization when used with a pooled buffer.
    /// </summary>
    /// <param name="result">The parse result to serialize.</param>
    /// <param name="schema">The schema used to parse the data.</param>
    /// <param name="output">The buffer writer to write JSON into.</param>
    public static void WriteJson(this ParseResult result, JsonContractSchema schema, IBufferWriter<byte> output)
    {
        using var writer = new Utf8JsonWriter(output);
        WriteNode(writer, schema.Root, result);
        writer.Flush();
    }

    private static void WriteNode(Utf8JsonWriter writer, SchemaNode node, ParseResult result)
    {
        if (node.Properties is null)
            return;

        writer.WriteStartObject();

        foreach (var (name, childNode) in node.Properties)
        {
            var prop = result[childNode.Path];
            if (!prop.HasValue)
                continue;

            writer.WritePropertyName(name);
            WriteValue(writer, childNode, prop, result);
        }

        writer.WriteEndObject();
    }

    private static void WriteValue(Utf8JsonWriter writer, SchemaNode node, ParsedProperty prop, ParseResult result)
    {
        var effectiveNode = node.ResolvedRef ?? node;
        var schemaType = effectiveNode.Type;

        // Object with nested properties — recurse
        if (schemaType.HasValue && schemaType.Value.HasFlag(SchemaType.Object) && effectiveNode.Properties is not null)
        {
            writer.WriteStartObject();
            foreach (var (childName, childNode) in effectiveNode.Properties)
            {
                var childProp = prop[childName];
                if (!childProp.HasValue)
                    continue;

                writer.WritePropertyName(childName);
                WriteValue(writer, childNode, childProp, result);
            }
            writer.WriteEndObject();
            return;
        }

        // Array
        if (schemaType.HasValue && schemaType.Value.HasFlag(SchemaType.Array))
        {
            writer.WriteStartArray();
            var itemsNode = effectiveNode.Items;
            for (int i = 0; i < prop.Count; i++)
            {
                var element = prop[i];
                if (itemsNode is not null)
                    WriteValue(writer, itemsNode, element, result);
                else
                    WriteScalar(writer, null, element);
            }
            writer.WriteEndArray();
            return;
        }

        // Scalar
        WriteScalar(writer, schemaType, prop);
    }

    private static void WriteScalar(Utf8JsonWriter writer, SchemaType? schemaType, ParsedProperty prop)
    {
        if (!prop.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        var raw = prop.RawBytes;

        if (schemaType.HasValue)
        {
            if (schemaType.Value.HasFlag(SchemaType.String))
            {
                writer.WriteStringValue(raw);
                return;
            }

            if (schemaType.Value.HasFlag(SchemaType.Integer) || schemaType.Value.HasFlag(SchemaType.Number))
            {
                writer.WriteRawValue(raw);
                return;
            }

            if (schemaType.Value.HasFlag(SchemaType.Boolean))
            {
                writer.WriteBooleanValue(prop.GetBoolean());
                return;
            }

            if (schemaType.Value.HasFlag(SchemaType.Null))
            {
                writer.WriteNullValue();
                return;
            }
        }

        // No type info — infer from raw bytes
        if (raw.Length == 0)
        {
            writer.WriteNullValue();
            return;
        }

        byte first = raw[0];
        if (first == (byte)'t' || first == (byte)'f')
            writer.WriteBooleanValue(first == (byte)'t');
        else if (first == (byte)'n')
            writer.WriteNullValue();
        else if (first == (byte)'-' || (first >= (byte)'0' && first <= (byte)'9'))
            writer.WriteRawValue(raw);
        else
            writer.WriteStringValue(raw);
    }
}
