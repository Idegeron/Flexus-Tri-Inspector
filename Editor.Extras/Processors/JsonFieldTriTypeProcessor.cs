#if FLEXUS_SERIALIZATION
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using TriInspector;
using TriInspector.Processors;
using TriInspector.Utilities;

[assembly: RegisterTriTypeProcessor(typeof(JsonFieldTriTypeProcessor), 0)]

namespace TriInspector.Processors 
{
    public class JsonFieldTriTypeProcessor : TriTypeProcessor
    {
        public override void ProcessType(Type type, List<TriPropertyDefinition> properties)
        {
            const int fieldsOffset = 1;

            properties.AddRange(TriReflectionUtilities
                .GetAllInstanceFieldsInDeclarationOrder(type)
                .Where(IsSerialized)
                .Select((it, ind) => TriPropertyDefinition.CreateForFieldInfo(ind + fieldsOffset, it)));
        }

        private static bool IsSerialized(FieldInfo fieldInfo)
        {
            return fieldInfo.GetCustomAttribute<JsonPropertyAttribute>(false) != null;
        }
    }
}
#endif