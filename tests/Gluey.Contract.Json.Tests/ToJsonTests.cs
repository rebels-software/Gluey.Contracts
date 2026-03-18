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

using System.Text;
using System.Text.Json;
using Gluey.Contract;
using Gluey.Contract.Json;

namespace Gluey.Contract.Json.Tests;

[TestFixture]
public class ToJsonTests
{
    private static (ParseResult result, JsonContractSchema schema) ParseWith(string schemaJson, string dataJson)
    {
        var schema = JsonContractSchema.Load(schemaJson)!;
        var result = schema.Parse(Encoding.UTF8.GetBytes(dataJson))!;
        return (result.Value, schema);
    }

    private static JsonDocument ParseJsonDoc(byte[] json)
        => JsonDocument.Parse(json);

    // ── Flat objects ─────────────────────────────────────────────────────

    [Test]
    public void ToJson_FlatObject_StringProperties()
    {
        var (result, schema) = ParseWith(
            """{"type":"object","properties":{"name":{"type":"string"},"email":{"type":"string"}}}""",
            """{"name":"Alice","email":"alice@example.com"}""");

        using (result)
        {
            var json = result.ToJson(schema);
            using var doc = ParseJsonDoc(json);
            doc.RootElement.GetProperty("name").GetString().Should().Be("Alice");
            doc.RootElement.GetProperty("email").GetString().Should().Be("alice@example.com");
        }
    }

    [Test]
    public void ToJson_FlatObject_IntegerProperty()
    {
        var (result, schema) = ParseWith(
            """{"type":"object","properties":{"age":{"type":"integer"}}}""",
            """{"age":42}""");

        using (result)
        {
            var json = result.ToJson(schema);
            using var doc = ParseJsonDoc(json);
            doc.RootElement.GetProperty("age").GetInt32().Should().Be(42);
        }
    }

    [Test]
    public void ToJson_FlatObject_NumberProperty()
    {
        var (result, schema) = ParseWith(
            """{"type":"object","properties":{"price":{"type":"number"}}}""",
            """{"price":19.99}""");

        using (result)
        {
            var json = result.ToJson(schema);
            using var doc = ParseJsonDoc(json);
            doc.RootElement.GetProperty("price").GetDouble().Should().BeApproximately(19.99, 0.001);
        }
    }

    [Test]
    public void ToJson_FlatObject_BooleanProperty()
    {
        var (result, schema) = ParseWith(
            """{"type":"object","properties":{"active":{"type":"boolean"}}}""",
            """{"active":true}""");

        using (result)
        {
            var json = result.ToJson(schema);
            using var doc = ParseJsonDoc(json);
            doc.RootElement.GetProperty("active").GetBoolean().Should().BeTrue();
        }
    }

    [Test]
    public void ToJson_FlatObject_NullProperty()
    {
        var (result, schema) = ParseWith(
            """{"type":"object","properties":{"value":{"type":"null"}}}""",
            """{"value":null}""");

        using (result)
        {
            var json = result.ToJson(schema);
            using var doc = ParseJsonDoc(json);
            doc.RootElement.GetProperty("value").ValueKind.Should().Be(JsonValueKind.Null);
        }
    }

    [Test]
    public void ToJson_MixedTypes()
    {
        var (result, schema) = ParseWith("""
            {
                "type": "object",
                "properties": {
                    "name": { "type": "string" },
                    "age": { "type": "integer" },
                    "score": { "type": "number" },
                    "active": { "type": "boolean" }
                }
            }
            """,
            """{"name":"Bob","age":30,"score":9.5,"active":false}""");

        using (result)
        {
            var json = result.ToJson(schema);
            using var doc = ParseJsonDoc(json);
            doc.RootElement.GetProperty("name").GetString().Should().Be("Bob");
            doc.RootElement.GetProperty("age").GetInt32().Should().Be(30);
            doc.RootElement.GetProperty("score").GetDouble().Should().BeApproximately(9.5, 0.001);
            doc.RootElement.GetProperty("active").GetBoolean().Should().BeFalse();
        }
    }

    // ── Missing properties ───────────────────────────────────────────────

