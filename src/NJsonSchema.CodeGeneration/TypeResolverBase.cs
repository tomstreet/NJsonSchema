//-----------------------------------------------------------------------
// <copyright file="TypeResolverBase.cs" company="NJsonSchema">
//     Copyright (c) Rico Suter. All rights reserved.
// </copyright>
// <license>https://github.com/rsuter/NJsonSchema/blob/master/LICENSE.md</license>
// <author>Rico Suter, mail@rsuter.com</author>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

namespace NJsonSchema.CodeGeneration
{
    /// <summary>The type resolver base.</summary>
    public abstract class TypeResolverBase<TGenerator> : ITypeResolver
        where TGenerator : TypeGeneratorBase
    {
        private readonly Dictionary<string, TGenerator> _types = new Dictionary<string, TGenerator>();
        private readonly Dictionary<JsonSchema4, string> _generatedTypeNames = new Dictionary<JsonSchema4, string>();
        private readonly ITypeNameGenerator _typeNameGenerator;

        private int _anonymousTypeCount = 0;

        /// <summary>Initializes a new instance of the <see cref="TypeResolverBase{TGenerator}"/> class.</summary>
        /// <param name="typeNameGenerator">The type name generator.</param>
        protected TypeResolverBase(ITypeNameGenerator typeNameGenerator)
        {
            _typeNameGenerator = typeNameGenerator;
        }

        /// <summary>Determines whether the generator for a given type name is registered.</summary>
        /// <param name="typeName">Name of the type.</param>
        /// <returns></returns>
        public bool HasTypeGenerator(string typeName)
        {
            return _types.ContainsKey(typeName);
        }

        /// <summary>Adds the type generator for a given type name.</summary>
        /// <param name="typeName">Name of the type.</param>
        /// <param name="generator">The generator.</param>
        public void AddOrReplaceTypeGenerator(string typeName, TGenerator generator)
        {
            _types[typeName] = generator;
        }

        /// <summary>Tries to resolve the schema and returns null if there was a problem.</summary>
        /// <param name="schema">The schema.</param>
        /// <param name="typeNameHint">The type name hint.</param>
        /// <returns>The type name.</returns>
        public string TryResolve(JsonSchema4 schema, string typeNameHint)
        {
            return schema != null ? Resolve(schema, false, typeNameHint) : null;
        }

        /// <summary>Generates the code for all described types (e.g. interfaces, classes, enums, etc).</summary>
        /// <returns>The code.</returns>
        public string GenerateTypes(ExtensionCode extensionCode)
        {
            var processedTypes = new List<string>();
            var types = new Dictionary<string, TypeGeneratorResult>();
            while (_types.Any(t => !processedTypes.Contains(t.Key)))
            {
                foreach (var pair in _types.ToList())
                {
                    processedTypes.Add(pair.Key);
                    var result = pair.Value.GenerateType(pair.Key);
                    types[result.TypeName] = result;
                }
            }

            return string.Join("\n\n", ClassOrderUtilities.Order(types.Values).Select(p =>
            {
                if (extensionCode?.Classes.ContainsKey(p.TypeName) == true)
                    return p.Code + "\n\n" + extensionCode.Classes[p.TypeName];

                return p.Code;
            }));
        }

        /// <summary>Resolves and possibly generates the specified schema.</summary>
        /// <param name="schema">The schema.</param>
        /// <param name="isNullable">Specifies whether the given type usage is nullable.</param>
        /// <param name="typeNameHint">The type name hint to use when generating the type and the type name is missing.</param>
        /// <returns>The type name.</returns>
        public abstract string Resolve(JsonSchema4 schema, bool isNullable, string typeNameHint);

        /// <summary>Creates a type generator.</summary>
        /// <param name="schema">The schema.</param>
        /// <returns>The generator.</returns>
        protected abstract TGenerator CreateTypeGenerator(JsonSchema4 schema);

        /// <summary>Adds a generator for the given schema if necessary.</summary>
        /// <param name="schema">The schema.</param>
        /// <param name="typeNameHint">The type name hint.</param>
        /// <returns>The type name of the created generator.</returns>
        protected virtual string AddGenerator(JsonSchema4 schema, string typeNameHint)
        {
            var typeName = GetOrGenerateTypeName(schema, typeNameHint);
            if (!HasTypeGenerator(typeName))
            {
                var generator = CreateTypeGenerator(schema);
                AddOrReplaceTypeGenerator(typeName, generator);

                // add all definitions so that all schemas are generated (also subschemas in inheritance trees)
                foreach (var pair in schema.Definitions)
                    AddGenerator(pair.Value, pair.Key);
            }
            return typeName;
        }

        /// <summary>Gets or generates the type name for the given schema.</summary>
        /// <param name="schema">The schema.</param>
        /// <param name="typeNameHint">The type name hint.</param>
        /// <returns>The type name.</returns>
        protected virtual string GetOrGenerateTypeName(JsonSchema4 schema, string typeNameHint)
        {
            var typeName = schema.GetTypeName(_typeNameGenerator); 

            if (string.IsNullOrEmpty(typeName))
            {
                if (!_generatedTypeNames.ContainsKey(schema))
                    _generatedTypeNames[schema] = GenerateTypeName(typeNameHint);

                return _generatedTypeNames[schema];
            }

            return typeName;
        }

        /// <summary>Generates a unique type name.</summary>
        /// <returns>The type name.</returns>
        public string GenerateTypeName()
        {
            return GenerateTypeName(string.Empty);
        }

        /// <summary>Generates a unique type name with the given hint.</summary>
        /// <param name="typeNameHint">The type name hint.</param>
        /// <returns>The type name.</returns>
        public string GenerateTypeName(string typeNameHint)
        {
            if (!string.IsNullOrEmpty(typeNameHint))
            {
                if (!HasTypeGenerator(typeNameHint))
                    return typeNameHint;

                do
                {
                    _anonymousTypeCount++;
                } while (HasTypeGenerator(typeNameHint + _anonymousTypeCount));

                return typeNameHint + _anonymousTypeCount;
            }
            else
                return GenerateTypeName("Anonymous");
        }
    }
}