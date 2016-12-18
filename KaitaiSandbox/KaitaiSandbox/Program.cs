using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using KT.Core;
using NJsonSchema;
using NJsonSchema.CodeGeneration.CSharp;
using NJsonSchema.CodeGeneration.TypeScript;
using NJsonSchema.Generation;
using NJsonSchema.Validation;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NodeDeserializers;
using YamlDotNet.Serialization.ObjectFactories;
using YamlDotNet.Serialization.Utilities;
using YamlDotNet.Serialization.ValueDeserializers;

namespace KaitaiSandbox
{
    class MyNodeTypeReslover : INodeTypeResolver
    {
        bool INodeTypeResolver.Resolve(NodeEvent nodeEvent, ref Type currentType)
        {
            var scalarEvent = nodeEvent as Scalar;
            if (currentType == typeof(object) && scalarEvent?.IsPlainImplicit == true)
            {
                var value = scalarEvent.Value;
                if (value == "true" || value == "false")
                {
                    currentType = typeof(bool);
                    return true;
                }

                if (Regex.IsMatch(value, "^(0x[0-9A-Fa-f_]+|[0-9]+)$"))
                {
                    currentType = typeof(long);
                    //var result = long.Parse(value);
                    return true;
                }
            }

            return false;
        }
    }

    class Program
    {
        public static string GitReposPath = AppDomain.CurrentDomain.BaseDirectory.Split(@"\kaitai_sandbox\")[0] + @"\";
        public static string KaitaiSandboxPath = $@"{GitReposPath}\kaitai_sandbox\";
        public static string ProjectPath = AppDomain.CurrentDomain.BaseDirectory.Split(@"\bin\")[0] + @"\";
        public static string KsyJsonPath = $@"{KaitaiSandboxPath}\ksy-jsons\";

        static void SaveIfNeeded(string fn, string text)
        {
            if (!File.Exists(fn) || File.ReadAllText(fn) != text)
                File.WriteAllText(FileUtils.ProvidePath(fn), text);
        }

        static void Main(string[] args)
        {
            var schema = JsonSchema4.FromJson(File.ReadAllText($@"{AppDomain.CurrentDomain.BaseDirectory}\ksy-json-schema.json"));
            var ksyValidationErrors = new Dictionary<string, ValidationError[]>();

            //var deserializer = new DeserializerBuilder().WithNodeDeserializer(new MyNodeDeserializer()).Build();
            //var deserializer = Deserializer.FromValueDeserializer(new MyValueDeserializer());
            //var deserializer = new Deserializer();
            var deserializer = new DeserializerBuilder().WithNodeTypeResolver(new MyNodeTypeReslover()).Build();
            deserializer.Deserialize(new StringReader("a: true\r\nb: 'true'"));

            var ksyBaseDir = $@"{GitReposPath}\kaitai_struct_webide\formats\";
            var ksyFiles = Directory.GetFiles(ksyBaseDir, "*.ksy", SearchOption.AllDirectories);
            foreach (var ksyFn in ksyFiles)
            {
                var ksyRelDir = ksyFn.Replace(ksyBaseDir, "");
                
                var obj = deserializer.Deserialize(new StringReader(File.ReadAllText(ksyFn)));
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.Indented);
                SaveIfNeeded($@"{KsyJsonPath}{ksyRelDir}.json", json);

                var validationErrors = schema.Validate(json).ToArray();
                if (validationErrors.Length > 0)
                    ksyValidationErrors[ksyRelDir] = validationErrors;
            }

            var allValidationErrors = ksyValidationErrors.SelectMany(x => x.Value.Select(err => new { fn = x.Key, err })).ToArray();
            var csharp = new CSharpGenerator(schema, new CSharpGeneratorSettings { ClassStyle = CSharpClassStyle.Poco }).GenerateFile("KsyFile");
            var typescript = new TypeScriptGenerator(schema, new TypeScriptGeneratorSettings()).GenerateFile("KsyFile");
        }
    }
}