    [Test]
    public void ToJson_MissingOptionalProperty_OmittedFromOutput()
    {
        var (result, schema) = ParseWith("""
            {
                "type": "object",
                "properties": {
                    "name": { "type": "string" },
                    "nickname": { "type": "string" }
                }
            }
            """,
            """{"name":"Alice"}""");

        using (result)
        {
            var json = result.ToJson(schema);
            using var doc = ParseJsonDoc(json);
            doc.RootElement.GetProperty("name").GetString().Should().Be("Alice");
            doc.RootElement.TryGetProperty("nickname", out _).Should().BeFalse();
        }
    }

    // ── Nested objects ───────────────────────────────────────────────────

    [Test]
    public void ToJson_NestedObject()
    {
        var (result, schema) = ParseWith("""
            {
                "type": "object",
                "properties": {
                    "address": {
                        "type": "object",
                        "properties": {
                            "street": { "type": "string" },
                            "city": { "type": "string" }
                        }
                    }
                }
            }
            """,
            """{"address":{"street":"123 Main St","city":"Springfield"}}""");

        using (result)
        {
            var json = result.ToJson(schema);
            using var doc = ParseJsonDoc(json);
            var addr = doc.RootElement.GetProperty("address");
            addr.GetProperty("street").GetString().Should().Be("123 Main St");
            addr.GetProperty("city").GetString().Should().Be("Springfield");
        }
    }

    // ── Arrays ───────────────────────────────────────────────────────────

    [Test]
    public void ToJson_StringArray()
    {
        var (result, schema) = ParseWith("""
            {
                "type": "object",
                "properties": {
                    "tags": {
                        "type": "array",
                        "items": { "type": "string" }
                    }
                }
            }
            """,
            """{"tags":["alpha","beta","gamma"]}""");

        using (result)
        {
            var json = result.ToJson(schema);
            using var doc = ParseJsonDoc(json);
            var tags = doc.RootElement.GetProperty("tags");
            tags.GetArrayLength().Should().Be(3);
            tags[0].GetString().Should().Be("alpha");
            tags[1].GetString().Should().Be("beta");
            tags[2].GetString().Should().Be("gamma");
        }
    }

    [Test]
    public void ToJson_IntegerArray()
    {
        var (result, schema) = ParseWith("""
            {
                "type": "object",
                "properties": {
                    "numbers": {
                        "type": "array",
                        "items": { "type": "integer" }
                    }
                }
            }
            """,
            """{"numbers":[1,2,3]}""");

        using (result)
        {
            var json = result.ToJson(schema);
            using var doc = ParseJsonDoc(json);
            var nums = doc.RootElement.GetProperty("numbers");
            nums.GetArrayLength().Should().Be(3);
            nums[0].GetInt32().Should().Be(1);
            nums[1].GetInt32().Should().Be(2);
            nums[2].GetInt32().Should().Be(3);
        }
    }

    [Test]
    public void ToJson_EmptyArray()
    {
        var (result, schema) = ParseWith("""
            {
                "type": "object",
                "properties": {
                    "items": {
                        "type": "array",
                        "items": { "type": "string" }
                    }
                }
            }
            """,
            """{"items":[]}""");

        using (result)
        {
            var json = result.ToJson(schema);
            using var doc = ParseJsonDoc(json);
            doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(0);
        }
    }

    // ── WriteJson (IBufferWriter overload) ────────────────────────────────

    [Test]
    public void WriteJson_ProducesSameOutputAsToJson()
    {
        var (result, schema) = ParseWith(
            """{"type":"object","properties":{"name":{"type":"string"},"age":{"type":"integer"}}}""",
            """{"name":"Alice","age":30}""");

        using (result)
        {
            var toJsonBytes = result.ToJson(schema);

            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            result.WriteJson(schema, buffer);
            var writeJsonBytes = buffer.WrittenSpan.ToArray();

            writeJsonBytes.Should().Equal(toJsonBytes);
        }
    }

    // ── Empty object ─────────────────────────────────────────────────────

    [Test]
    public void ToJson_EmptyObject_NoPropertiesPresent()
    {
        var (result, schema) = ParseWith(
            """{"type":"object","properties":{"name":{"type":"string"}}}""",
            """{}""");

        using (result)
        {
            var json = result.ToJson(schema);
            using var doc = ParseJsonDoc(json);
            doc.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
            doc.RootElement.EnumerateObject().Count().Should().Be(0);
        }
    }
}
